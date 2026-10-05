namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Игрок лиги дуэлей. Вступает тот, кто дал ссылку «добавить в друзья»: без неё
/// соперник не сможет позвать его в дружеский бой, и вызов повиснет.
/// </summary>
public class DuelProfile
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public required string PlayerTag { get; set; }
    public required string Name { get; set; }

    /// <summary>Ссылка из игры (link.clashroyale.com/invite/friend…) с тегом этого же игрока.</summary>
    public required string FriendLink { get; set; }

    /// <summary>Рейтинг Эло. Старт - 1000.</summary>
    public int Rating { get; set; } = 1000;
    public int Peak { get; set; } = 1000;
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    /// <summary>Язык интерфейса Telegram на момент вступления - на нём шлём личные сообщения.</summary>
    public string? Lang { get; set; }

    public DateTime JoinedUtc { get; set; }
    public DateTime? LastDuelUtc { get; set; }
}

public enum DuelState
{
    Active = 0,
    Finished = 1,
    Cancelled = 2,
    Expired = 3,
}

/// <summary>
/// Принятый вызов 1х1. Непринятых в базе нет: вызов живёт в кнопке сообщения, а
/// строка появляется, когда соперник нажал «Принять». Счёт бот берёт из журнала
/// боёв сам - ни скриншотов, ни споров.
/// </summary>
public class Duel
{
    public int Id { get; set; }

    /// <summary>1 или 3.</summary>
    public int BestOf { get; set; }

    /// <summary>Режим дружеского боя (DuelModes): засчитываются только бои в нём. null - старые дуэли, любой режим.</summary>
    public string? Mode { get; set; }
    public DuelState State { get; set; }

    /// <summary>A - кто вызвал, B - кто принял.</summary>
    public long ATelegramUserId { get; set; }
    public required string ATag { get; set; }
    public required string AName { get; set; }
    public long BTelegramUserId { get; set; }
    public required string BTag { get; set; }
    public required string BName { get; set; }

    public int ScoreA { get; set; }
    public int ScoreB { get; set; }

    /// <summary>Рейтинг до дуэли и изменение. Delta 0 у нерейтинговой (лимит на пару в сутки).</summary>
    public int RatingA { get; set; }
    public int RatingB { get; set; }
    public int DeltaA { get; set; }
    public int DeltaB { get; set; }
    public bool Rated { get; set; }

    public DateTime AcceptedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public DateTime? CheckedUtc { get; set; }

    /// <summary>Где карточка вызова: сообщение в чате или инлайн-сообщение. Её и правим.</summary>
    public long? ChatId { get; set; }
    public int? MessageId { get; set; }
    public string? InlineMessageId { get; set; }

    /// <summary>Язык карточки - того, кто вызвал.</summary>
    public string? Lang { get; set; }
}
