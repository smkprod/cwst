namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Шаг к покупке: человек увидел предложение (окно Плюса, витрина) или нажал «Купить» -
/// сервер выставил счёт. Оплату отдельно не пишем: она и так лежит в выдачах Плюса и
/// оплатах спонсорства. Строка на человека, день, товар, шаг и место - сколько раз за
/// день он открыл окно, не важно: важно, дошёл ли до следующего шага.
/// </summary>
public class FunnelEvent
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public required string DayUtc { get; set; }

    /// <summary>plus / sponsor - см. <see cref="Products"/>.</summary>
    public required string Product { get; set; }

    /// <summary>seen / buy - см. <see cref="Steps"/>.</summary>
    public required string Step { get; set; }

    /// <summary>Где: sheet (окно покупки), showcase (витрина), tab (витрина вкладкой), invoice (счёт).</summary>
    public required string Place { get; set; }

    public DateTime FirstUtc { get; set; }

    public static class Products
    {
        public const string Plus = "plus";
        public const string Sponsor = "sponsor";
        public static readonly string[] All = [Plus, Sponsor];
    }

    public static class Steps
    {
        public const string Seen = "seen";
        public const string Buy = "buy";
    }

    /// <summary>Места, откуда приложение может отметить «увидел». Остальное отбрасываем.</summary>
    public static readonly string[] SeenPlaces = ["sheet", "showcase", "tab"];
}
