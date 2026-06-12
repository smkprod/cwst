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

    public Task<List<Player>> GetAllLinkedAsync(CancellationToken ct = default) =>
        db.Players.AsNoTracking()
            .Include(p => p.Clan)
            .Where(p => p.TelegramUserId != null)
            .ToListAsync(ct);

    public async Task AddAsync(Player player, CancellationToken ct = default) =>
        await db.Players.AddAsync(player, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
