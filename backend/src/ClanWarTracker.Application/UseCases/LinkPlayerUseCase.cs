using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class LinkPlayerUseCase(IClashRoyaleApi crApi, IClanRepository clans, IPlayerRepository players)
{
    /// <param name="chatId">ID группового чата клана, или null если вызов из ЛС.</param>
    /// <returns>Имя игрока из CR API или null, если тег не найден.</returns>
    public async Task<string?> ExecuteAsync(long telegramUserId, string playerTag, long? chatId, CancellationToken ct = default)
    {
        playerTag = Normalize(playerTag);
        var name = await crApi.GetPlayerNameAsync(playerTag, ct);
        if (name is null) return null;

        // Если вызов из группы — ищем клан, из ЛС — clan остаётся null
        Clan? clan = null;
        if (chatId.HasValue)
        {
            clan = await clans.GetByChatIdAsync(chatId.Value, ct)
                   ?? throw new InvalidOperationException("Клан не привязан к этому чату. Сначала /setup.");
        }

        var existing = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (existing is not null)
        {
            existing.PlayerTag = playerTag;
            existing.Name = name;
            // Обновляем клан только если привязываем из группы
            if (clan is not null) existing.ClanId = clan.Id;
        }
        else
        {
            await players.AddAsync(new Player
            {
                PlayerTag = playerTag,
                Name = name,
                TelegramUserId = telegramUserId,
                ClanId = clan?.Id,
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
