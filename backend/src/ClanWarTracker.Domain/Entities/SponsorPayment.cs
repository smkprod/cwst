namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Оплата спонсорства звёздами Telegram.
///
/// Журнал нужен не для отчётности, а для двух вещей, без которых приём денег
/// небезопасен. Первая — идемпотентность: Telegram вправе доставить сообщение об
/// оплате повторно, и без записи с уникальным номером платежа второе такое
/// сообщение продлило бы спонсорство ещё на месяц бесплатно. Вторая — возврат:
/// вернуть звёзды можно только по номеру платежа, и если его не сохранить в
/// момент оплаты, взять его потом будет неоткуда.
/// </summary>
public class SponsorPayment
{
    public int Id { get; set; }

    /// <summary>Кто платил. Может не совпадать с получателем — оплата по пересланной ссылке.</summary>
    public long PayerTelegramUserId { get; set; }

    /// <summary>Кому досталось спонсорство.</summary>
    public required string PlayerTag { get; set; }

    public int Stars { get; set; }
    public int Days { get; set; }

    /// <summary>
    /// Номер платежа в Telegram. Уникален: по нему отсекаются повторные доставки
    /// и по нему же делается возврат.
    /// </summary>
    public required string TelegramChargeId { get; set; }

    public DateTime PaidAtUtc { get; set; }

    /// <summary>
    /// За что заплачено: «sponsor» или «plus». Журнал один на все продажи: по нему
    /// же воронка кампаний считает, кто заплатил, и отдельная таблица на каждый
    /// товар разнесла бы деньги по углам.
    /// </summary>
    public string Kind { get; set; } = Kinds.Sponsor;

    /// <summary>Звёзды возвращены. Доступ, купленный этим платежом, при этом отзывается.</summary>
    public DateTime? RefundedAtUtc { get; set; }

    public static class Kinds
    {
        public const string Sponsor = "sponsor";
        public const string Plus = "plus";
    }
}
