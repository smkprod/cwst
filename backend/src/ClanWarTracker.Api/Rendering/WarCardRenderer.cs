using SkiaSharp;

namespace ClanWarTracker.Api.Rendering;

/// <summary>Всё, что попадает на карточку. Считается снаружи — рисовалка ничего не запрашивает.</summary>
public record WarCardModel(
    string PlayerName,
    string ClanName,
    int Fame,
    int Rank,
    int ClanSize,
    int DecksToday,
    int RacePosition,
    int RaceClans,
    string BotName);

/// <summary>
/// Рисует картинку «моя война» для inline-режима.
///
/// Текстовую карточку в чужом чате пролистывают, картинку — разглядывают. Ради этого
/// и заводится рисование: inline существует не для удобства своих, а чтобы бота
/// увидели те, кто о нём не слышал.
///
/// Шрифт берётся из файла рядом с приложением: в образе .NET шрифтов нет вообще,
/// и системный вызов вернул бы пустоту, а кириллица стала бы квадратами.
/// </summary>
public class WarCardRenderer
{
    public const int Width = 800;
    public const int Height = 420;

    private static readonly SKColor Bg = SKColor.Parse("#0f1017");
    private static readonly SKColor Panel = SKColor.Parse("#171922");
    private static readonly SKColor Text = SKColor.Parse("#eef0f6");
    private static readonly SKColor Muted = SKColor.Parse("#8b93a7");
    private static readonly SKColor Accent = SKColor.Parse("#9c6bff");
    private static readonly SKColor Gold = SKColor.Parse("#ffc83d");

    private readonly SKTypeface _regular;
    private readonly SKTypeface _bold;

    public WarCardRenderer(IWebHostEnvironment env)
    {
        // ContentRootPath, а не текущая папка: в контейнере рабочий каталог может
        // не совпадать с тем, куда положены файлы приложения.
        var dir = Path.Combine(env.ContentRootPath, "Assets");
        _regular = Load(Path.Combine(dir, "DejaVuSans.ttf"));
        _bold = Load(Path.Combine(dir, "DejaVuSans-Bold.ttf"));
    }

    /// <summary>
    /// Шрифт обязателен: без него подписи не нарисуются, и лучше упасть при старте
    /// с внятной причиной, чем годами отдавать карточки с пустыми строками.
    /// </summary>
    private static SKTypeface Load(string path)
    {
        using var stream = File.OpenRead(path);
        return SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException($"Не удалось загрузить шрифт: {path}");
    }

    public byte[] Render(WarCardModel m)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(Bg);

        using var fill = new SKPaint { IsAntialias = true };

        // Мягкое свечение сверху, чтобы карточка не выглядела плоским прямоугольником
        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, Height),
                [Accent.WithAlpha(46), Bg],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, Height), glow);
        }

        var nameFont = new SKFont(_bold, 44);
        var clanFont = new SKFont(_regular, 24);
        var hugeFont = new SKFont(_bold, 92);
        var labelFont = new SKFont(_regular, 20);
        var statFont = new SKFont(_bold, 34);
        var footFont = new SKFont(_regular, 20);

        // Имя и клан
        fill.Color = Text;
        canvas.DrawText(Fit(m.PlayerName, nameFont, 520), 48, 96, nameFont, fill);
        fill.Color = Muted;
        canvas.DrawText(Fit(m.ClanName, clanFont, 520), 48, 132, clanFont, fill);

        // Главное число — медали за неделю
        fill.Color = Gold;
        canvas.DrawText(Num(m.Fame), 48, 250, hugeFont, fill);
        fill.Color = Muted;
        canvas.DrawText("медалей за неделю", 48, 286, labelFont, fill);

        // Плитки со всем остальным
        Tile(canvas, fill, 48, 316, $"#{m.Rank}", $"из {m.ClanSize} в клане", statFont, labelFont);
        Tile(canvas, fill, 288, 316, $"{m.DecksToday}/4", "колод сегодня", statFont, labelFont);
        Tile(canvas, fill, 528, 316, $"{m.RacePosition}/{m.RaceClans}", "место в гонке", statFont, labelFont);

        fill.Color = Muted;
        canvas.DrawText($"@{m.BotName}", Width - 48, 72, SKTextAlign.Right, footFont, fill);

        // JPEG, а не PNG: Telegram для inline-фото принимает именно его. Прозрачности
        // тут нет (фон закрашен целиком), так что терять нечего.
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    private static void Tile(SKCanvas canvas, SKPaint fill, float x, float y,
        string value, string label, SKFont valueFont, SKFont labelFont)
    {
        fill.Color = Panel;
        canvas.DrawRoundRect(new SKRect(x, y, x + 224, y + 76), 14, 14, fill);
        fill.Color = Text;
        canvas.DrawText(value, x + 18, y + 42, valueFont, fill);
        fill.Color = Muted;
        canvas.DrawText(label, x + 18, y + 66, labelFont, fill);
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
