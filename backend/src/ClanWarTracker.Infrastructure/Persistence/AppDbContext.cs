using ClanWarTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Clan> Clans => Set<Clan>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<WarSnapshot> WarSnapshots => Set<WarSnapshot>();
    public DbSet<PlayerWarSnapshot> PlayerWarSnapshots => Set<PlayerWarSnapshot>();
    public DbSet<RecruitmentProfile> RecruitmentProfiles => Set<RecruitmentProfile>();

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
             .HasForeignKey(p => p.ClanId);
        });

        mb.Entity<RecruitmentProfile>(e =>
        {
            e.HasIndex(r => r.PlayerTag).IsUnique();
            e.HasIndex(r => r.TelegramUserId).IsUnique();
            e.Property(r => r.PlayerTag).HasMaxLength(16);
            e.Property(r => r.Note).HasMaxLength(500);
            e.Property(r => r.Name).HasMaxLength(64);
        });
    }
}
