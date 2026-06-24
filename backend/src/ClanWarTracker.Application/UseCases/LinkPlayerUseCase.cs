using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class LinkPlayerUseCase(
    IClashRoyaleApi crApi, IClanRepository clans, IPlayerRepository players, INotificationSender notifier)
{
    /// <param name="chatId">ID группового чата клана, или null если вызов из ЛС.</param>
    /// <param name="referrerTelegramUserId">Telegram ID пригласившего (из реф-ссылки), или null.</param>
    /// <returns>Имя игрока из CR API или null, если тег не найден.</returns>
    public async Task<string?> ExecuteAsync(long telegramUserId, string playerTag, long? chatId,
        long? referrerTelegramUserId = null, CancellationToken ct = default)
    {
        playerTag = Normalize(playerTag);
        var name = await crApi.GetPlayerNameAsync(playerTag, ct);
        if (name is null) return null;

        // Если вызов из группы — ищем клан, из ЛС — clan остаётся null
        Clan? clan = null;
        if (chatId.HasValue)
        {
            clan = await clans.GetByChatIdAsync(chatId.Value, ct);
            // Clan not set up yet — link player anyway; ClanController auto-assigns clan later
        }

        var existing = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (existing is not null)
        {
            existing.PlayerTag = playerTag;
            existing.Name = name;
            // Обновляем клан только если привязываем из группы
            if (clan is not null) existing.ClanId = clan.Id;
            await players.SaveChangesAsync(ct);
            return name;
        }

        // Новый игрок: фиксируем пригласившего (реферал засчитывается только при ПЕРВОЙ привязке,
        // и только если пригласивший — реальный игрок и это не он сам).
        Player? referrer = null;
        if (referrerTelegramUserId is { } refId && refId != telegramUserId)
            referrer = await players.GetByTelegramIdAsync(refId, ct);

        await players.AddAsync(new Player
        {
            PlayerTag = playerTag,
            Name = name,
            TelegramUserId = telegramUserId,
            ClanId = clan?.Id,
            ReferrerTelegramUserId = referrer is null ? null : referrerTelegramUserId,
        }, ct);
        await players.SaveChangesAsync(ct);

        // Награда виральной петли: сразу сообщаем пригласившему, что друг подключился.
        if (referrer is not null)
        {
            try
            {
                await notifier.SendToUserAsync(referrer.TelegramUserId!.Value,
                    $"🎉 По твоей ссылке в Clanify зашёл новый игрок: {name}. Спасибо, что зовёшь друзей!", ct);
            }
            catch { /* пригласивший мог заблокировать бота — не критично */ }
        }

        return name;
    }

    public static string Normalize(string tag)
    {
        tag = tag.Trim().ToUpperInvariant().Replace("O", "0");
        return tag.StartsWith('#') ? tag : "#" + tag;
    }
}
