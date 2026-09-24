namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Модератор сервиса: человек, которому владелец открыл панель на просмотр.
///
/// Назначают по юзернейму, потому что в момент назначения о человеке больше ничего
/// не известно — он может вообще ни разу не открывать приложение. Но юзернейм в
/// Telegram можно освободить, и тогда его займёт кто угодно, получив заодно и права.
/// Поэтому юзернейм здесь — только способ узнать человека в первый раз: при первом
/// же совпадении мы запоминаем его числовой id, и дальше сверяемся только по нему.
/// После этого смена или потеря юзернейма на доступ не влияет, а чужой человек,
/// занявший освободившийся юзернейм, модератором не станет.
/// </summary>
public class ServiceModerator
{
    public int Id { get; set; }

    /// <summary>Юзернейм без «@» и в нижнем регистре — сравнивать иначе нечем.</summary>
    public required string TelegramUsername { get; set; }

    /// <summary>
    /// Числовой id. Пуст, пока человек ни разу не заходил; проставляется при первом
    /// совпадении по юзернейму и с этого момента становится единственным признаком.
    /// </summary>
    public long? TelegramUserId { get; set; }

    /// <summary>Заметка владельца: кто это и зачем. Не обязательна.</summary>
    public string? Note { get; set; }

    public DateTime AddedAtUtc { get; set; }

    /// <summary>Кто назначил — на случай, если модераторов станет несколько.</summary>
    public long AddedByTelegramUserId { get; set; }

    /// <summary>Когда человек впервые открыл панель. Пусто — ещё ни разу.</summary>
    public DateTime? FirstSeenAtUtc { get; set; }

    /// <summary>Юзернейм к виду, в котором он лежит в базе: без «@», в нижнем регистре.</summary>
    public static string NormalizeUsername(string raw) =>
        raw.Trim().TrimStart('@').ToLowerInvariant();
}
