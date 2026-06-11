using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class SetupClanUseCase(IClashRoyaleApi crApi, IClanRepository clans)
{
    /// <summary>Новые кланы получают пробный Pro, чтобы увидеть прогнозы и историю.</summary>
    private const int TrialDays = 14;

    /// <returns>Имя клана или null, если тег не найден в CR API.</returns>
    public async Task<string?> ExecuteAsync(long chatId, string clanTag, CancellationToken ct = default)
    {
        clanTag = LinkPlayerUseCase.Normalize(clanTag);
        var name = await crApi.GetClanNameAsync(clanTag, ct);
        if (name is null) return null;

        // Ищем запись для этого чата ИЛИ для этого тега (мог остаться от старого чата)
        var existing = await clans.GetByChatIdAsync(chatId, ct)
                       ?? await clans.GetByTagAsync(clanTag, ct);

        if (existing is not null)
        {
            existing.ClanTag = clanTag;
            existing.Name = name;
            existing.TelegramChatId = chatId;
        }
        else
        {
            await clans.AddAsync(new Clan
            {
                ClanTag = clanTag,
                Name = name,
                TelegramChatId = chatId,
                PlanTier = PlanTier.Pro,
                PlanExpiresAtUtc = DateTime.UtcNow.AddDays(TrialDays),
            }, ct);
        }

        await clans.SaveChangesAsync(ct);
        return name;
    }
}
