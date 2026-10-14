using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Infrastructure.Persistence.Repositories;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>
/// Запросы к базе. Именно здесь были самые дорогие баги: декартово произведение турниров
/// (191 ГБ из базы), «одна неделя», которая тянула весь сезон, вечный рост таблиц.
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private int SeedClan()
    {
        using var db = _db.New();
        var clan = new Clan { ClanTag = "#CLAN", Name = "Clan" };
        db.Clans.Add(clan);
        db.SaveChanges();
        return clan.Id;
    }

    private void SeedWeek(int clanId, int season, int section, string source = "live", int fame = 1000, int periods = 2)
    {
        using var db = _db.New();
        for (var p = 0; p < periods; p++)
            db.WarSnapshots.Add(new WarSnapshot
            {
                ClanId = clanId,
                SeasonId = season,
                SectionIndex = section,
                PeriodIndex = 3 + p,
                PeriodType = "warDay",
                Source = source,
                TotalFame = fame,
                CapturedAtUtc = T0,
                Players = [new PlayerWarSnapshot { PlayerTag = "#P1", Name = "P1", Fame = fame }],
            });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetByClan_LoadsExactlyRequestedWeeks()
    {
        var clan = SeedClan();
        SeedWeek(clan, 130, 0);
        SeedWeek(clan, 130, 1);
        SeedWeek(clan, 130, 2);
        SeedWeek(clan, 131, 0);

        await using var db = _db.New();
        var repo = new WarSnapshotRepository(db);

        var one = await repo.GetByClanAsync(clan, weeks: 1);
        Assert.All(one, s => Assert.Equal((131, 0), (s.SeasonId, s.SectionIndex)));
        Assert.Equal(2, one.Count);

        // Две недели через границу сезона: (131,0) и (130,2), но не (130,0..1)
        var two = await repo.GetByClanAsync(clan, weeks: 2);
        Assert.Equal([(130, 2), (131, 0)],
            two.Select(s => (s.SeasonId, s.SectionIndex)).Distinct().OrderBy(k => k).ToList());
        Assert.All(two, s => Assert.NotEmpty(s.Players));
    }

    [Fact]
    public async Task VerifiedWeeks_OnlyLogSourceWithFame()
    {
        var clan = SeedClan();
        SeedWeek(clan, 130, 0, source: "log");
        SeedWeek(clan, 130, 1, source: "live");
        SeedWeek(clan, 130, 2, source: "log", fame: 0);

        await using var db = _db.New();
        var weeks = await new WarSnapshotRepository(db).GetVerifiedWeeksAsync(clan);

        Assert.Equal([(130, 0)], weeks);
    }

    [Fact]
    public async Task TopPlayers_PurgeBefore_KeepsRecentDays()
    {
        await using (var db = _db.New())
        {
            foreach (var day in new[] { "2026-08-01", "2026-09-30", "2026-10-14" })
                db.TopPlayers.Add(new TopPlayer { DayUtc = day, Rank = 1, PlayerTag = "#T", Name = "T" });
            await db.SaveChangesAsync();
        }

        await using (var db = _db.New())
        {
            var repo = new TopPlayerRepository(db);
            Assert.Equal(1, await repo.PurgeBeforeAsync("2026-09-01"));
            Assert.Equal(["2026-10-14", "2026-09-30"], await repo.DaysAsync(10));
        }
    }

    [Fact]
    public async Task SentNotifications_PurgeKeepsOnceMarks()
    {
        await using (var db = _db.New())
        {
            db.SentNotifications.Add(new SentNotification { Kind = "report", Key = "old", SentAtUtc = T0.AddDays(-30) });
            db.SentNotifications.Add(new SentNotification { Kind = SentNotification.OncePrefix + "first", Key = "old", SentAtUtc = T0.AddDays(-30) });
            db.SentNotifications.Add(new SentNotification { Kind = "report", Key = "new", SentAtUtc = T0 });
            await db.SaveChangesAsync();
        }

        await using (var db = _db.New())
        {
            var repo = new SentNotificationRepository(db);
            await repo.PurgeOlderThanAsync(T0.AddDays(-14));
            Assert.Equal(["new"], await repo.GetKeysAsync("report", DateTime.MinValue));
            Assert.Single(await repo.GetKeysAsync(SentNotification.OncePrefix + "first", DateTime.MinValue));
        }
    }

    [Fact]
    public async Task BattlesForTags_WithoutHeavyDetail()
    {
        await using (var db = _db.New())
        {
            var b = Battle(0, 1, tag: "#A");
            b.DetailJson = new string('x', 5000);
            db.PlayerBattles.Add(b);
            db.PlayerBattles.Add(Battle(1, -1, tag: "#B"));
            db.PlayerBattles.Add(Battle(2, 1, tag: "#C"));
            await db.SaveChangesAsync();
        }

        await using (var db = _db.New())
        {
            var rows = await new PlayerBattleRepository(db).GetForTagsBetweenAsync(["#A", "#B"], T0, T0.AddHours(1));

            Assert.Equal(["#A", "#B"], rows.Select(r => r.PlayerTag));
            Assert.All(rows, r => Assert.Null(r.DetailJson));
            Assert.Equal(1, rows[0].Result);
        }
    }

    [Fact]
    public async Task AutoResultTournaments_LoadParticipantsAndMatches()
    {
        await using (var db = _db.New())
        {
            var t = new Tournament
            {
                Name = "Cup", CreatorPlayerTag = "#C", CreatorName = "C",
                AutoResults = true, Status = TournamentStatus.InProgress,
            };
            for (var i = 0; i < 8; i++) t.Participants.Add(new TournamentParticipant { PlayerTag = $"#P{i}", PlayerName = $"P{i}", TelegramUserId = 100 + i });
            db.Tournaments.Add(t);
            await db.SaveChangesAsync();
            for (var i = 0; i < 4; i++)
                t.Matches.Add(new TournamentMatch
                {
                    Round = 1,
                    SlotIndex = i,
                    ParticipantAId = t.Participants[i * 2].Id,
                    ParticipantBId = t.Participants[i * 2 + 1].Id,
                });
            await db.SaveChangesAsync();
        }

        await using (var db = _db.New())
        {
            var list = await new TournamentRepository(db).GetForAutoResultsAsync();

            var t = Assert.Single(list);
            Assert.Equal(8, t.Participants.Count);
            // Раздельные запросы, а не «участники × матчи»: ровно 4 матча, не 32
            Assert.Equal(4, t.Matches.Count);
            Assert.All(t.Matches, m => Assert.NotNull(m.ParticipantA));
        }
    }

    [Fact]
    public async Task DailyAttempts_StopsAfterLimitAndResetsNextDay()
    {
        await using var db = _db.New();
        var settings = new ServiceSettingRepository(db);

        for (var i = 0; i < 3; i++)
            Assert.True(await DailyAttempts.TryStartAsync(settings, "job", "2026-10-14", 3, default));
        Assert.False(await DailyAttempts.TryStartAsync(settings, "job", "2026-10-14", 3, default));

        Assert.True(await DailyAttempts.TryStartAsync(settings, "job", "2026-10-15", 3, default));
    }
}
