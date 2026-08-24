namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Колода игрока из мирового топа — то, чем реально играют лучшие прямо сейчас.
///
/// Нужна, чтобы перестать зависеть от списка мета-колод, который правится руками:
/// он устаревает каждый сезон, и заметить это можно только по жалобам. Живой топ
/// обновляет сам себя.
/// </summary>
public class CrTopDeck
{
    public required string PlayerName { get; set; }
    public required string PlayerTag { get; set; }
    public int Rank { get; set; }
    public int Trophies { get; set; }
    public string? ClanName { get; set; }

    /// <summary>Восемь карт текущей колоды в том порядке, в каком их отдаёт игра.</summary>
    public List<CrDeckCard> Cards { get; set; } = [];
}

/// <summary>
/// Сколько игроков из выборки топа держат эту карту в колоде.
///
/// Считается отдельно от самих колод намеренно: у двух десятков лучших игроков
/// восемь карт почти никогда не совпадают целиком, и группировка по колодам дала бы
/// список из одиночек. Частота карт при той же выборке остаётся осмысленной.
/// </summary>
public record CrCardUsage(string Name, string IconUrl, int Users);
