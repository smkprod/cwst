using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace ClanWarTracker.Api.Rendering;

public record WarCardModel(
    string PlayerName, string ClanName, int Fame, int Rank, int ClanSize,
    int DecksToday, int RacePosition, int RaceClans, string BotName);

public record ProfileCardModel(
    string PlayerName, string? ClanName, int Level, int Trophies, int BestTrophies,
    int WarDayWins, int ThreeCrownWins, string BotName);

public record ClanCardModel(
    string ClanName, string ClanTag, int Members, int ClanScore,
    int WarTrophies, int RequiredTrophies, string BotName);

/// <summary>Карта колоды для отрисовки: иконку скачиваем, уровень подписываем.</summary>
public record DeckCardEntry(string Name, string IconUrl, int Level, bool Maxed);

public record DeckCardModel(
    string Title, string Subtitle, List<DeckCardEntry> Cards, double AvgLevel, string BotName);

/// <summary>
/// Рисует карточки для inline-режима.
///
/// Текстовую карточку в чужом чате пролистывают, картинку разглядывают. Inline
/// существует не ради удобства своих, а чтобы бота увидели те, кто о нём не слышал.
///
/// Шрифт берётся из файла рядом с приложением: в образе .NET шрифтов нет вообще,
/// и системный вызов вернул бы пустоту, а кириллица стала бы квадратами.
/// </summary>
public class CardRenderer(IWebHostEnvironment env, IHttpClientFactory http, IMemoryCache cache)
{
    public const int Width = 800;
    public const int Height = 420;

    private static readonly SKColor Bg = SKColor.Parse("#0f1017");
    private static readonly SKColor Panel = SKColor.Parse("#171922");
    private static readonly SKColor Text = SKColor.Parse("#eef0f6");
    private static readonly SKColor Muted = SKColor.Parse("#8b93a7");
    private static readonly SKColor Accent = SKColor.Parse("#9c6bff");
    private static readonly SKColor Gold = SKColor.Parse("#ffc83d");
    private static readonly SKColor Green = SKColor.Parse("#4caf74");

    private readonly SKTypeface _regular = Load(env, "DejaVuSans.ttf");
    private readonly SKTypeface _bold = Load(env, "DejaVuSans-Bold.ttf");

    /// <summary>
    /// Шрифт обязателен: без него подписи не нарисуются, и лучше упасть при старте
    /// с внятной причиной, чем годами отдавать карточки с пустыми строками.
    /// </summary>
    private static SKTypeface Load(IWebHostEnvironment env, string file)
    {
        // ContentRootPath, а не текущая папка: в контейнере рабочий каталог может
        // не совпадать с тем, куда положены файлы приложения.
        var path = Path.Combine(env.ContentRootPath, "Assets", file);
        using var stream = File.OpenRead(path);
        return SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException($"Не удалось загрузить шрифт: {path}");
    }

    public byte[] RenderWar(WarCardModel m) => Draw(canvas =>
    {
        using var fill = new SKPaint { IsAntialias = true };
        Header(canvas, fill, m.PlayerName, m.ClanName, m.BotName);

        fill.Color = Gold;
        canvas.DrawText(Num(m.Fame), 48, 250, new SKFont(_bold, 92), fill);
        fill.Color = Muted;
        canvas.DrawText("медалей за неделю", 48, 286, new SKFont(_regular, 20), fill);

        Tile(canvas, fill, 48, 316, $"#{m.Rank}", $"из {m.ClanSize} в клане");
        Tile(canvas, fill, 288, 316, $"{m.DecksToday}/4", "колод сегодня");
        Tile(canvas, fill, 528, 316, $"{m.RacePosition}/{m.RaceClans}", "место в гонке");
    });

    public byte[] RenderProfile(ProfileCardModel m) => Draw(canvas =>
    {
        using var fill = new SKPaint { IsAntialias = true };
        Header(canvas, fill, m.PlayerName, m.ClanName ?? "без клана", m.BotName);

        fill.Color = Gold;
        canvas.DrawText(Num(m.Trophies), 48, 250, new SKFont(_bold, 92), fill);
        fill.Color = Muted;
        canvas.DrawText($"кубков · рекорд {Num(m.BestTrophies)}", 48, 286, new SKFont(_regular, 20), fill);

        Tile(canvas, fill, 48, 316, m.Level.ToString(), "уровень");
        Tile(canvas, fill, 288, 316, Num(m.WarDayWins), "побед в КВ");
        Tile(canvas, fill, 528, 316, Num(m.ThreeCrownWins), "три короны");
    });

    public byte[] RenderClan(ClanCardModel m) => Draw(canvas =>
    {
        using var fill = new SKPaint { IsAntialias = true };
        Header(canvas, fill, m.ClanName, m.ClanTag, m.BotName);

        fill.Color = Gold;
        canvas.DrawText(Num(m.WarTrophies), 48, 250, new SKFont(_bold, 92), fill);
        fill.Color = Muted;
        canvas.DrawText("трофеев Клановых войн", 48, 286, new SKFont(_regular, 20), fill);

        Tile(canvas, fill, 48, 316, $"{m.Members}/50", "участников");
        Tile(canvas, fill, 288, 316, Num(m.ClanScore), "очки клана");
        Tile(canvas, fill, 528, 316, Num(m.RequiredTrophies), "порог входа");
    });

    /// <summary>Колода: восемь карт настоящими иконками — ради этого картинки и затевались.</summary>
    public byte[] RenderDeck(DeckCardModel m) => Draw(canvas =>
    {
        using var fill = new SKPaint { IsAntialias = true };
        Header(canvas, fill, m.Title, m.Subtitle, m.BotName);

        const int cols = 4, size = 150, gapX = 24, gapY = 18;
        var startX = (Width - (cols * size + (cols - 1) * gapX)) / 2f;

        for (var i = 0; i < Math.Min(8, m.Cards.Count); i++)
        {
            var c = m.Cards[i];
            var x = startX + i % cols * (size + gapX);
            var y = 150 + i / cols * (size * 1.2f + gapY);

            var img = Icon(c.IconUrl);
            if (img is not null)
            {
                canvas.DrawImage(img, new SKRect(x, y, x + size, y + size * 1.2f));
            }
            else
            {
                // Иконка не скачалась — рисуем заглушку, а не дыру в вёрстке
                fill.Color = Panel;
                canvas.DrawRoundRect(new SKRect(x, y, x + size, y + size * 1.2f), 10, 10, fill);
            }

            fill.Color = c.Maxed ? Gold : Text;
            canvas.DrawText(c.Level.ToString(), x + size / 2f, y + size * 1.2f + 26,
                SKTextAlign.Center, new SKFont(_bold, 24), fill);
        }

        fill.Color = Muted;
        canvas.DrawText($"средний уровень {m.AvgLevel}", Width / 2f, Height - 16,
            SKTextAlign.Center, new SKFont(_regular, 20), fill);
    });

    /// <summary>Общий каркас: фон, свечение, кодирование в JPEG.</summary>
    private static byte[] Draw(Action<SKCanvas> body)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(Bg);

        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, Height),
                [Accent.WithAlpha(46), Bg], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, Height), glow);
        }

        body(canvas);

        // JPEG, а не PNG: Telegram для inline-фото принимает только его.
        // Прозрачности тут нет (фон закрашен целиком), так что терять нечего.
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    private void Header(SKCanvas canvas, SKPaint fill, string title, string subtitle, string bot)
    {
        fill.Color = Text;
        var titleFont = new SKFont(_bold, 44);
        canvas.DrawText(Fit(title, titleFont, 520), 48, 96, titleFont, fill);

        fill.Color = Muted;
        var subFont = new SKFont(_regular, 24);
        canvas.DrawText(Fit(subtitle, subFont, 520), 48, 132, subFont, fill);
        canvas.DrawText($"@{bot}", Width - 48, 72, SKTextAlign.Right, new SKFont(_regular, 20), fill);
    }

    private void Tile(SKCanvas canvas, SKPaint fill, float x, float y, string value, string label)
    {
        fill.Color = Panel;
        canvas.DrawRoundRect(new SKRect(x, y, x + 224, y + 76), 14, 14, fill);
        fill.Color = Text;
        canvas.DrawText(value, x + 18, y + 42, new SKFont(_bold, 34), fill);
        fill.Color = Muted;
        canvas.DrawText(label, x + 18, y + 66, new SKFont(_regular, 20), fill);
    }

    /// <summary>
    /// Иконка карты с CDN игры. Держим в памяти сутки: картинки неизменны, а без кэша
    /// каждая карточка колоды означала бы восемь загрузок по сети.
    /// null — не скачалась, вызывающий рисует заглушку.
    /// </summary>
    private SKImage? Icon(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        return cache.GetOrCreate($"cardicon:{url}", entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            try
            {
                using var client = http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                return SKImage.FromEncodedData(bytes);
            }
            catch
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return null;
            }
        });
    }

    /// <summary>Разряды пробелами и без привязки к культуре: 12345 → «12 345».</summary>
    private static string Num(int n) =>
        n.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');

    /// <summary>
    /// Подрезает строку под ширину. Имена в CR бывают длинными и с эмодзи, а вылезший
    /// за край текст выглядит как поломка вёрстки — многоточие честнее.
    /// </summary>
    private static string Fit(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;

        var cut = text;
        while (cut.Length > 1 && font.MeasureText(cut + "…") > maxWidth)
            cut = cut[..^1];
        return cut + "…";
    }
}
