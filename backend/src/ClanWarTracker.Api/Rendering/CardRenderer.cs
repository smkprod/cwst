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

/// <summary>Поздравление с идеальным днём: 900 медалей за четыре боя.</summary>
public record PerfectDayCardModel(
    string PlayerName, string ClanName, int Fame, string BotName, string? ArtUrl);

/// <summary>
/// Открытая награда спонсора. Подпись приходит готовой: что считать наградой и
/// на каком языке её называть — не дело рисовалки. Значка в модели нет: рисуется
/// кубок из ассетов, потому что эмодзи-шрифта в проекте нет.
/// </summary>
public record AchievementCardModel(
    string PlayerName, string ClanName, string Title, string Subtitle, string BotName);

/// <summary>
/// Итог турнира: кто победил, с каким счётом и над кем.
/// </summary>
/// <param name="Roster">Состав команды строкой; пусто в одиночном турнире.</param>
public record ChampionCardModel(
    string TeamName, string Roster, string TournamentName, string Score,
    string RunnerUp, int Teams, string BotName);

/// <summary>
/// Рисует карточки для inline-режима и для чата.
///
/// Прошлые версии выглядели как тёмный прямоугольник с цифрами, и дело было не в
/// библиотеке: Skia рисует ровно то, что ей скажут. Дело было в шрифте и в отсутствии
/// системы. DejaVu — служебный шрифт диалоговых окон, им нельзя сделать «как в игре»;
/// его заменили Russo One (заголовки и крупные числа — он тяжёлый и геометричный) и
/// Rubik (подписи). Оба с полной кириллицей и украинскими буквами.
///
/// Геометрия отсюда сначала собиралась макетом в браузере и правилась по скриншотам,
/// и только потом переносилась сюда числами: вслепую расставлять прямоугольники —
/// ровно тот способ, которым получались прошлые версии.
/// </summary>
public class CardRenderer(IWebHostEnvironment env, IHttpClientFactory http, IMemoryCache cache)
{
    public const int Width = 800;

    /// <summary>Высота карточек со статистикой.</summary>
    public const int StatHeight = 360;

    /// <summary>Колода выше: восемь артов в два ряда просто не влезают ниже.</summary>
    public const int DeckHeight = 540;

    private const float Pad = 44;
    private const float TopPad = 34;

    // Палитра. Фон — не плоская заливка, а три слоя: диагональный градиент и два
    // цветных пятна по углам. Именно они дают глубину, которой не было раньше.
    private static readonly SKColor BgTop = SKColor.Parse("#2a1d5e");
    private static readonly SKColor BgMid = SKColor.Parse("#141232");
    private static readonly SKColor BgBottom = SKColor.Parse("#0a0a18");
    private static readonly SKColor GlowPurple = new(124, 77, 255);
    private static readonly SKColor GlowBlue = new(0, 168, 255);

    private static readonly SKColor Ink = SKColor.Parse("#ffffff");
    private static readonly SKColor Muted = SKColor.Parse("#9aa3c7");
    private static readonly SKColor Faint = SKColor.Parse("#7c85ab");
    private static readonly SKColor Gold = SKColor.Parse("#ffc83d");

    private readonly SKTypeface _display = Load(env, "RussoOne-Regular.ttf");
    private readonly SKTypeface _regular = Load(env, "Rubik-Regular.ttf");
    private readonly SKTypeface _medium = Load(env, "Rubik-SemiBold.ttf");

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
        Header(canvas, m.PlayerName, m.ClanName, m.BotName, StatHeight);
        Hero(canvas, Num(m.Fame), "медалей за неделю");
        Tiles(canvas,
            ($"#{m.Rank}", $"из {m.ClanSize} в клане"),
            ($"{m.DecksToday}/4", "колод сегодня"),
            ($"{m.RacePosition}/{m.RaceClans}", "место в гонке"));
    });

    public byte[] RenderProfile(ProfileCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        Header(canvas, m.PlayerName, m.ClanName ?? "без клана", m.BotName, StatHeight);
        Hero(canvas, Num(m.Trophies), $"кубков · рекорд {Num(m.BestTrophies)}");
        Tiles(canvas,
            (m.Level.ToString(), "уровень"),
            (Num(m.WarDayWins), "побед в КВ"),
            (Num(m.ThreeCrownWins), "три короны"));
    });

    public byte[] RenderClan(ClanCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        Header(canvas, m.ClanName, m.ClanTag, m.BotName, StatHeight);
        Hero(canvas, Num(m.WarTrophies), "трофеев Клановых войн");
        Tiles(canvas,
            ($"{m.Members}/50", "участников"),
            (Num(m.ClanScore), "очки клана"),
            (Num(m.RequiredTrophies), "порог входа"));
    });

    /// <summary>
    /// Идеальный день: 900 медалей. Уходит картинкой в клановый чат, поэтому число
    /// здесь главный и почти единственный герой — подробности в подписи к фото.
    /// </summary>
    public byte[] RenderPerfectDay(PerfectDayCardModel m) => Draw(m.ArtUrl, StatHeight, canvas =>
    {
        Header(canvas, m.PlayerName, m.ClanName, m.BotName, StatHeight);

        using var p = new SKPaint { IsAntialias = true };

        // Плашка-заголовок над числом: без неё карточка неотличима от обычной
        // статистики, а это не статистика, а поздравление.
        var badge = new SKRect(Pad, 110, Pad + 236, 144);
        p.Color = Gold.WithAlpha(38);
        canvas.DrawRoundRect(badge, 17, 17, p);
        p.Color = Gold;
        canvas.DrawText("ИДЕАЛЬНЫЙ ДЕНЬ", Pad + 18, badge.MidY + 6, new SKFont(_medium, 15), p);

        // 196 — базовая линия числа: его верх ложится сразу под плашку, а подпись
        // на 226 не достаёт до ряда плиток, который начинается на 240.
        HeroAt(canvas, Num(m.Fame), "из 900 возможных", 196, 226);
        Tiles(canvas,
            ("4/4", "колоды сыграны"),
            ("0", "поражений"),
            ("225", "медалей за бой"));
    });

    /// <summary>Колода: восемь карт настоящими артами — ради этого картинки и затевались.</summary>
    /// <summary>
    /// Карточка открытой награды — привилегия спонсора.
    ///
    /// Рисуется на своей картинке вместо обычного градиента: в том и смысл, что в
    /// общем чате она видна издалека и отличается от всех остальных сообщений
    /// бота. Поверх фона кладётся затемнение — без него белый текст тонет в
    /// закате, и проверять это приходится глазами, а не рассуждением.
    /// </summary>
    public byte[] RenderAchievement(AchievementCardModel m)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, StatHeight));
        var canvas = surface.Canvas;

        if (_achievementBg is not null)
            canvas.DrawImage(_achievementBg, new SKRect(0, 0, Width, StatHeight));
        else
            Background(canvas, StatHeight);

        using (var veil = new SKPaint { IsAntialias = true })
        {
            veil.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, StatHeight),
                [new SKColor(8, 8, 20, 190), new SKColor(8, 8, 20, 120), new SKColor(8, 8, 20, 225)],
                [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Width, StatHeight), veil);
        }

        Header(canvas, m.PlayerName, m.ClanName, m.BotName, StatHeight);

        using var paint = new SKPaint { IsAntialias = true };

        // Кубок из ассетов, а не эмодзи.
        //
        // Эмодзи-шрифта в проекте нет намеренно — цветной весит десяток мегабайт, —
        // и Clean() по этой же причине вырезает эмодзи из имён. Нарисуй я тут значок
        // текстом, вышел бы пустой квадрат: ровно та поломка, от которой Clean и
        // защищает. Кубок лежит в Assets, используется карточкой чемпиона и рисуется
        // без шрифта вообще.
        if (_trophy is not null) Art(canvas, paint, _trophy, Width / 2f, 104, 96, centered: true);

        var titleFont = new SKFont(_display, 40);
        paint.Color = Gold;
        canvas.DrawText(m.Title, Width / 2f, 252, SKTextAlign.Center, titleFont, paint);

        var subFont = new SKFont(_regular, 24);
        paint.Color = Muted;
        canvas.DrawText(m.Subtitle, Width / 2f, 292, SKTextAlign.Center, subFont, paint);

        using (var line = new SKPaint { IsAntialias = true })
        {
            line.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, 0),
                [Gold, Gold.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, StatHeight - 5, Width, StatHeight), line);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    public byte[] RenderDeck(DeckCardModel m)
    {
        // Восемь иконок сразу, а не по одной внутри отрисовки: последовательно они
        // качались до сорока секунд на холодном кэше, и Telegram успевал бросить
        // загрузку на середине — в чате появлялась половина картинки и серая заливка.
        Warm(m.Cards.Select(c => c.IconUrl));

        return Draw(null, DeckHeight, canvas =>
        {
            Header(canvas, m.Title, m.Subtitle, m.BotName, DeckHeight);

            const int cols = 4, cw = 132, gapX = 20, gapY = 14;
            var ch = (int)(cw * 1.2f);
            var startX = (Width - (cols * cw + (cols - 1) * gapX)) / 2f;
            const float top = 146;

            using var p = new SKPaint { IsAntialias = true };
            for (var i = 0; i < Math.Min(8, m.Cards.Count); i++)
            {
                var c = m.Cards[i];
                var x = startX + i % cols * (cw + gapX);
                var y = top + i / cols * (ch + gapY + 8);
                DeckSlot(canvas, p, c, x, y, cw, ch);
            }

            p.Color = Muted;
            canvas.DrawText($"средний уровень {m.AvgLevel}", Pad, DeckHeight - 26,
                new SKFont(_regular, 19), p);
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
            p.Color = Ink.WithAlpha(18);
            p.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(new SKRect(x, y, x + cw, y + ch), 12, 12, p);
        }

        // Уровень — плашкой поверх нижнего края арта, как значок в игре
        var badge = new SKRect(x + cw / 2f - 27, y + ch - 20, x + cw / 2f + 27, y + ch + 14);
        p.Color = BgBottom.WithAlpha(235);
        p.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(badge, 11, 11, p);
        p.Color = c.Maxed ? Gold : Ink;
        canvas.DrawText(c.Level.ToString(), badge.MidX, badge.MidY + 8,
            SKTextAlign.Center, new SKFont(_medium, 22), p);
    }

    /// <summary>
    /// Общий каркас: фон, арт справа, содержимое, золотая полоса и кодирование.
    /// </summary>
    private byte[] Draw(string? artUrl, int height, Action<SKCanvas> body)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, height));
        var canvas = surface.Canvas;

        Background(canvas, height);
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
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Фон в три слоя. Один градиент читается как заливка; два цветных пятна по
    /// противоположным углам дают объём, а сетка — фактуру, без которой большие
    /// пустые области выглядят незаконченными.
    /// </summary>
    private static void Background(SKCanvas canvas, int height)
    {
        var full = new SKRect(0, 0, Width, height);

        using (var bg = new SKPaint { IsAntialias = true })
        {
            bg.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, height),
                [BgTop, BgMid, BgBottom], [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(full, bg);
        }

        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateRadialGradient(
                new SKPoint(Width * 0.08f, -height * 0.10f), Width * 0.78f,
                [GlowPurple.WithAlpha(140), GlowPurple.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(full, glow);
        }

        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateRadialGradient(
                new SKPoint(Width * 0.96f, height * 1.10f), Width * 0.65f,
                [GlowBlue.WithAlpha(72), GlowBlue.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(full, glow);
        }

        // Сетка гаснет к правому нижнему углу: ровная по всему полю читалась бы как
        // разлинованный лист, а не как фактура.
        using (var grid = new SKPaint { IsAntialias = false, StrokeWidth = 1 })
        {
            grid.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width * 0.7f, height * 0.7f),
                [Ink.WithAlpha(14), Ink.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            for (var x = 40; x < Width; x += 40)
                canvas.DrawLine(x, 0, x, height, grid);
            for (var y = 40; y < height; y += 40)
                canvas.DrawLine(0, y, Width, y, grid);
        }
    }

    /// <summary>Левый край панели с артом — он же правая граница текстовой колонки.</summary>
    private const float ArtLeft = Width - Pad - ArtSize;
    private const float ArtSize = 196;

    /// <summary>
    /// Арт карты отдельной панелью у правого края: своя рамка, лёгкая подложка и
    /// блик по верхнему краю. Растянутый фоном арт кричал громче цифр и обрывался
    /// на границе прямой линией — панель снимает обе проблемы.
    /// </summary>
    private static void ArtPanel(SKCanvas canvas, SKImage? art)
    {
        var panel = new SKRect(ArtLeft, TopPad, ArtLeft + ArtSize, TopPad + ArtSize);

        using var p = new SKPaint { IsAntialias = true };
        p.Shader = SKShader.CreateLinearGradient(
            new SKPoint(panel.Left, panel.Top), new SKPoint(panel.Right, panel.Bottom),
            [Ink.WithAlpha(33), Ink.WithAlpha(8)], [0f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(panel, 22, 22, p);
        p.Shader = null;

        if (art is not null)
        {
            // Вписываем целиком (contain), а не заполняем: обрезка съела бы половину
            // персонажа, а панель маленькая — потеря была бы заметной.
            const float inset = 18;
            var box = new SKRect(panel.Left + inset, panel.Top + inset,
                panel.Right - inset, panel.Bottom - inset);
            var scale = Math.Min(box.Width / art.Width, box.Height / art.Height);
            var w = art.Width * scale;
            var h = art.Height * scale;
            canvas.DrawImage(art, new SKRect(
                box.MidX - w / 2, box.MidY - h / 2, box.MidX + w / 2, box.MidY + h / 2),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), p);
        }

        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = 1.5f;
        p.Color = Ink.WithAlpha(42);
        canvas.DrawRoundRect(panel, 22, 22, p);
    }

    /// <summary>Ширина левой колонки: до панели с артом, с зазором.</summary>
    private const float ContentWidth = ArtLeft - Pad - 30;

    private void Header(SKCanvas canvas, string title, string subtitle, string bot, int height)
    {
        using var p = new SKPaint { IsAntialias = true };

        p.Color = Ink;
        var titleFont = new SKFont(_display, 40);
        canvas.DrawText(Fit(Clean(title), titleFont, ContentWidth), Pad, 66, titleFont, p);

        p.Color = Muted;
        var subFont = new SKFont(_regular, 19);
        canvas.DrawText(Fit(Clean(subtitle), subFont, ContentWidth), Pad, 100, subFont, p);

        p.Color = Faint;
        canvas.DrawText($"@{bot}", Width - Pad, height - 18,
            SKTextAlign.Right, new SKFont(_regular, 15), p);
    }

    /// <summary>Главное число карточки с подписью под ним.</summary>
    private void Hero(SKCanvas canvas, string value, string label) =>
        HeroAt(canvas, value, label, 178, 208);

    private void HeroAt(SKCanvas canvas, string value, string label, float valueBaseline, float labelBaseline)
    {
        using var p = new SKPaint { IsAntialias = true };
        var font = new SKFont(_display, 72);
        var text = Fit(value, font, ContentWidth);

        // Свечение под числом: то же самое слово, размытое и полупрозрачное. Без него
        // золотой текст на тёмном фоне выглядит наклейкой, а не светящейся цифрой.
        p.Color = Gold.WithAlpha(110);
        p.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18);
        canvas.DrawText(text, Pad, valueBaseline, font, p);
        p.MaskFilter = null;

        p.Color = Gold;
        canvas.DrawText(text, Pad, valueBaseline, font, p);

        p.Color = Muted;
        var labelFont = new SKFont(_regular, 18);
        canvas.DrawText(Fit(label, labelFont, ContentWidth), Pad, labelBaseline, labelFont, p);
    }

    /// <summary>
    /// Три плитки в ряд. Каждая — не плоский прямоугольник, а панель с вертикальным
    /// градиентом, рамкой и бликом по верхнему краю: так они читаются как объекты,
    /// лежащие на фоне, а не как дырки в нём.
    /// </summary>
    private void Tiles(SKCanvas canvas, params (string Value, string Label)[] tiles)
    {
        const float gap = 14, top = 240, h = 76;
        var w = (Width - 2 * Pad - gap * (tiles.Length - 1)) / tiles.Length;

        using var p = new SKPaint { IsAntialias = true };
        for (var i = 0; i < tiles.Length; i++)
        {
            var x = Pad + i * (w + gap);
            var rect = new SKRect(x, top, x + w, top + h);

            p.Style = SKPaintStyle.Fill;
            p.Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Left, rect.Bottom),
                [Ink.WithAlpha(24), Ink.WithAlpha(9)], [0f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(rect, 16, 16, p);
            p.Shader = null;

            p.Style = SKPaintStyle.Stroke;
            p.StrokeWidth = 1;
            p.Color = Ink.WithAlpha(26);
            canvas.DrawRoundRect(rect, 16, 16, p);

            // Блик по верхнему краю — тот самый inset-highlight из макета
            p.Color = Ink.WithAlpha(36);
            canvas.DrawLine(rect.Left + 16, rect.Top + 1, rect.Right - 16, rect.Top + 1, p);

            p.Style = SKPaintStyle.Fill;
            p.Color = Ink;
            var vf = new SKFont(_medium, 27);
            canvas.DrawText(Fit(tiles[i].Value, vf, w - 32), x + 16, top + 34, vf, p);

            p.Color = Muted;
            var lf = new SKFont(_regular, 14);
            canvas.DrawText(Fit(tiles[i].Label, lf, w - 32), x + 16, top + 56, lf, p);
        }
    }

    /* ---------- «Карта дня»: фрагмент арта ---------- */

    /// <summary>Сторона картинки-загадки. Квадрат: фрагмент вырезается квадратом.</summary>

    /* --- Карточка чемпиона турнира --- */

    public const int ChampionHeight = 560;

    /// <summary>Белое золото: эта карточка намеренно выбивается из тёмной остальной серии.</summary>
    private static readonly SKColor ChampInk = SKColor.Parse("#2e2611");
    private static readonly SKColor ChampMuted = SKColor.Parse("#8a7440");
    private static readonly SKColor ChampFaint = SKColor.Parse("#9c8a55");
    private static readonly SKColor ChampDeep = SKColor.Parse("#6b5a2a");
    private static readonly SKColor GoldDark = SKColor.Parse("#b8860b");

    private readonly SKImage? _kingBlue = LoadImage(env, "KingBlue.png");
    private readonly SKImage? _kingRed = LoadImage(env, "KingRed.png");
    private readonly SKImage? _trophy = LoadImage(env, "Trophy.png");

    /// <summary>
    /// Фон карточки наград спонсора. Лежит готовым под размер карточки, поэтому
    /// на отрисовке его не масштабируем: 800×360 и есть та рамка, в которую он
    /// уже обрезан.
    /// </summary>
    private readonly SKImage? _achievementBg = LoadImage(env, "AchievementBg.jpg");

    /// <summary>
    /// Картинка из Assets. В отличие от шрифта её отсутствие не фатально: карточка
    /// без фигур соберётся, просто будет скучнее — падать из-за этого при старте
    /// всего сервиса незачем.
    /// </summary>
    private static SKImage? LoadImage(IWebHostEnvironment env, string file)
    {
        try
        {
            // Перегрузки с путём к файлу у FromEncodedData нет — только данные.
            var path = Path.Combine(env.ContentRootPath, "Assets", file);
            return SKImage.FromEncodedData(File.ReadAllBytes(path));
        }
        catch { return null; }
    }

    /// <summary>
    /// Итог турнира для чата: кубок между двумя королями, имя чемпиона на плашке.
    ///
    /// Рисовать королей примитивами я пробовал трижды — выходили то иконки
    /// пользователя, то фигуры в капюшонах. Настоящие фигуры лежат в Assets и
    /// берутся оттуда: ни сети, ни зависимости от доступности CDN.
    ///
    /// Геометрия собиралась макетом в браузере с этими же картинками и правилась по
    /// скриншотам: в первой версии короли были крупнее и перекрывали имя команды,
    /// во второй плашка резала им лица. Здесь они стоят по бокам от кубка, а текст
    /// целиком ниже — пересекаться нечему.
    /// </summary>
    public byte[] RenderChampion(ChampionCardModel m)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, ChampionHeight));
        var canvas = surface.Canvas;
        using var p = new SKPaint { IsAntialias = true };

        ChampionBackground(canvas, p);

        // Фигуры и кубок. Каждая может не загрузиться — композиция это переживает.
        if (_trophy is not null) Art(canvas, p, _trophy, Width / 2f, 46, 206, centered: true);
        if (_kingBlue is not null) Art(canvas, p, _kingBlue, 4, 62, 226, centered: false);
        if (_kingRed is not null) Art(canvas, p, _kingRed, Width - 4, 62, 226, centered: false, mirror: true);

        // Надзаголовок вразрядку: межбуквенного интервала в Skia нет, рисуем посимвольно.
        p.Shader = null;
        p.Color = GoldDark;
        Spaced(canvas, "ЧЕМПИОН ТУРНИРА", Width / 2f, 48, new SKFont(_medium, 15), 7, p);

        // Плашка под текстом: короли и кубок сверху, текст снизу — без подложки
        // светлые буквы на светлом фоне читались бы хуже.
        p.Color = SKColor.Parse("#fffdf6").WithAlpha(240);
        canvas.DrawRoundRect(new SKRect(70, 300, 730, 496), 20, 20, p);
        p.Color = SKColor.Parse("#d4a017");
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(new SKRect(70, 300, 730, 496), 20, 20, p);
        p.Style = SKPaintStyle.Fill;

        // Имя команды — главный герой карточки, поэтому самый крупный кегль,
        // но с ужиманием: название разрешено до 64 символов.
        var nameFont = new SKFont(_display, 42);
        p.Color = ChampInk;
        canvas.DrawText(Fit(Clean(m.TeamName), nameFont, 600), Width / 2f, 358,
            SKTextAlign.Center, nameFont, p);

        if (m.Roster.Length > 0)
        {
            var rosterFont = new SKFont(_regular, 15);
            p.Color = ChampMuted;
            canvas.DrawText(Fit(Clean(m.Roster), rosterFont, 600), Width / 2f, 386,
                SKTextAlign.Center, rosterFont, p);
        }

        // DrawLine рисует обводкой: с Fill-стилем от линии остался бы волосок.
        p.Color = GoldDark.WithAlpha(102);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = 1;
        canvas.DrawLine(300, 406, 500, 406, p);
        p.Style = SKPaintStyle.Fill;

        var tourFont = new SKFont(_medium, 16);
        p.Color = ChampDeep;
        canvas.DrawText(Fit(Clean(m.TournamentName), tourFont, 600), Width / 2f, 432,
            SKTextAlign.Center, tourFont, p);

        var footFont = new SKFont(_regular, 14);
        p.Color = ChampFaint;
        var final = m.RunnerUp.Length > 0
            ? $"Финал {m.Score} против «{Clean(m.RunnerUp)}»"
            : $"Финал {m.Score}";
        canvas.DrawText(Fit(final, footFont, 600), Width / 2f, 456, SKTextAlign.Center, footFont, p);

        if (m.Teams > 0)
            canvas.DrawText($"{m.Teams} команд", Width / 2f, 478, SKTextAlign.Center, footFont, p);

        p.Color = SKColor.Parse("#c2b28a");
        canvas.DrawText($"@{m.BotName}", Width - 34, 540, SKTextAlign.Right, new SKFont(_regular, 12), p);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        return data.ToArray();
    }

    /// <summary>
    /// Рисует картинку заданной высоты, сохраняя пропорции.
    /// </summary>
    /// <param name="x">Левый край; при centered — центр, при mirror — правый край.</param>
    private static void Art(SKCanvas canvas, SKPaint p, SKImage img,
        float x, float y, float height, bool centered, bool mirror = false)
    {
        var width = img.Width * (height / img.Height);
        var left = centered ? x - width / 2 : mirror ? x - width : x;
        var src = new SKRect(0, 0, img.Width, img.Height);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        if (!mirror)
        {
            canvas.DrawImage(img, src, new SKRect(left, y, left + width, y + height), sampling, p);
            return;
        }

        // Правый король развёрнут лицом внутрь: оба смотрят на кубок, а не в одну сторону.
        canvas.Save();
        canvas.Translate(left + width, y);
        canvas.Scale(-1, 1);
        canvas.DrawImage(img, src, new SKRect(0, 0, width, height), sampling, p);
        canvas.Restore();
    }

    /// <summary>Тёплый белый фон, золотое свечение вокруг кубка и двойная рамка.</summary>
    private static void ChampionBackground(SKCanvas canvas, SKPaint p)
    {
        var full = new SKRect(0, 0, Width, ChampionHeight);

        p.Shader = SKShader.CreateRadialGradient(
            new SKPoint(Width / 2f, 78), Width * 1.05f,
            [SKColor.Parse("#ffffff"), SKColor.Parse("#fffaf0"), SKColor.Parse("#f4e6c6")],
            [0f, 0.40f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawRect(full, p);

        // Свечение вокруг кубка: без него центр карточки проваливается.
        p.Shader = SKShader.CreateRadialGradient(
            new SKPoint(Width / 2f, 150), 290,
            [SKColor.Parse("#ffd970").WithAlpha(140), SKColor.Parse("#ffd970").WithAlpha(0)],
            [0f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawRect(full, p);

        p.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 16), new SKPoint(0, ChampionHeight - 16),
            [SKColor.Parse("#fbe18f"), SKColor.Parse("#e0ae21"),
             SKColor.Parse("#b8860b"), SKColor.Parse("#8a6508")],
            [0f, 0.38f, 0.72f, 1f], SKShaderTileMode.Clamp);
        p.Style = SKPaintStyle.Stroke;
        p.StrokeWidth = 4;
        canvas.DrawRoundRect(new SKRect(16, 16, Width - 16, ChampionHeight - 16), 22, 22, p);

        p.Shader = null;
        p.Color = GoldDark.WithAlpha(128);
        p.StrokeWidth = 1.2f;
        canvas.DrawRoundRect(new SKRect(27, 27, Width - 27, ChampionHeight - 27), 15, 15, p);

        p.Style = SKPaintStyle.Fill;
        p.StrokeWidth = 0;
    }

    /// <summary>
    /// Текст вразрядку по центру. В Skia межбуквенного интервала нет, поэтому
    /// считаем ширину сами и рисуем посимвольно от левого края.
    /// </summary>
    private static void Spaced(SKCanvas canvas, string text, float centerX, float y,
        SKFont font, float spacing, SKPaint p)
    {
        var total = text.Sum(c => font.MeasureText(c.ToString())) + spacing * (text.Length - 1);
        var cursor = centerX - total / 2;

        foreach (var c in text)
        {
            var s = c.ToString();
            canvas.DrawText(s, cursor, y, font, p);
            cursor += font.MeasureText(s) + spacing;
        }
    }

    public const int PuzzleSize = 420;

    /// <summary>
    /// Какую долю арта показываем на 1-й, 2-й и 3-й попытке. Начинаем не с самого
    /// мелкого куска: по 10% арта не угадывается ничего, и игра из загадки
    /// превращается в лотерею.
    /// </summary>
    private static readonly float[] PuzzleZoom = [0.30f, 0.48f, 0.72f];

    public byte[]? RenderPuzzle(string artUrl, int level, int seed)
    {
        var art = Icon(artUrl);
        if (art is null) return null;

        using var surface = SKSurface.Create(new SKImageInfo(PuzzleSize, PuzzleSize));
        Background(surface.Canvas, PuzzleSize);
        Fragment(surface.Canvas, art, new SKRect(0, 0, PuzzleSize, PuzzleSize), level, seed);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Лист предпросмотра: несколько карт, у каждой все три попытки подряд, с ответом
    /// под строкой. Нужен, чтобы одним взглядом понять, угадываемы ли фрагменты
    /// вообще — по одной картинке этого не видно, а перебирать их по ссылкам мучительно.
    /// </summary>
    public byte[] RenderPuzzleSheet(IReadOnlyList<(string Name, string ArtUrl)> cards, int seed)
    {
        const int cell = 200, pad = 10, caption = 22;
        var cols = PuzzleZoom.Length;
        var rows = cards.Count;
        var w = pad + cols * (cell + pad);
        var h = pad + rows * (cell + caption + pad);

        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        Background(canvas, h);

        using var p = new SKPaint { IsAntialias = true };
        for (var row = 0; row < rows; row++)
        {
            var art = Icon(cards[row].ArtUrl);
            var y = pad + row * (cell + caption + pad);

            for (var col = 0; col < cols; col++)
            {
                var x = pad + col * (cell + pad);
                var box = new SKRect(x, y, x + cell, y + cell);

                p.Color = BgBottom.WithAlpha(140);
                canvas.DrawRoundRect(box, 12, 12, p);
                if (art is not null) Fragment(canvas, art, box, col + 1, seed + row);
            }

            p.Color = Muted;
            canvas.DrawText(
                Fit(Clean(cards[row].Name), new SKFont(_regular, 16), w - 2 * pad),
                pad, y + cell + 17, new SKFont(_regular, 16), p);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>Вырезает кусок арта вокруг найденной детали и вписывает его в dest.</summary>
    private static void Fragment(SKCanvas canvas, SKImage art, SKRect dest, int level, int seed)
    {
        var zoom = PuzzleZoom[Math.Clamp(level, 1, PuzzleZoom.Length) - 1];
        var centre = DetailPoint(art, seed);

        var half = Math.Min(art.Width, art.Height) * zoom / 2;
        // Квадрат держим внутри арта, не сжимая: иначе у краёв менялся бы масштаб,
        // и по «раздутости» картинки было бы видно, что кроп упёрся в границу.
        var cx = Math.Clamp(centre.X, half, art.Width - half);
        var cy = Math.Clamp(centre.Y, half, art.Height - half);
        var src = new SKRect(cx - half, cy - half, cx + half, cy + half);

        // Линейная фильтрация: при увеличении втрое соседние пиксели иначе
        // превращаются в крупные квадраты, и угадывать приходится мозаику, а не карту.
        using var p = new SKPaint { IsAntialias = true };
        canvas.DrawImage(art, src, dest,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), p);
    }

    /// <summary>Сетка кандидатов при поиске детали. 8×8 — 36 внутренних клеток.</summary>
    private const int PuzzleGrid = 8;

    /// <summary>Из скольких самых «детальных» клеток выбираем, чтобы кроп не повторялся.</summary>
    private const int PuzzleTopCells = 10;

    /// <summary>
    /// Точка, вокруг которой резать: самое насыщенное деталями место арта.
    ///
    /// Случайная точка не годится — половина арта карты это ровная заливка брони
    /// или прозрачный фон, и на такой кусок смотреть бессмысленно. Считаем по
    /// уменьшенной копии, насколько клетка отличается от соседей, и выбираем из
    /// десятка лучших — детерминированно по seed, чтобы у всех была одна загадка.
    /// </summary>
    private static SKPoint DetailPoint(SKImage art, int seed)
    {
        const int px = PuzzleGrid * 4;
        using var small = new SKBitmap(new SKImageInfo(px, px, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(small))
        {
            c.Clear(SKColors.Transparent);
            c.DrawImage(art, new SKRect(0, 0, px, px));
        }

        var luma = new int[px, px];
        for (var y = 0; y < px; y++)
            for (var x = 0; x < px; x++)
            {
                var c = small.GetPixel(x, y);
                // Прозрачное считаем нулём: пустой фон не должен выглядеть деталью
                luma[x, y] = c.Alpha == 0 ? 0 : (c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000;
            }

        var cells = new List<(int Score, int X, int Y)>();
        for (var gy = 1; gy < PuzzleGrid - 1; gy++)
            for (var gx = 1; gx < PuzzleGrid - 1; gx++)
            {
                var score = 0;
                for (var y = gy * 4; y < gy * 4 + 4; y++)
                    for (var x = gx * 4; x < gx * 4 + 4; x++)
                    {
                        if (x + 1 < px) score += Math.Abs(luma[x, y] - luma[x + 1, y]);
                        if (y + 1 < px) score += Math.Abs(luma[x, y] - luma[x, y + 1]);
                    }
                cells.Add((score, gx, gy));
            }

        var top = cells.OrderByDescending(c => c.Score).Take(PuzzleTopCells).ToList();
        var pick = top[(seed & 0x7FFFFFFF) % top.Count];
        return new SKPoint(
            (pick.X + 0.5f) * art.Width / PuzzleGrid,
            (pick.Y + 0.5f) * art.Height / PuzzleGrid);
    }

    /* ---------- Загрузка иконок ---------- */

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
    /// Игроки любят эмодзи в никах, а текстовые шрифты их не содержат — и вместо
    /// дракона в имени появлялся пустой квадрат, выглядевший как поломка. Цветной
    /// эмодзи-шрифт весит десяток мегабайт ради украшения, поэтому такие символы
    /// просто убираем: имя без эмодзи читается нормально, имя с квадратом — нет.
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
