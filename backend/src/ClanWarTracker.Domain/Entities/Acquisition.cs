namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Откуда пришёл человек и докуда дошёл.
///
/// Одна строка на пользователя Telegram, записывается при первом /start с меткой
/// и дальше только дополняется отметками шагов. Первый источник выигрывает:
/// человек, пришедший по рекламе, а потом нажавший чью-то реферальную ссылку,
/// засчитывается рекламе — иначе платная кампания отдавала бы свои результаты
/// бесплатной.
///
/// Раньше реферал жил в памяти воркера до привязки тега и терялся при каждом
/// перезапуске, то есть при каждом деплое. А если тег привязывали в приложении,
/// он терялся всегда: приложение работает в другом контейнере и этой памяти не
/// видит.
/// </summary>
public class Acquisition
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }

    /// <summary>«ad:&lt;код&gt;» — рекламная кампания, «ref» — реферальная ссылка игрока.</summary>
    public required string Source { get; set; }

    /// <summary>Кто пригласил — только для источника «ref».</summary>
    public long? ReferrerTelegramUserId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    /// <summary>Привязал свой тег Clash Royale.</summary>
    public DateTime? LinkedAtUtc { get; set; }

    /// <summary>Подключил бота к чату своего клана — главное, ради чего платят за рекламу.</summary>
    public DateTime? ClanConnectedAtUtc { get; set; }
}
