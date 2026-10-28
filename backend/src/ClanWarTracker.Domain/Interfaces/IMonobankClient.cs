namespace ClanWarTracker.Domain.Interfaces;

/// <summary>
/// Личный API Монобанка владельца: только чтение банки. Ключ - в .env на сервере
/// (MONOBANK_TOKEN); без него оплата через банку подтверждается руками в панели.
/// </summary>
public interface IMonobankClient
{
    /// <summary>Ключ задан - выписку можно читать.</summary>
    bool Configured { get; }

    /// <summary>Банки владельца: id для выписки и sendId из ссылки send.monobank.ua/jar/….</summary>
    Task<IReadOnlyList<MonoJar>> JarsAsync(CancellationToken ct = default);

    /// <summary>Операции банки за период (не больше 31 дня). Монобанк пускает раз в минуту.</summary>
    Task<IReadOnlyList<MonoTx>> StatementAsync(string accountId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public record MonoJar(string Id, string SendId, string Title);

/// <param name="AmountKop">Сумма в копейках: пополнение банки - больше нуля.</param>
public record MonoTx(string Id, DateTime TimeUtc, long AmountKop, string? Comment, string? Description);
