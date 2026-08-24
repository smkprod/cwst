using ClanWarTracker.Application.Notifications;
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
        long? referrerTelegramUserId = null, string? telegramUsername = null, CancellationToken ct = default)
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
            if (!string.IsNullOrEmpty(telegramUsername)) existing.TelegramUsername = telegramUsername;
            // Обновляем клан только если привязываем из группы
            if (clan is not null) existing.ClanId = clan.Id;
            await players.SaveChangesAsync(ct);
            return name;
        }

        // Реферал засчитывается только при ПЕРВОЙ привязке, и только если пригласивший —
        // реальный игрок и это не он сам.
        Player? referrer = null;
        if (referrerTelegramUserId is { } refId && refId != telegramUserId)
            referrer = await players.GetByTelegramIdAsync(refId, ct);

        // Лидер мог привязать этот тег заранее через /bind — тогда в базе уже лежит
        // запись с одним @username и без TelegramUserId. Это тот же самый человек,
        // поэтому занимаем её. Иначе в клане появлялись две строки на один тег: одна
        // «от лидера», другая «от игрока», и обе висели в панели.
        var unclaimed = await players.GetUnclaimedByTagAsync(playerTag, ct);
        if (unclaimed is not null)
        {
            unclaimed.TelegramUserId = telegramUserId;
            unclaimed.Name = name;
            if (!string.IsNullOrEmpty(telegramUsername)) unclaimed.TelegramUsername = telegramUsername;
            if (clan is not null) unclaimed.ClanId = clan.Id;
            // Привязку подтвердил сам игрок — это уже не «со слов лидера»
            unclaimed.LinkedByLeader = false;
            if (unclaimed.ReferrerTelegramUserId is null && referrer is not null)
                unclaimed.ReferrerTelegramUserId = referrerTelegramUserId;
            await players.SaveChangesAsync(ct);

            await NotifyReferrerAsync(referrer, name, ct);
            return name;
        }

        await players.AddAsync(new Player
        {
            PlayerTag = playerTag,
            Name = name,
            TelegramUserId = telegramUserId,
            TelegramUsername = telegramUsername,
            ClanId = clan?.Id,
            ReferrerTelegramUserId = referrer is null ? null : referrerTelegramUserId,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);
        await players.SaveChangesAsync(ct);

        await NotifyReferrerAsync(referrer, name, ct);
        return name;
    }

    /// <summary>
    /// Награда виральной петли: сразу сообщаем пригласившему, что друг подключился.
    /// Язык берём у клана пригласившего — это его бот и его язык, а не приглашённого.
    /// </summary>
    private async Task NotifyReferrerAsync(Player? referrer, string name, CancellationToken ct)
    {
        if (referrer?.TelegramUserId is not long refId) return;

        var refClan = referrer.ClanId.HasValue
            ? await clans.GetByIdAsync(referrer.ClanId.Value, ct)
            : null;
        var t = NotificationSettings.Parse(refClan?.NotificationSettingsJson).Text;

        try
        {
            await notifier.SendToUserAsync(refId, string.Format(t.ReferralJoined, name), ct);
        }
        catch { /* пригласивший мог заблокировать бота — не критично */ }
    }

    public static string Normalize(string tag)
    {
        tag = tag.Trim().ToUpperInvariant().Replace("O", "0");
        return tag.StartsWith('#') ? tag : "#" + tag;
    }
}
