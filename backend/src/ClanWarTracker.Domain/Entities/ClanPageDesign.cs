namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Оформление страницы клана.
///
/// Фон клана и дизайн — разные вещи, и разделены намеренно. Фон это картинка, он
/// берётся из набора <see cref="SponsorBackground.ForClans"/> и выбирается спонсором
/// лично. Дизайн это рамка, свечение и цвет заголовка вокруг этой картинки, и он
/// принадлежит клану, а не человеку: спонсор может уйти, а страница клана должна
/// остаться такой, какой её сделали.
///
/// Поэтому дизайн лежит на клане, а не на игроке, и переживает окончание
/// спонсорства. Открыть новый дизайн может только клан со спонсором, но потерять
/// уже выбранный из-за того, что спонсорство кончилось, нельзя — иначе страница
/// сама себя ломает в день, когда никто ничего не менял.
/// </summary>
public static class ClanPageDesign
{
    /// <summary>Обычная тёмная карточка. Такая страница у всех по умолчанию.</summary>
    public const string Plain = "plain";

    public const string Royal = "royal";
    public const string Neon = "neon";
    public const string Stone = "stone";
    public const string Blood = "blood";
    public const string Frost = "frost";
    public const string Gold = "gold";

    /// <summary>Доступно любому клану.</summary>
    public static readonly string[] Free = [Plain];

    /// <summary>Открывается клану, в котором есть действующий спонсор.</summary>
    public static readonly string[] ForSponsors = [Royal, Gold, Neon, Stone, Blood, Frost];

    public static readonly string[] All = [.. Free, .. ForSponsors];

    public static bool IsKnown(string? key) => key is not null && Array.IndexOf(All, key) >= 0;

    public static bool NeedsSponsor(string? key) => key is not null && Array.IndexOf(ForSponsors, key) >= 0;

    /// <summary>Ключ, если он из каталога; иначе <see cref="Plain"/>.</summary>
    public static string Normalize(string? key) => IsKnown(key) ? key! : Plain;

    /// <summary>Девиз клана на его странице. Одна строка, не полотно текста.</summary>
    public const int MaxMottoLength = 120;
}
