namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Выдача доступа к платной функции: покупка, триал, подарок или ручная выдача.
///
/// Привязано к Telegram-аккаунту, а не к строке игрока. Строк игрока у одного
/// человека бывает несколько - по одной на каждый клан, где он бывал, - и доступ,
/// записанный в одну из них, однажды уже терялся при переходе в другой клан.
/// Аккаунт Telegram у человека один, и платит именно он.
///
/// Строка на каждую выдачу, а не одно поле «до какого числа»: по журналу видно,
/// откуда взялся доступ, его можно отозвать при возврате звёзд, а триал - выдать
/// ровно один раз.
/// </summary>
public class Entitlement
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }

    /// <summary>Что выдано. Пока одно - <see cref="Skus.Plus"/>.</summary>
    public required string Sku { get; set; }

    /// <summary>Откуда: покупка, триал, подарок, ручная выдача. См. <see cref="Sources"/>.</summary>
    public required string Source { get; set; }

    public int Days { get; set; }

    /// <summary>
    /// До какого момента действует доступ после этой выдачи. Считается от конца
    /// предыдущего доступа, а не от «сейчас»: докупивший заранее не теряет остаток.
    /// </summary>
    public DateTime UntilUtc { get; set; }

    /// <summary>Тег на момент выдачи - чтобы триал нельзя было взять второй раз на тот же тег.</summary>
    public string? PlayerTag { get; set; }

    public int Stars { get; set; }

    /// <summary>Номер платежа Telegram у покупки. По нему доступ отзывается при возврате.</summary>
    public string? ChargeId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Отозвано (возврат звёзд или решение владельца). Отозванная выдача доступа не даёт.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    public static class Skus
    {
        public const string Plus = "plus";
    }

    public static class Sources
    {
        public const string Purchase = "purchase";
        public const string Trial = "trial";
        public const string Gift = "gift";
        public const string Grant = "grant";
    }
}

/// <summary>
/// Личные настройки оповещений игрока - по Telegram-аккаунту, как и доступ.
/// Сюда же пишется состояние «Стоп-тильта», чтобы не слать одно и то же дважды.
/// </summary>
public class PlayerAlertPrefs
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }

    /// <summary>
    /// «Стоп-тильт». null - человек ещё не выбирал: у кого есть Плюс, тому включено,
    /// ради этого его и покупают. Выключил явно - не пишем, даже с Плюсом.
    /// </summary>
    public bool? TiltAlerts { get; set; }

    /// <summary>Время боя, после которого написали в последний раз: одна серия - одно сообщение.</summary>
    public DateTime? LastAlertBattleUtc { get; set; }

    public DateTime? LastAlertSentUtc { get; set; }

    /// <summary>День (UTC, yyyy-MM-dd) и число сообщений за него - потолок, чтобы не превратиться в спам.</summary>
    public string? AlertDay { get; set; }
    public int AlertsToday { get; set; }

    /// <summary>
    /// Написать не вышло: человек не запускал бота или заблокировал его. Больше не
    /// пытаемся, пока он сам не откроет приложение или не напишет боту.
    /// </summary>
    public bool DmBlocked { get; set; }
}
