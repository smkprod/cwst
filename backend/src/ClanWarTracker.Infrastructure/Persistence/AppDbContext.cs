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
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentParticipant> TournamentParticipants => Set<TournamentParticipant>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();
    public DbSet<GameTournament> GameTournaments => Set<GameTournament>();
    public DbSet<WarBattle> WarBattles => Set<WarBattle>();
    public DbSet<Respect> Respects => Set<Respect>();
    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();
    public DbSet<PuzzleResult> PuzzleResults => Set<PuzzleResult>();
    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();

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

        mb.Entity<SentNotification>(e =>
        {
            // Уникальность — это и есть защита от повтора: вторая вставка того же
            // события упадёт на индексе, а не уйдёт вторым сообщением в чат.
            e.HasIndex(n => new { n.Kind, n.Key }).IsUnique();
            e.HasIndex(n => n.SentAtUtc);   // по нему чистим старое
            e.Property(n => n.Kind).HasMaxLength(32);
            e.Property(n => n.Key).HasMaxLength(200);
        });

        mb.Entity<ActivityDay>(e =>
        {
            // Одна строка на человека в день — она же защита от повторной записи
            e.HasIndex(a => new { a.PlayerId, a.DayUtc }).IsUnique();
            // Сводка считает по дням сразу по всем игрокам
            e.HasIndex(a => a.DayUtc);
            e.Property(a => a.DayUtc).HasMaxLength(10);
        });

        mb.Entity<PuzzleResult>(e =>
        {
            // Одна запись на игрока в день: она же защита от переигровки после промаха
            e.HasIndex(r => new { r.PlayerId, r.Day }).IsUnique();
            // Серия считается обходом дней игрока назад — нужен порядок по дню
            e.HasIndex(r => new { r.PlayerId, r.Day, r.Solved });
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

        mb.Entity<RecruitmentProfile>(e =>
        {
            e.HasIndex(r => r.PlayerTag).IsUnique();
            e.HasIndex(r => r.TelegramUserId).IsUnique();
            e.Property(r => r.PlayerTag).HasMaxLength(16);
            e.Property(r => r.Note).HasMaxLength(500);
            e.Property(r => r.Name).HasMaxLength(64);
        });

        mb.Entity<Tournament>(e =>
        {
            e.HasIndex(t => t.CreatorTelegramUserId);
            e.Property(t => t.Name).HasMaxLength(80);
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.PrizeInfo).HasMaxLength(500);
            e.Property(t => t.ClanInviteLink).HasMaxLength(300);
            e.Property(t => t.CreatorPlayerTag).HasMaxLength(16);
            e.Property(t => t.CreatorName).HasMaxLength(64);
        });

        mb.Entity<TournamentParticipant>(e =>
        {
            e.HasIndex(p => new { p.TournamentId, p.PlayerTag }).IsUnique();
            e.HasIndex(p => new { p.TournamentId, p.TelegramUserId }).IsUnique();
            e.Property(p => p.PlayerTag).HasMaxLength(16);
            e.Property(p => p.PlayerName).HasMaxLength(64);
            e.HasOne(p => p.Tournament)
             .WithMany(t => t.Participants)
             .HasForeignKey(p => p.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TournamentMatch>(e =>
        {
            e.HasIndex(m => new { m.TournamentId, m.Round, m.SlotIndex }).IsUnique();
            e.HasOne(m => m.Tournament)
             .WithMany(t => t.Matches)
             .HasForeignKey(m => m.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.ParticipantA)
             .WithMany()
             .HasForeignKey(m => m.ParticipantAId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.ParticipantB)
             .WithMany()
             .HasForeignKey(m => m.ParticipantBId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.WinnerParticipant)
             .WithMany()
             .HasForeignKey(m => m.WinnerParticipantId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.NextMatch)
             .WithMany()
             .HasForeignKey(m => m.NextMatchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<GameTournament>(e =>
        {
            e.HasIndex(t => t.TournamentTag).IsUnique();
            e.Property(t => t.TournamentTag).HasMaxLength(16);
            e.Property(t => t.Name).HasMaxLength(120);
            e.Property(t => t.Password).HasMaxLength(64);
            e.Property(t => t.CreatorName).HasMaxLength(64);
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
