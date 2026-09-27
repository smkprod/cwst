using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class SetupClanUseCase(IClashRoyaleApi crApi, IClanRepository clans)
{
    /// <param name="messageThreadId">ID темы (Topic) форума, если /setup выполнен внутри темы —
    /// тогда все напоминания/отчёты бот шлёт туда же, а не в общий чат группы.</param>
    /// <returns>Имя клана или null, если тег не найден в CR API.</returns>
    public async Task<string?> ExecuteAsync(
        long chatId, string clanTag, int? messageThreadId = null, CancellationToken ct = default)
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
            existing.TelegramMessageThreadId = messageThreadId;
        }
        else
        {
            await clans.AddAsync(new Clan
            {
                ClanTag = clanTag,
                Name = name,
                TelegramChatId = chatId,
                TelegramMessageThreadId = messageThreadId,
                CreatedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        await clans.SaveChangesAsync(ct);
        return name;
    }
}
