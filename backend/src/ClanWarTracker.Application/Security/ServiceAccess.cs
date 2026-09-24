using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Security;

/// <summary>Уровень доступа к сервису целиком — не к клану и не к чату.</summary>
public enum ServiceRole
{
    /// <summary>Обычный игрок. Панели не видит.</summary>
    None = 0,

    /// <summary>Видит панель. Что именно ему там можно — решают права.</summary>
    Moderator = 1,

    /// <summary>Может всё, и права ему не выдают: он их выдаёт.</summary>
    Owner = 2,
}

/// <summary>
/// Id владельца сервиса. Отдельной записью, а не через IConfiguration, потому что
/// слой Application о конфигурации ничего не знает и знать не должен — хосты
/// (API и воркер) считывают её у себя и передают сюда уже разобранное значение.
/// </summary>
public record ServiceAccessOptions(long OwnerTelegramUserId)
{
    /// <summary>
    /// Единственная реализация проверки владельца на весь проект.
    ///
    /// Живёт на настройках, а не на <see cref="ServiceAccess"/>, чтобы ею мог
    /// пользоваться и бот: он работает фоновой службой, куда scoped-сервис с
    /// репозиторием не внедришь, а владельцу база для опознания и не нужна.
    /// </summary>
    public bool IsOwner(long telegramUserId) =>
        OwnerTelegramUserId != 0 && telegramUserId == OwnerTelegramUserId;
}

/// <summary>Кто вошедший для сервиса и что ему можно.</summary>
public record ServiceIdentity(ServiceRole Role, ServicePermission Permissions)
{
    public static readonly ServiceIdentity Nobody = new(ServiceRole.None, ServicePermission.None);

    /// <summary>
    /// Владелец проходит любую проверку, не сверяясь с флагами: ему их никто не
    /// выдавал и выдать не может — он источник всех остальных прав.
    /// </summary>
    public bool Can(ServicePermission permission) =>
        Role == ServiceRole.Owner || (Permissions & permission) == permission;

    /// <summary>Видит ли панель вообще.</summary>
    public bool HasPanel => Role != ServiceRole.None;
}

/// <summary>
/// Единственное место, где решается «кто ты для сервиса».
///
/// До этого класса проверка владельца была скопирована в четыре контроллера, и
/// каждая копия читала конфиг сама. Любое новое право пришлось бы добавлять
/// четырежды, а забытая копия означала бы дыру, которую никто не заметит.
/// </summary>
public class ServiceAccess(IServiceModeratorRepository moderators, ServiceAccessOptions options)
{
    /// <summary>Владелец известен из конфига и в базе не хранится: он появляется раньше базы.</summary>
    public bool IsOwner(long telegramUserId) => options.IsOwner(telegramUserId);

    /// <param name="username">
    /// Юзернейм вошедшего, если Telegram его прислал. Нужен только чтобы узнать
    /// модератора в первый раз — дальше запись опознаётся по числовому id.
    /// </param>
    public async Task<ServiceIdentity> ResolveAsync(
        long telegramUserId, string? username, CancellationToken ct = default)
    {
        if (IsOwner(telegramUserId))
            return new ServiceIdentity(ServiceRole.Owner, ServicePermission.All);

        var moderator = await moderators.FindAsync(telegramUserId, username, ct);
        if (moderator is null) return ServiceIdentity.Nobody;

        // Первый вход: закрепляем запись за числовым id. С этого момента юзернейм
        // на доступ не влияет — ни его смена владельцем, ни захват освободившегося
        // имени посторонним.
        if (moderator.TelegramUserId is null)
        {
            moderator.TelegramUserId = telegramUserId;
            moderator.FirstSeenAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(username))
                moderator.TelegramUsername = ServiceModerator.NormalizeUsername(username);
            await moderators.SaveChangesAsync(ct);
        }

        return new ServiceIdentity(ServiceRole.Moderator, moderator.Permissions);
    }
}
