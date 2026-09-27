namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Колода топа за день: сколько раз сыграна и с каким итогом.
///
/// Считается из боёв топ-игроков, а не из колод в их профилях. Профиль говорит,
/// чем игрок играет сейчас; бои говорят, чем выигрывают, - а мета это второе.
/// </summary>
public class MetaDeckDay
{
    public int Id { get; set; }

    /// <summary>День снимка, «yyyy-MM-dd» по UTC.</summary>
    public required string DayUtc { get; set; }

    /// <summary>Ключи восьми карт по возрастанию через запятую, см. <see cref="MetaCard.Key"/>.</summary>
    public required string DeckKey { get; set; }

    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
}

/// <summary>
/// Карта против карты за день: сколько раз встретились и сколько выиграла первая.
///
/// Контры считаются на уровне карт, а не колод. Колод в топе за день тысячи, и
/// пары «колода против колоды» почти все встречаются по разу - статистики из них
/// не выходит. Пар «карта против карты» не больше нескольких десятков тысяч, у
/// каждой сотни встреч, и из восьми карт любой колоды, даже редкой, складывается
/// её картина против любой карты соперника.
/// </summary>
public class MetaMatchupDay
{
    public int Id { get; set; }
    public required string DayUtc { get; set; }
    public int CardKey { get; set; }
    public int OppCardKey { get; set; }
    public int Games { get; set; }
    public int Wins { get; set; }
}

public static class MetaCard
{
    /// <summary>
    /// Ключ карты в мете: id, а у эволюции - тот же id со знаком минус.
    ///
    /// Эволюция меняет карту настолько, что «Рыцарь» и «эволюция Рыцаря» - разные
    /// карты и для колоды, и для контр. Отдельный столбец под это не нужен: id карт
    /// положительные, и минус однозначно читается как «эволюция».
    /// </summary>
    public static int Key(CrDeckCard card) => card.EvolutionLevel > 0 ? -card.Id : card.Id;

    public static string DeckKey(IEnumerable<int> keys) => string.Join(',', keys.OrderBy(k => k));

    public static int[] ParseDeckKey(string deckKey) =>
        deckKey.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var k) ? k : 0)
            .Where(k => k != 0)
            .ToArray();
}
