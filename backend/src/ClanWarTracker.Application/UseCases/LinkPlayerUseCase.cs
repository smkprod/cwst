using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class LinkPlayerUseCase(IClashRoyaleApi crApi, IClanRepository clans, IPlayerRepository players)
{
    /// <returns>Имя игрока из CR API или null, если тег не найден.</returns>
    public async Task<string?> ExecuteAsync(long telegramUserId, string playerTag, long chatId, CancellationToken ct = default)
    {
        playerTag = Normalize(playerTag);
        var name = await crApi.GetPlayerNameAsync(playerTag, ct);
        if (name is null) return null;

        var clan = await clans.GetByChatIdAsync(chatId, ct)
                   ?? throw new InvalidOperationException("Клан не привязан к этому чату. Сначала /setup.");

        var existing = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (existing is not null)
        {
            existing.PlayerTag = playerTag;
            existing.Name = name;
            existing.ClanId = clan.Id;
        }
        else
        {
            await players.AddAsync(new Player
            {
                PlayerTag = playerTag,
                Name = name,
                TelegramUserId = telegramUserId,
                ClanId = clan.Id
            }, ct);
        }

        await players.SaveChangesAsync(ct);
        return name;
    }

    public static string Normalize(string tag)
    {
        tag = tag.Trim().ToUpperInvariant().Replace("O", "0");
        return tag.StartsWith('#') ? tag : "#" + tag;
    }
}
