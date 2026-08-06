using ClanWarTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Clan> Clans => Set<Clan>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<WarSnapshot> WarSnapshots => Set<WarSnapshot>();
    public DbSet<PlayerWarSnapshot> PlayerWarSnapshots => Set<PlayerWarSnapshot>();
    public DbSet<WarBattle> WarBattles => Set<WarBattle>();
    public DbSet<Respect> Respects => Set<Respect>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Clan>(e =>
        {
            e.HasIndex(c => c.TelegramChatId).IsUnique();
            e.HasIndex(c => c.ClanTag).IsUnique();
            e.Property(c => c.ClanTag).HasMaxLength(16);
        });

        mb.Entity<WarSnapshot>(e =>
        {
            e.HasIndex(s => new { s.ClanId, s.SeasonId, s.SectionIndex, s.PeriodIndex }).IsUnique();
            e.Property(s => s.PeriodType).HasMaxLength(16);
            e.Property(s => s.Source).HasMaxLength(8).HasDefaultValue("live");
            e.HasOne(s => s.Clan)
             .WithMany()
             .HasForeignKey(s => s.ClanId);
        });

        mb.Entity<PlayerWarSnapshot>(e =>
        {
            e.HasIndex(p => new { p.WarSnapshotId, p.PlayerTag }).IsUnique();
            e.HasIndex(p => p.PlayerTag);
            e.Property(p => p.PlayerTag).HasMaxLength(16);
            e.HasOne(p => p.Snapshot)
             .WithMany(s => s.Players)
             .HasForeignKey(p => p.WarSnapshotId);
        });

        mb.Entity<Player>(e =>
        {
            e.HasIndex(p => p.TelegramUserId).IsUnique();
            e.HasIndex(p => p.PlayerTag);
            e.Property(p => p.PlayerTag).HasMaxLength(16);
            e.HasOne(p => p.Clan)
             .WithMany(c => c.Players)
             .HasForeignKey(p => p.ClanId)
             .IsRequired(false);
        });

        mb.Entity<Respect>(e =>
        {
            e.HasIndex(r => new { r.FromPlayerTag, r.DayUtc }).IsUnique(); // лимит «1 в сутки»
            e.HasIndex(r => new { r.ClanId, r.DayUtc });                   // топ дня по клану
            e.HasIndex(r => r.ToPlayerTag);                                // счётчики получателя
            e.Property(r => r.FromPlayerTag).HasMaxLength(16);
            e.Property(r => r.ToPlayerTag).HasMaxLength(16);
            e.Property(r => r.FromName).HasMaxLength(64);
            e.Property(r => r.ToName).HasMaxLength(64);
            e.Property(r => r.DayUtc).HasMaxLength(10);
        });

        mb.Entity<WarBattle>(e =>
        {
            e.HasIndex(b => new { b.ClanId, b.PlayerTag, b.BattleTimeUtc }).IsUnique();
            e.HasIndex(b => new { b.ClanId, b.SeasonId, b.SectionIndex });
            e.Property(b => b.PlayerTag).HasMaxLength(16);
            e.Property(b => b.PlayerName).HasMaxLength(64);
        });
    }
}
