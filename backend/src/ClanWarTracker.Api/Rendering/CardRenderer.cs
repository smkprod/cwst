using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace ClanWarTracker.Api.Rendering;

public record WarCardModel(
    string PlayerName, string ClanName, int Fame, int Rank, int ClanSize,
    int DecksToday, int RacePosition, int RaceClans, string BotName, string? ArtUrl);

public record ProfileCardModel(
    string PlayerName, string? ClanName, int Level, int Trophies, int BestTrophies,
    int WarDayWins, int ThreeCrownWins, string BotName, string? ArtUrl);

public record ClanCardModel(
    string ClanName, string ClanTag, int Members, int ClanScore,
    int WarTrophies, int RequiredTrophies, string BotName, string? ArtUrl);

/// <summary>Карта колоды для отрисовки: иконку скачиваем, уровень подписываем.</summary>
public record DeckCardEntry(string Name, string IconUrl, int Level, bool Maxed);

public record DeckCardModel(
    string Title, string Subtitle, List<DeckCardEntry> Cards, double AvgLevel, string BotName);

/// <summary>
/// Рисует карточки для inline-режима.
///
/// Первая версия выглядела как таблица на тёмном прямоугольнике: плоская заливка,
/// пустая правая половина и оторванные от неё цифры слева. Здесь исправлено ровно это —
/// диагональный градиент с подсветкой, арт карты из самой игры справа и выровненная
/// сетка плиток, у которых текст стоит по общей базовой линии.
///
/// Шрифт берётся из файла рядом с приложением: в образе .NET шрифтов нет вообще,
/// и системный вызов вернул бы пустоту, а кириллица стала бы квадратами.
/// </summary>
public class CardRenderer(IWebHostEnvironment env, IHttpClientFactory http, IMemoryCache cache)
{
    public const int Width = 800;
    public const int Height = 420;

    private const float Pad = 52;

    // Палитра арены: глубокий индиго с фиолетовой подсветкой — так выглядит игра,
    // а не «тёмная тема дашборда», которой была первая версия.
    private static readonly SKColor Deep = SKColor.Parse("#0b0d1a");
    private static readonly SKColor Mid = SKColor.Parse("#241a4d");
    private static readonly SKColor Glow = SKColor.Parse("#6d3bd6");
    private static readonly SKColor Panel = SKColor.Parse("#ffffff");
    private static readonly SKColor Text = SKColor.Parse("#f4f5fb");
    private static readonly SKColor Muted = SKColor.Parse("#9aa1bd");
    private static readonly SKColor Gold = SKColor.Parse("#ffc83d");

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

    public byte[] RenderWar(WarCardModel m) => Draw(m.ArtUrl, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.PlayerName, m.ClanName, m.BotName);
        Hero(canvas, p, Num(m.Fame), "медалей за неделю");
        Tiles(canvas, p,
            ($"#{m.Rank}", $"из {m.ClanSize} в клане"),
            ($"{m.DecksToday}/4", "колод сегодня"),
            ($"{m.RacePosition}/{m.RaceClans}", "место в гонке"));
    });

    public byte[] RenderProfile(ProfileCardModel m) => Draw(m.ArtUrl, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.PlayerName, m.ClanName ?? "без клана", m.BotName);
        Hero(canvas, p, Num(m.Trophies), $"кубков · рекорд {Num(m.BestTrophies)}");
        Tiles(canvas, p,
            (m.Level.ToString(), "уровень"),
            (Num(m.WarDayWins), "побед в КВ"),
            (Num(m.ThreeCrownWins), "три короны"));
    });

    public byte[] RenderClan(ClanCardModel m) => Draw(m.ArtUrl, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.ClanName, m.ClanTag, m.BotName);
        Hero(canvas, p, Num(m.WarTrophies), "трофеев Клановых войн");
        Tiles(canvas, p,
            ($"{m.Members}/50", "участников"),
            (Num(m.ClanScore), "очки клана"),
            (Num(m.RequiredTrophies), "порог входа"));
    });

    /// <summary>Колода: восемь карт настоящими артами — ради этого картинки и затевались.</summary>
    public byte[] RenderDeck(DeckCardModel m) => Draw(null, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.Title, m.Subtitle, m.BotName);

        const int cols = 4, cw = 132, gapX = 20, gapY = 14;
        var ch = (int)(cw * 1.2f);
        var startX = (Width - (cols * cw + (cols - 1) * gapX)) / 2f;
        const float top = 158;

        for (var i = 0; i < Math.Min(8, m.Cards.Count); i++)
        {
            var c = m.Cards[i];
            var x = startX + i % cols * (cw + gapX);
            var y = top + i / cols * (ch + gapY + 8);

            var img = Icon(c.IconUrl);
            if (img is not null)
            {
                canvas.DrawImage(img, new SKRect(x, y, x + cw, y + ch));
            }
            else
            {
                // Иконка не скачалась — рисуем заглушку, а не дыру в вёрстке
                p.Color = Panel.WithAlpha(18);
                canvas.DrawRoundRect(new SKRect(x, y, x + cw, y + ch), 10, 10, p);
            }

            // Уровень — плашкой поверх нижнего края арта, как значок в игре
            var badge = new SKRect(x + cw / 2f - 26, y + ch - 20, x + cw / 2f + 26, y + ch + 12);
            p.Color = Deep.WithAlpha(230);
            canvas.DrawRoundRect(badge, 10, 10, p);
            p.Color = c.Maxed ? Gold : Text;
            canvas.DrawText(c.Level.ToString(), badge.MidX, badge.MidY + 9,
                SKTextAlign.Center, new SKFont(_bold, 24), p);
        }

        p.Color = Muted;
        canvas.DrawText($"средний уровень {m.AvgLevel}", Width / 2f, Height - 20,
            SKTextAlign.Center, new SKFont(_regular, 20), p);
    });

    /// <summary>
    /// Общий каркас: диагональный градиент, подсветка, арт карты справа и кодирование.
    ///
    /// Арт заводится под градиентную вуаль слева направо — иначе картинка обрывалась бы
    /// прямой линией посреди карточки и читалась бы как ошибка вёрстки, а не как фон.
    /// </summary>
    private byte[] Draw(string? artUrl, Action<SKCanvas> body)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;

        using (var bg = new SKPaint { IsAntialias = true })
        {
            bg.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, Height),
                [Mid, Deep], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, Height), bg);
        }

        // Мягкое пятно света в левом верхнем углу — там, где имя игрока
        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateRadialGradient(
                new SKPoint(120, 40), 520,
                [Glow.WithAlpha(120), Glow.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, Height), glow);
        }

        var art = artUrl is null ? null : Icon(artUrl);
        if (art is not null)
        {
            // Арт крупно у правого края, с выходом за границы — так он читается как фон,
            // а не как приклеенная сбоку картинка
            const float size = 460;
            canvas.DrawImage(art, new SKRect(Width - size + 60, -40, Width + 60, size - 40 + 40));

            using var veil = new SKPaint { IsAntialias = true };
            veil.Shader = SKShader.CreateLinearGradient(
                new SKPoint(Width - size, 0), new SKPoint(Width - 40, 0),
                [Deep, Deep.WithAlpha(120)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(Width - size, 0, Width, Height), veil);
        }

        body(canvas);

        // Золотая полоса по низу — единственный «фирменный» штрих, который держит композицию
        using (var line = new SKPaint { IsAntialias = true })
        {
            line.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, 0),
                [Gold, Gold.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, Height - 5, Width, Height), line);
        }

        // JPEG, а не PNG: Telegram для inline-фото принимает только его.
        // Прозрачности тут нет (фон закрашен целиком), так что терять нечего.
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private void Header(SKCanvas canvas, SKPaint p, string title, string subtitle, string bot)
    {
        p.Color = Text;
        var titleFont = new SKFont(_bold, 42);
        canvas.DrawText(Fit(Clean(title), titleFont, 470), Pad, 92, titleFont, p);

        p.Color = Muted;
        var subFont = new SKFont(_regular, 22);
        canvas.DrawText(Fit(Clean(subtitle), subFont, 470), Pad, 126, subFont, p);
        canvas.DrawText($"@{bot}", Width - Pad, 62, SKTextAlign.Right, new SKFont(_regular, 18), p);
    }

    /// <summary>Главное число карточки с подписью под ним.</summary>
    private void Hero(SKCanvas canvas, SKPaint p, string value, string label)
    {
        p.Color = Gold;
        canvas.DrawText(value, Pad, 252, new SKFont(_bold, 86), p);
        p.Color = Muted;
        canvas.DrawText(label, Pad, 286, new SKFont(_regular, 20), p);
    }

    /// <summary>
    /// Три плитки в ряд по общей сетке. Ширина считается от полей, а не задаётся
    /// числом: в первой версии плитки не доставали до правого края и композиция
    /// выглядела съехавшей.
    /// </summary>
    private void Tiles(SKCanvas canvas, SKPaint p, params (string Value, string Label)[] tiles)
    {
        const float gap = 16, top = 312, h = 78;
        var w = (Width - 2 * Pad - gap * (tiles.Length - 1)) / tiles.Length;

        for (var i = 0; i < tiles.Length; i++)
        {
            var x = Pad + i * (w + gap);
            var rect = new SKRect(x, top, x + w, top + h);

            p.Color = Panel.WithAlpha(20);
            canvas.DrawRoundRect(rect, 16, 16, p);

            p.Color = Text;
            canvas.DrawText(tiles[i].Value, x + 18, top + 40, new SKFont(_bold, 32), p);
            p.Color = Muted;
            canvas.DrawText(tiles[i].Label, x + 18, top + 65, new SKFont(_regular, 17), p);
        }
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
    /// Выбрасывает символы, которых нет в шрифте.
    ///
    /// Игроки любят эмодзи в никах, а DejaVu их не содержит — и вместо дракона в имени
    /// появлялся пустой квадрат, выглядевший как поломка. Цветной эмодзи-шрифт весит
    /// десяток мегабайт ради украшения, поэтому такие символы просто убираем: имя без
    /// эмодзи читается нормально, имя с квадратом — нет.
    /// </summary>
    private string Clean(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            var element = (string)e.Current;
            var cp = char.ConvertToUtf32(element, 0);
            // Пробел не ищем в шрифте — он есть всегда, а ContainsGlyph на нём капризен
            if (cp == ' ' || _regular.ContainsGlyph(cp)) sb.Append(element);
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Подрезает строку под ширину. Имена в CR бывают длинными, а вылезший за край
    /// текст выглядит как поломка вёрстки — многоточие честнее.
    /// </summary>
    private static string Fit(string text, SKFont font, float maxWidth)
    {
        if (text.Length == 0 || font.MeasureText(text) <= maxWidth) return text;

        var cut = text;
        while (cut.Length > 1 && font.MeasureText(cut + "…") > maxWidth)
            cut = cut[..^1];
        return cut + "…";
    }
}
