using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ClanRepository(AppDbContext db) : IClanRepository
{
    public Task<Clan?> GetByChatIdAsync(long chatId, CancellationToken ct = default) =>
        db.Clans.FirstOrDefaultAsync(c => c.TelegramChatId == chatId, ct);

    public Task<Clan?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Clans.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Clan?> GetByTagAsync(string clanTag, CancellationToken ct = default) =>
        db.Clans.FirstOrDefaultAsync(c => c.ClanTag == clanTag, ct);

    public Task<List<Clan>> GetAllAsync(CancellationToken ct = default) =>
        db.Clans.ToListAsync(ct);

    public async Task AddAsync(Clan clan, CancellationToken ct = default) =>
        await db.Clans.AddAsync(clan, ct);

    public Task RemoveAsync(Clan clan, CancellationToken ct = default)
    {
        db.Clans.Remove(clan); // игроки и снапшоты удалятся каскадом (FK ON DELETE CASCADE)
        return db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class PlayerRepository(AppDbContext db) : IPlayerRepository
{
    public Task<Player?> GetByTelegramIdAsync(long telegramUserId, CancellationToken ct = default) =>
        db.Players.FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, ct);

    public Task<List<Player>> GetByClanIdAsync(int clanId, CancellationToken ct = default) =>
        db.Players.Where(p => p.ClanId == clanId).ToListAsync(ct);

    public Task<Player?> GetUnclaimedByTagAsync(string playerTag, CancellationToken ct = default) =>
        db.Players
            .Where(p => p.PlayerTag == playerTag && p.TelegramUserId == null)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Все привязанные игроки. Привязка — это не только «человек сам нажал Старт»:
    /// после /bind у игрока может быть один лишь @username, и такого человека бот
    /// вполне тегает в чате. Раньше условие смотрело только на TelegramUserId, и все
    /// привязанные лидером были невидимы для панели и рейтинга.
    ///
    /// Кому нужны именно личные сообщения (рассылка), тот дополнительно отсеивает
    /// записи без TelegramUserId — писать первым Telegram боту не даёт.
    /// </summary>
    public Task<List<Player>> GetAllLinkedAsync(CancellationToken ct = default) =>
        db.Players.AsNoTracking()
            .Include(p => p.Clan)
            .Where(p => p.TelegramUserId != null || p.TelegramUsername != null)
            .ToListAsync(ct);

    public async Task AddAsync(Player player, CancellationToken ct = default) =>
        await db.Players.AddAsync(player, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
