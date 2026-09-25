namespace ClanWarTracker.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }      // #ABC123
    public required string Name { get; set; }
    public long? TelegramUserId { get; set; }            // null = не привязан к Telegram
    public string? TelegramUsername { get; set; }        // @username в Telegram (для тегов в чате), null — нет
    public int? ClanId { get; set; }   // null = игрок без клана в боте
    public Clan? Clan { get; set; }
    public DateTime? LastReminderSentAt { get; set; }    // анти-спам напоминаний
    public DateTime? LastSmartAlertSentAt { get; set; }   // анти-спам персональных алертов о влиянии на победу
    /// <summary>Кто пригласил этого игрока (Telegram ID пригласившего). null — пришёл сам.</summary>
    public long? ReferrerTelegramUserId { get; set; }

    /// <summary>Когда игрок привязал аккаунт. null — привязан до появления поля.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    /// <summary>
    /// Привязку сделал лидер клана через /bind, а не сам игрок. Нужно, чтобы отличать
    /// данные «со слов главы» от подтверждённых, и чтобы собственный /link игрока
    /// такую привязку перебивал — он лучше знает, какой аккаунт его.
    /// </summary>
    public bool LinkedByLeader { get; set; }

    /// <summary>
    /// Уровни наград на момент, когда игрок их последний раз видел: {"perfectDays":2,...}.
    ///
    /// Без этого «открыл ачивку» не существует как событие: человек просто однажды
    /// замечает, что число стало больше. Храним снимок, чтобы поймать РАЗНИЦУ и
    /// поздравить ровно один раз.
    /// </summary>
    public string? SeenAchievementsJson { get; set; }

    // «Что нового»: снимок на момент прошлого визита в Mini App — для персональной
    // карточки-дельты при входе (ранг ↑/↓, медали +N, респекты с прошлого раза).
    public DateTime? LastVisitAtUtc { get; set; }
    public int? LastVisitFame { get; set; }
    public int? LastVisitRank { get; set; }

    /// <summary>
    /// Сколько раз игрока пнули напоминанием (ручной «пинок» админа или авто-напоминание
    /// перед концом дня). Копится за всё время — для карточки «кого чаще всех пинают».
    /// </summary>
    public int NudgeCount { get; set; }

    /// <summary>
    /// Значок, который игрок выставил напоказ, — ключ из витрины наград
    /// («perfectDays», «mvpWeeks» и т.д.). null — не выбран.
    ///
    /// Награды и так считались, но видел их только сам игрок у себя на экране.
    /// Значок, которого никто не видит, статусом не является: смысл появляется
    /// ровно тогда, когда он стоит рядом с именем в общем списке.
    /// </summary>
    /// <summary>
    /// До какого момента игрок — спонсор. null или прошедшая дата — обычный игрок.
    ///
    /// Датой, а не флагом: спонсорство продаётся на месяц, и «когда кончится»
    /// нужно знать и для показа, и для напоминания о продлении. Флаг пришлось бы
    /// снимать вручную, а руками такое всегда забывают.
    /// </summary>
    public DateTime? SponsorUntilUtc { get; set; }

    /// <summary>Выбранный спонсором фон: ключ из каталога («sky», «arena», «night», «ice»).</summary>
    public string? SponsorBackgroundKey { get; set; }

    /// <summary>Спонсор ли прямо сейчас.</summary>
    public bool IsSponsor(DateTime utcNow) => SponsorUntilUtc is { } until && until > utcNow;

    public string? ShowcaseBadgeKey { get; set; }

    /// <summary>
    /// Уровень выставленного значка на момент выбора: 1 бронза, 2 серебро, 3 золото.
    ///
    /// Храним снимком, а не считаем на лету: подсчёт наград читает полгода снимков
    /// войны на каждого игрока, и делать это для всего состава при каждом открытии
    /// приложения нельзя. Значение освежается, когда игрок сам смотрит свою витрину.
    /// </summary>
    public int ShowcaseBadgeLevel { get; set; }
}
