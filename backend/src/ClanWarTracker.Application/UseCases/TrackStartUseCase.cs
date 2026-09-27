using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Запоминает, откуда пришёл человек, по параметру /start.
///
/// «ad_&lt;код&gt;» — рекламная кампания, «ref_&lt;id&gt;» — реферальная ссылка игрока.
/// Считаются только новые люди: уже привязавшийся игрок, нажавший рекламную
/// ссылку, пришёл не из неё, и без этой проверки реклама получала бы в свою
/// воронку тех, кто и так был в боте.
/// </summary>
public class TrackStartUseCase(IAcquisitionRepository acquisitions, IPlayerRepository players)
{
    public const string AdPrefix = "ad_";
    public const string RefPrefix = "ref_";

    /// <summary>Источник для воронки: «ad:&lt;код&gt;». Отдельно от ссылки, чтобы в базе не жил формат Telegram.</summary>
    public static string AdSource(string code) => "ad:" + code;
    public const string RefSource = "ref";

    /// <returns>true — источник записан.</returns>
    public async Task<bool> ExecuteAsync(long telegramUserId, string? startArg, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(startArg)) return false;

        Acquisition? entry = null;
        if (startArg.StartsWith(AdPrefix, StringComparison.Ordinal))
        {
            // Код может не совпасть ни с одной кампанией — опечатка в ссылке. Всё равно
            // пишем: в панели он окажется среди неизвестных, и будет видно, что ссылка
            // гуляет с ошибкой, а не что реклама не принесла никого.
            var code = startArg[AdPrefix.Length..].ToLowerInvariant();
            if (!Campaign.IsValidCode(code)) return false;
            entry = new Acquisition { TelegramUserId = telegramUserId, Source = AdSource(code) };
        }
        else if (startArg.StartsWith(RefPrefix, StringComparison.Ordinal)
                 && long.TryParse(startArg.AsSpan(RefPrefix.Length), out var referrer)
                 && referrer != telegramUserId)
        {
            entry = new Acquisition
            {
                TelegramUserId = telegramUserId,
                Source = RefSource,
                ReferrerTelegramUserId = referrer,
            };
        }

        if (entry is null) return false;
        if (await players.GetByTelegramIdAsync(telegramUserId, ct) is not null) return false;

        entry.StartedAtUtc = DateTime.UtcNow;
        return await acquisitions.AddIfNewAsync(entry, ct);
    }
}
