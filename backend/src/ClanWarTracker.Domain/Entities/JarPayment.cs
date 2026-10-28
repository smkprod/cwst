namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Заказ на оплату через банку Монобанка: человек переводит деньги в банку и пишет
/// в комментарии код заказа, а бот по выписке банки находит перевод и выдаёт доступ.
///
/// Звёзды есть не у всех, а карта Монобанка - почти у каждого в Украине. Код нужен,
/// потому что в выписке банки нет Telegram: без него перевод не с кем сопоставить.
/// </summary>
public class JarOrder
{
    public int Id { get; set; }

    /// <summary>Шесть цифр - их легко набрать в комментарии с любой раскладки.</summary>
    public required string Code { get; set; }

    public long TelegramUserId { get; set; }

    /// <summary>Строка игрока, на которую идёт Клан Про. Для Плюса не нужна: он на аккаунт.</summary>
    public int? PlayerId { get; set; }

    /// <summary>plus или pro, см. <see cref="Products"/>.</summary>
    public required string Product { get; set; }

    public int Days { get; set; }

    /// <summary>Сколько гривен к оплате - цена на момент заказа.</summary>
    public int AmountUah { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Когда выдан доступ. null - ещё не оплачено.</summary>
    public DateTime? PaidUtc { get; set; }

    /// <summary>Сколько пришло, в копейках. null - подтверждено вручную без перевода из выписки.</summary>
    public long? PaidKop { get; set; }

    /// <summary>Подтвердил владелец в панели, а не бот по выписке.</summary>
    public bool Manual { get; set; }

    public static class Products
    {
        public const string Plus = "plus";
        public const string Pro = "pro";
    }
}

/// <summary>
/// Перевод в банку из выписки Монобанка. Каждый пишется один раз (по номеру операции),
/// и сопоставленный, и нет: несопоставленные владелец видит в панели и привязывает
/// к заказу руками - забыл код, ошибся цифрой, заплатил меньше.
/// </summary>
public class JarIncoming
{
    public int Id { get; set; }

    /// <summary>Номер операции в Монобанке. Уникален: повторное чтение выписки не дублирует.</summary>
    public required string MonoTxId { get; set; }

    public long AmountKop { get; set; }

    /// <summary>Комментарий отправителя - там должен быть код.</summary>
    public string? Comment { get; set; }

    /// <summary>Описание операции из выписки: обычно «Від: Имя».</summary>
    public string? Sender { get; set; }

    public DateTime TimeUtc { get; set; }

    /// <summary>Заказ, за который засчитан перевод. null - не сопоставлен.</summary>
    public int? OrderId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
