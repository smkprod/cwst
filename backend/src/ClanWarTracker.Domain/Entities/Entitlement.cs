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

    /// <summary>Кто подарил (Source = gift). null - купил себе сам или выдано не игроком.</summary>
    public long? GiverTelegramUserId { get; set; }

    /// <summary>Когда напомнили, что пропуск заканчивается. Одно напоминание на срок.</summary>
    public DateTime? ReminderSentUtc { get; set; }

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
        /// <summary>Плюс, входящий в купленное спонсорство: дни складываются с купленным Плюсом.</summary>
        public const string Sponsor = "sponsor";
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

    /// <summary>Язык сообщений (ru/uk/en). null - берём язык клана, иначе русский.</summary>
    public string? Lang { get; set; }

    /// <summary>Смещение местного времени от UTC в минутах - для тихих часов и «до 21:52».</summary>
    public int? TzOffsetMinutes { get; set; }

    /// <summary>
    /// Бесплатные живые сигналы без Плюса. Вместо триала на дни: человек сначала сам
    /// видит, как бот вовремя его остановил, и платит уже за это.
    /// </summary>
    public int FreeSignalsLeft { get; set; } = DefaultFreeSignals;

    public const int DefaultFreeSignals = 2;

    /// <summary>После скольких поражений подряд писать: 2 или 3.</summary>
    public int LossThreshold { get; set; } = 2;

    /// <summary>«Лимит на вечер»: столько поражений за день - и бот предлагает закончить. null - выключен.</summary>
    public int? DailyLossLimit { get; set; }

    /// <summary>Тихие часы 23:00-08:00 по местному времени: ночью не пишем.</summary>
    public bool QuietHours { get; set; } = true;

    /// <summary>Нажал «Пауза»: до этого момента молчим, потом пишем «можно».</summary>
    public DateTime? PauseUntilUtc { get; set; }

    /// <summary>Нажал «Сегодня не писать»: молчим до этого момента (конец местного дня).</summary>
    public DateTime? MutedUntilUtc { get; set; }

    /// <summary>Когда прислали знакомство с тильт-типом. Один раз на человека.</summary>
    public DateTime? IntroSentUtc { get; set; }

    /// <summary>День (местный, yyyy-MM-dd), когда уже написали про «лимит на вечер».</summary>
    public string? LimitAlertDay { get; set; }

    // Трекер боёв: одна тихая карточка захода, которая правится после каждого боя.

    public bool TrackerEnabled { get; set; }

    /// <summary>Последний бой, который трекер уже учёл. Пишется до отправки: лучше пропустить, чем повторить.</summary>
    public DateTime? TrackerWatermarkUtc { get; set; }

    /// <summary>Открытая карточка захода и начало захода, к которому она относится.</summary>
    public int? TrackerCardMessageId { get; set; }
    public DateTime? TrackerCardStartUtc { get; set; }

    /// <summary>«Сегодня без карточек» - до местной полуночи.</summary>
    public DateTime? TrackerMutedUntilUtc { get; set; }

    /// <summary>Бой, на котором сегодня показана бесплатная подсказка Плюса.</summary>
    public DateTime? TrackerHintBattleUtc { get; set; }

    // Личная скидка на Плюс (после челленджа): цена на себя до указанного момента.
    public int? PromoPrice7 { get; set; }
    public int? PromoPrice30 { get; set; }
    public DateTime? PromoUntilUtc { get; set; }
}

/// <summary>
/// Один сигнал «Стоп-тильта» и что было после него.
///
/// Нужен не для отчётности: по нему бот правит своё же сообщение в итог захода,
/// помнит нажатую кнопку и считает главный довод продлить Плюс - «после паузы ты
/// выигрываешь 57%, без паузы 29%». Без журнала эту цифру взять было бы неоткуда.
/// </summary>
public class TiltAlert
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public required string PlayerTag { get; set; }

    /// <summary>streak - серия поражений; limit - «лимит на вечер».</summary>
    public required string Kind { get; set; }

    public DateTime SentUtc { get; set; }

    /// <summary>Бой, после которого написали: всё, что позже, - «после сигнала».</summary>
    public DateTime TriggerBattleUtc { get; set; }

    public DateTime SessionStartUtc { get; set; }
    public int LossStreak { get; set; }

    /// <summary>Сообщение в личке - его бот правит в итог захода. null - не доставлено.</summary>
    public int? MessageId { get; set; }

    /// <summary>Бесплатный сигнал (без Плюса).</summary>
    public bool Free { get; set; }

    /// <summary>Язык, на котором написали, - итог пишется на нём же.</summary>
    public string Lang { get; set; } = "ru";

    /// <summary>
    /// Текст отправленного сигнала. Telegram правит сообщение только целиком, а по
    /// кнопке «Пауза» к тому же тексту надо дописать одну строку.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>Нажатая кнопка: pause, go, mute. null - не нажал ничего.</summary>
    public string? Choice { get; set; }
    public DateTime? ChoiceUtc { get; set; }

    /// <summary>Когда написали «можно» после паузы.</summary>
    public DateTime? ResumeSentUtc { get; set; }

    /// <summary>Когда подвели итог захода. null - заход ещё идёт.</summary>
    public DateTime? SummaryUtc { get; set; }

    public int SessionWins { get; set; }
    public int SessionLosses { get; set; }
    public int SessionTrophies { get; set; }

    /// <summary>Бои после сигнала в том же заходе - для «пауза работает».</summary>
    public int AfterWins { get; set; }
    public int AfterLosses { get; set; }
}
