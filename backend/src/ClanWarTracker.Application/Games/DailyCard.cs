using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Games;

/// <summary>
/// «Карта дня»: у всех игроков в один день одна и та же загадка.
///
/// Общая карта здесь не деталь, а весь смысл. Разные загадки у разных людей не дают
/// повода поговорить, а одна на всех превращает игру в разговор в клановом чате —
/// именно это, а не само угадывание, заставляет открывать бота каждый день.
///
/// Никакого хранилища: и карта, и варианты ответа выводятся из номера дня. Значит
/// их можно посчитать где угодно и когда угодно, а перезапуск сервиса ничего не рвёт.
/// </summary>
public static class DailyCard
{
    /// <summary>
    /// Отсчёт номеров загадок. Дата в прошлом и не двигается: сдвиг эпохи перетасовал
    /// бы все будущие загадки и сбил бы номера, которые люди уже видели в чате.
    /// </summary>
    private static readonly DateOnly Epoch = new(2026, 1, 1);

    /// <summary>Сколько вариантов ответа показываем.</summary>
    public const int OptionCount = 4;

    /// <summary>Номер сегодняшней загадки — он же её публичное имя («Карта дня #47»).</summary>
    public static int DayNumber(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow).DayNumber - Epoch.DayNumber + 1;

    /// <summary>
    /// Карта дня. Колода тасуется целиком и раздаётся по кругу, а не выбирается
    /// случайно каждый день: случайный выбор повторил бы карту через неделю, и игра
    /// сразу стала бы выглядеть небрежной. Так каждая карта выпадает один раз за круг,
    /// а на новом круге порядок другой.
    /// </summary>
    public static CrCatalogCard? Pick(IReadOnlyList<CrCatalogCard> catalog, int day)
    {
        if (catalog.Count == 0) return null;

        var deck = Ordered(catalog);
        var cycle = (day - 1) / deck.Count;
        var index = (day - 1) % deck.Count;

        Shuffle(deck, Seed($"cycle:{cycle}"));
        return deck[index];
    }

    /// <summary>
    /// Варианты ответа: правильный плюс три чужих, в порядке, одинаковом для всех.
    /// Неверные берём из карт того же диапазона стоимости — вариант «Мега-рыцарь или
    /// Зеркало» отгадывается без картинки, по одному только здравому смыслу.
    /// </summary>
    public static List<CrCatalogCard> Options(IReadOnlyList<CrCatalogCard> catalog, CrCatalogCard answer, int day)
    {
        var pool = Ordered(catalog).Where(c => c.Id != answer.Id).ToList();
        if (pool.Count == 0) return [answer];

        var near = pool.Where(c => Math.Abs(c.ElixirCost - answer.ElixirCost) <= 1).ToList();
        // У совсем редкой стоимости соседей может не набраться — тогда берём всех
        var source = near.Count >= OptionCount - 1 ? near : pool;

        Shuffle(source, Seed($"options:{day}"));
        var chosen = source.Take(OptionCount - 1).ToList();
        chosen.Add(answer);

        Shuffle(chosen, Seed($"order:{day}"));
        return chosen;
    }

    /// <summary>
    /// Стабильный порядок справочника. API отдаёт карты как ему вздумается, а загадка
    /// обязана совпадать у всех и не меняться после перезапуска — поэтому сортируем
    /// по неизменному id из игры.
    /// </summary>
    private static List<CrCatalogCard> Ordered(IReadOnlyList<CrCatalogCard> catalog) =>
        catalog.OrderBy(c => c.Id).ToList();

    /// <summary>
    /// Тасовка Фишера — Йетса на детерминированном генераторе. Random.Shared здесь
    /// не годится: у разных процессов вышли бы разные загадки.
    /// </summary>
    private static void Shuffle<T>(List<T> items, int seed)
    {
        var rnd = new Random(seed);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Устойчивый хеш строки. string.GetHashCode рандомизирован от запуска к запуску,
    /// поэтому после перезапуска сервиса загадка дня менялась бы прямо посреди дня.
    /// </summary>
    public static int Seed(string key)
    {
        var hash = 17;
        foreach (var ch in key) hash = unchecked(hash * 31 + ch);
        return hash & 0x7FFFFFFF;
    }
}
