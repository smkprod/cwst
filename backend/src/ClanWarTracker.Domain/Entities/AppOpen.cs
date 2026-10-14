namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Заход в Mini App с источником: откуда человек пришёл (кнопка под постом в чате клана,
/// личное сообщение бота, ответ на команду, канал, чужая ссылка или просто меню) и куда
/// вела кнопка. Строка на человека, день, источник и раздел - сколько раз за день он
/// открыл, не важно: важно, какие кнопки вообще приводят людей.
/// </summary>
public class AppOpen
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public required string DayUtc { get; set; }

    /// <summary>chat / dm / bot / channel / share / link / menu - см. OpenSource.</summary>
    public required string Source { get; set; }

    /// <summary>Раздел, куда вела кнопка: home, review, builder, plus, m (отчёт о бое)...</summary>
    public required string Dest { get; set; }

    /// <summary>Был ли аккаунт уже привязан в момент захода - отделяет новых от своих.</summary>
    public bool Linked { get; set; }

    public DateTime FirstUtc { get; set; }
}
