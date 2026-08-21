using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum BindOutcome { Ok, TagNotFound, NotInClan, NothingToUnbind }

public record BindResult(BindOutcome Outcome, string? PlayerName = null, bool CanDm = false);

/// <summary>
/// Привязка игрока к Telegram-аккаунту руками лидера. Смысл в том, что заставить
/// полсотни человек написать боту — заведомо проигрышная затея, а чтобы ТЕГНУТЬ
/// человека в чате, его регистрация не нужна: достаточно @username.
///
/// Личные сообщения — другое дело: Telegram запрещает боту писать первым, поэтому
/// в ЛС дойдёт только тем, у кого есть TelegramUserId (то есть кто сам нажимал
/// «Старт»). Про это честно сообщаем лидеру в ответе.
/// </summary>
public class BindPlayerUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IClanRepository clans)
{
    /// <param name="telegramUserId">ID из ответа на сообщение; null — привязка только по @username.</param>
    public async Task<BindResult> BindAsync(int clanId, string rawTag, string? username,
        long? telegramUserId, CancellationToken ct = default)
    {
        var playerTag = LinkPlayerUseCase.Normalize(rawTag);

        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return new(BindOutcome.NotInClan);

        // Тег должен принадлежать текущему составу клана — иначе лидер по опечатке
        // привяжет постороннего, и бот начнёт тегать не того человека.
        Dictionary<string, string> roles;
        try { roles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
        catch { roles = new(StringComparer.OrdinalIgnoreCase); }
        if (roles.Count > 0 && !roles.ContainsKey(playerTag)) return new(BindOutcome.NotInClan);

        var name = await crApi.GetPlayerNameAsync(playerTag, ct);
        if (name is null) return new(BindOutcome.TagNotFound);

        var clanPlayers = await players.GetByClanIdAsync(clanId, ct);
        var existing = clanPlayers.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));

        // Тот же Telegram уже привязан к другому тегу? Переносим привязку, а не создаём
        // вторую запись: TelegramUserId уникален, вставка упала бы на индексе.
        if (telegramUserId is long uid)
        {
            var byUser = await players.GetByTelegramIdAsync(uid, ct);
            if (byUser is not null && byUser.Id != existing?.Id)
            {
                byUser.PlayerTag = playerTag;
                byUser.Name = name;
                byUser.ClanId = clanId;
                if (!string.IsNullOrEmpty(username)) byUser.TelegramUsername = username;
                byUser.LinkedByLeader = true;
                await players.SaveChangesAsync(ct);
                return new(BindOutcome.Ok, name, CanDm: true);
            }
        }

        if (existing is not null)
        {
            existing.Name = name;
            if (!string.IsNullOrEmpty(username)) existing.TelegramUsername = username;
            if (telegramUserId is not null) existing.TelegramUserId = telegramUserId;
            existing.LinkedByLeader = true;
            await players.SaveChangesAsync(ct);
            return new(BindOutcome.Ok, name, existing.TelegramUserId is not null);
        }

        await players.AddAsync(new Player
        {
            PlayerTag = playerTag,
            Name = name,
            ClanId = clanId,
            TelegramUserId = telegramUserId,
            TelegramUsername = username,
            LinkedByLeader = true,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);
        await players.SaveChangesAsync(ct);
        return new(BindOutcome.Ok, name, telegramUserId is not null);
    }

    /// <summary>Снимает привязку — но только ту, что поставил лидер.</summary>
    public async Task<BindResult> UnbindAsync(int clanId, string rawTag, CancellationToken ct = default)
    {
        var playerTag = LinkPlayerUseCase.Normalize(rawTag);
        var clanPlayers = await players.GetByClanIdAsync(clanId, ct);
        var existing = clanPlayers.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));

        if (existing is null) return new(BindOutcome.NothingToUnbind);

        // Привязку, которую игрок сделал сам, лидер снимать не должен —
        // это его аккаунт, а не запись в клановой табличке.
        if (!existing.LinkedByLeader) return new(BindOutcome.NothingToUnbind, existing.Name);

        existing.TelegramUsername = null;
        existing.TelegramUserId = null;
        existing.LinkedByLeader = false;
        await players.SaveChangesAsync(ct);
        return new(BindOutcome.Ok, existing.Name);
    }
}
