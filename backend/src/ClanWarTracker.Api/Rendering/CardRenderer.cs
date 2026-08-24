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
/// Вся значимая часть держится выше подвала: в превью Telegram низ картинки видно не
/// всегда, и подписи под цифрами там срезало. Внизу теперь только имя бота и полоска —
/// то, что не жалко потерять.
///
/// Шрифт берётся из файла рядом с приложением: в образе .NET шрифтов нет вообще,
/// и системный вызов вернул бы пустоту, а кириллица стала бы квадратами.
/// </summary>
public class CardRenderer(IWebHostEnvironment env, IHttpClientFactory http, IMemoryCache cache)
{
    public const int Width = 800;

    /// <summary>
    /// Высота карточек со статистикой. Была 420 — при такой высоте композиция
    /// растягивалась, а нижний ряд плиток оказывался у самого края.
    /// </summary>
    public const int StatHeight = 360;

    /// <summary>Колода выше остальных: восемь артов в два ряда просто не влезают ниже.</summary>
    public const int DeckHeight = 540;

    private const float Pad = 44;

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

    public byte[] RenderWar(WarCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.PlayerName, m.ClanName, m.BotName, StatHeight);
        Hero(canvas, p, Num(m.Fame), "медалей за неделю");
        Tiles(canvas, p,
            ($"#{m.Rank}", $"из {m.ClanSize} в клане"),
            ($"{m.DecksToday}/4", "колод сегодня"),
            ($"{m.RacePosition}/{m.RaceClans}", "место в гонке"));
    });

    public byte[] RenderProfile(ProfileCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.PlayerName, m.ClanName ?? "без клана", m.BotName, StatHeight);
        Hero(canvas, p, Num(m.Trophies), $"кубков · рекорд {Num(m.BestTrophies)}");
        Tiles(canvas, p,
            (m.Level.ToString(), "уровень"),
            (Num(m.WarDayWins), "побед в КВ"),
            (Num(m.ThreeCrownWins), "три короны"));
    });

    public byte[] RenderClan(ClanCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        using var p = new SKPaint { IsAntialias = true };
        Header(canvas, p, m.ClanName, m.ClanTag, m.BotName, StatHeight);
        Hero(canvas, p, Num(m.WarTrophies), "трофеев Клановых войн");
        Tiles(canvas, p,
            ($"{m.Members}/50", "участников"),
            (Num(m.ClanScore), "очки клана"),
            (Num(m.RequiredTrophies), "порог входа"));
    });

    /// <summary>Колода: восемь карт настоящими артами — ради этого картинки и затевались.</summary>
    public byte[] RenderDeck(DeckCardModel m)
    {
        // Восемь иконок сразу, а не по одной внутри отрисовки: последовательно они
        // качались до сорока секунд на холодном кэше, и Telegram успевал бросить
        // загрузку на середине — в чате появлялась половина картинки и серая заливка.
        Warm(m.Cards.Select(c => c.IconUrl));

        return Draw(null, DeckHeight, canvas =>
        {
            using var p = new SKPaint { IsAntialias = true };
            Header(canvas, p, m.Title, m.Subtitle, m.BotName, DeckHeight);

            const int cols = 4, cw = 132, gapX = 20, gapY = 14;
            var ch = (int)(cw * 1.2f);
            var startX = (Width - (cols * cw + (cols - 1) * gapX)) / 2f;
            const float top = 146;

            for (var i = 0; i < Math.Min(8, m.Cards.Count); i++)
            {
                var c = m.Cards[i];
                var x = startX + i % cols * (cw + gapX);
                var y = top + i / cols * (ch + gapY + 8);

                DeckSlot(canvas, p, c, x, y, cw, ch);
            }

            p.Color = Muted;
            canvas.DrawText($"средний уровень {m.AvgLevel}", Pad, DeckHeight - 20,
                new SKFont(_regular, 20), p);
        });
    }

    /// <summary>Одна карта колоды: арт и плашка с уровнем поверх нижнего края.</summary>
    private void DeckSlot(SKCanvas canvas, SKPaint p, DeckCardEntry c, float x, float y, int cw, int ch)
    {
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

    /// <summary>
    /// Общий каркас: диагональный градиент, подсветка, арт карты справа и кодирование.
    ///
    /// Арт заводится под градиентную вуаль слева направо — иначе картинка обрывалась бы
    /// прямой линией посреди карточки и читалась бы как ошибка вёрстки, а не как фон.
    /// </summary>
    private byte[] Draw(string? artUrl, int height, Action<SKCanvas> body)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, height));
        var canvas = surface.Canvas;

        using (var bg = new SKPaint { IsAntialias = true })
        {
            bg.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, height),
                [Mid, Deep], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, height), bg);
        }

        // Мягкое пятно света в левом верхнем углу — там, где имя игрока
        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateRadialGradient(
                new SKPoint(110, 30), 480,
                [Glow.WithAlpha(110), Glow.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, height), glow);
        }

        if (artUrl is not null) ArtPanel(canvas, Icon(artUrl));

        body(canvas);

        // Золотая полоса по низу — единственный «фирменный» штрих, который держит композицию
        using (var line = new SKPaint { IsAntialias = true })
        {
            line.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, 0),
                [Gold, Gold.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, height - 5, Width, height), line);
        }

        // JPEG, а не PNG: Telegram для inline-фото принимает только его.
        // 88 вместо 90 — на глаз неотличимо, но файл заметно легче, а лёгкий файл
        // Telegram дотягивает целиком даже на плохой сети.
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    /// <summary>
    /// Арт карты отдельной панелью у правого края.
    ///
    /// Сначала он растягивался фоном во всю правую половину — и получалось плохо сразу
    /// по трём причинам: на границе с фоном оставался хард-стык, лица кричали громче
    /// цифр, а плитки со статистикой ложились поверх картинки и переставали читаться.
    /// Панель решает всё это разом: у арта есть своя рамка, он не наезжает на текст,
    /// и вписан целиком, поэтому никого не обрезает.
    /// </summary>
    private static void ArtPanel(SKCanvas canvas, SKImage? art)
    {
        var panel = new SKRect(ArtLeft, 40, Width - Pad, 226);

        using var p = new SKPaint { IsAntialias = true };
        p.Color = Panel.WithAlpha(16);
        canvas.DrawRoundRect(panel, 18, 18, p);

        if (art is not null)
        {
            // Вписываем целиком (contain), а не заполняем: обрезка съела бы половину
            // персонажа, а панель маленькая — потеря была бы заметной.
            const float inset = 12;
            var box = new SKRect(panel.Left + inset, panel.Top + inset,
                panel.Right - inset, panel.Bottom - inset);
            var scale = Math.Min(box.Width / art.Width, box.Height / art.Height);
            var w = art.Width * scale;
            var h = art.Height * scale;
            canvas.DrawImage(art, new SKRect(
                box.MidX - w / 2, box.MidY - h / 2, box.MidX + w / 2, box.MidY + h / 2));
        }

        p.Color = Panel.WithAlpha(38);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = 2;
        canvas.DrawRoundRect(panel, 18, 18, p);
    }

    /// <summary>Левый край панели с артом — он же правая граница текстовой колонки.</summary>
    private const float ArtLeft = 560;

    /// <summary>Ширина левой колонки: до панели с артом, с зазором.</summary>
    private const float ContentWidth = ArtLeft - Pad - 28;

    private void Header(SKCanvas canvas, SKPaint p, string title, string subtitle, string bot, int height)
    {
        p.Color = Text;
        var titleFont = new SKFont(_bold, 38);
        canvas.DrawText(Fit(Clean(title), titleFont, ContentWidth), Pad, 82, titleFont, p);

        p.Color = Muted;
        var subFont = new SKFont(_regular, 21);
        canvas.DrawText(Fit(Clean(subtitle), subFont, ContentWidth), Pad, 112, subFont, p);

        // Подпись бота внизу: сверху справа она ложилась на панель с артом
        canvas.DrawText($"@{bot}", Width - Pad, height - 20,
            SKTextAlign.Right, new SKFont(_regular, 17), p);
    }

    /// <summary>Главное число карточки с подписью под ним.</summary>
    private void Hero(SKCanvas canvas, SKPaint p, string value, string label)
    {
        var font = new SKFont(_bold, 74);
        p.Color = Gold;
        canvas.DrawText(Fit(value, font, ContentWidth), Pad, 196, font, p);
        p.Color = Muted;
        var labelFont = new SKFont(_regular, 19);
        canvas.DrawText(Fit(label, labelFont, ContentWidth), Pad, 226, labelFont, p);
    }

    /// <summary>
    /// Три плитки в ряд по общей сетке. Ширина считается от полей, а не задаётся
    /// числом: в первой версии плитки не доставали до правого края и композиция
    /// выглядела съехавшей.
    /// </summary>
    private void Tiles(SKCanvas canvas, SKPaint p, params (string Value, string Label)[] tiles)
    {
        // Ряд стоит ПОД панелью с артом (она заканчивается на 226), поэтому плитки
        // всегда на чистом фоне и читаются независимо от того, что нарисовано справа.
        // Низ ряда — 316 из 360: подписи не упираются в край, и превью Telegram их не режет.
        const float gap = 14, top = 246, h = 70;
        var w = (Width - 2 * Pad - gap * (tiles.Length - 1)) / tiles.Length;

        for (var i = 0; i < tiles.Length; i++)
        {
            var x = Pad + i * (w + gap);
            var rect = new SKRect(x, top, x + w, top + h);

            p.Color = Deep.WithAlpha(170);
            canvas.DrawRoundRect(rect, 16, 16, p);

            p.Color = Text;
            var vf = new SKFont(_bold, 28);
            canvas.DrawText(Fit(tiles[i].Value, vf, w - 32), x + 18, top + 36, vf, p);
            p.Color = Muted;
            var lf = new SKFont(_regular, 16);
            canvas.DrawText(Fit(tiles[i].Label, lf, w - 32), x + 18, top + 58, lf, p);
        }
    }

    /// <summary>
    /// Сколько ждём одну иконку. Было пять секунд — на восьми картах колоды это
    /// складывалось в сорок, и картинку успевал бросить недокачанной уже Telegram.
    /// Лучше нарисовать заглушку, чем отдать половину JPEG.
    /// </summary>
    private static readonly TimeSpan IconTimeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Иконка карты с CDN игры. Держим в памяти сутки: картинки неизменны, а без кэша
    /// каждая карточка колоды означала бы восемь загрузок по сети.
    /// null — не скачалась, вызывающий рисует заглушку.
    /// </summary>
    private SKImage? Icon(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (cache.TryGetValue<SKImage?>(IconKey(url), out var cached)) return cached;

        Warm([url]);
        return cache.TryGetValue<SKImage?>(IconKey(url), out var loaded) ? loaded : null;
    }

    /// <summary>
    /// Заранее и параллельно скачивает то, чего ещё нет в кэше. Сеть здесь — самая
    /// долгая часть отрисовки, и единственный способ её сократить — не ходить по
    /// адресам по очереди.
    /// </summary>
    private void Warm(IEnumerable<string?> urls)
    {
        var missing = urls
            .Where(u => !string.IsNullOrEmpty(u) && !cache.TryGetValue(IconKey(u!), out _))
            .Distinct()
            .ToList();
        if (missing.Count == 0) return;

        Task.WhenAll(missing.Select(u => FetchIconAsync(u!))).GetAwaiter().GetResult();
    }

    private async Task FetchIconAsync(string url)
    {
        SKImage? img = null;
        try
        {
            using var client = http.CreateClient();
            client.Timeout = IconTimeout;
            img = SKImage.FromEncodedData(await client.GetByteArrayAsync(url));
        }
        catch
        {
            // Не скачалась — запомним это ненадолго, чтобы не долбить CDN на каждый показ
        }

        cache.Set(IconKey(url), img, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = img is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(24),
        });
    }

    private static string IconKey(string url) => $"cardicon:{url}";

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
