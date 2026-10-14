using ClanWarTracker.Application.Analytics;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>Метки источника и ссылки привязки/подключения: на них держится сводка «откуда заходят».</summary>
public class EngagementTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("", "menu", "home")]
    [InlineData(null, "menu", "home")]
    [InlineData("home-c", "chat", "home")]
    [InlineData("review-c", "chat", "review")]
    [InlineData("plus_trk-d", "dm", "plus_trk")]
    [InlineData("m_12345-d", "dm", "m")]
    [InlineData("home-b", "bot", "home")]
    [InlineData("builder-k", "channel", "builder")]
    [InlineData("ch_ABC123-s", "share", "ch")]
    [InlineData("ch_ABC123", "link", "ch")]
    [InlineData("link_2PYGQV-c", "chat", "link")]
    [InlineData("matches-d", "dm", "matches")]
    [InlineData("challenge", "link", "challenge")]
    [InlineData("<script>-c", "chat", "other")]
    public void SourceAndDestFromStartParam(string? raw, string source, string dest)
    {
        Assert.Equal((source, dest), OpenSource.Parse(raw));
    }

    [Fact]
    public void Tag_RoundTripsThroughParse()
    {
        Assert.Equal("home-c", OpenSource.Tag(null, OpenSource.For(-100123)));
        Assert.Equal("review-d", OpenSource.Tag("review", OpenSource.For(42)));
        Assert.Equal((OpenSource.Dm, "review"), OpenSource.Parse(OpenSource.Tag("review", "d")));
    }

    [Fact]
    public void LinkAndConnectParams_FitTelegramLimits()
    {
        // startapp/startgroup: только [A-Za-z0-9_-], до 64 символов
        var link = InviteUnlinkedUseCase.LinkParam("#2pyg0qv");
        var connect = ChatConnectReminderUseCase.ConnectUrl("#2pyg0qv");
        Assert.Equal("link_2PYG0QV", link);
        Assert.Equal("startgroup:setup_2PYG0QV", connect);
        Assert.Matches("^[A-Za-z0-9_-]{1,64}$", OpenSource.Tag(link, "c"));
    }

    [Fact]
    public async Task AppOpens_OneRowPerUserDaySourceDest()
    {
        await using var db = _db.New();
        var repo = new AppOpenRepository(db);
        AppOpen Open(string source) => new()
        {
            TelegramUserId = 1, DayUtc = "2026-10-14", Source = source, Dest = "home", FirstUtc = TestData.T0,
        };

        await repo.AddAsync(Open("chat"));
        await repo.AddAsync(Open("chat"));
        await repo.AddAsync(Open("dm"));

        Assert.Equal(2, (await repo.GetSinceAsync("2026-10-01")).Count);
    }

    [Fact]
    public async Task Engagement_SourcesAndRetention()
    {
        var today = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = _db.New())
        {
            var clan = new Clan { ClanTag = "#C", Name = "C" };
            db.Clans.Add(clan);
            db.SaveChanges();
            // Два новичка 10 дней назад: один вернулся на следующий день, второй - нет
            for (var i = 0; i < 2; i++)
                db.Players.Add(new Player { PlayerTag = $"#P{i}", Name = $"P{i}", ClanId = clan.Id, TelegramUserId = 100 + i });
            db.SaveChanges();
            var ids = db.Players.Select(p => p.Id).ToList();
            void Day(int player, int back) => db.ActivityDays.Add(new ActivityDay
                { PlayerId = player, DayUtc = today.AddDays(-back).ToString("yyyy-MM-dd"), FirstSeenUtc = today });
            Day(ids[0], 10); Day(ids[0], 9); Day(ids[0], 1);
            Day(ids[1], 10);

            db.AppOpens.Add(new AppOpen { TelegramUserId = 100, DayUtc = "2026-10-13", Source = "chat", Dest = "review", Linked = true, FirstUtc = today });
            db.AppOpens.Add(new AppOpen { TelegramUserId = 200, DayUtc = "2026-10-01", Source = "chat", Dest = "link", Linked = false, FirstUtc = today });
            db.SaveChanges();
        }

        await using (var db = _db.New())
        {
            var r = await new GetEngagementUseCase(new AppOpenRepository(db), new ActivityRepository(db)).ExecuteAsync(today);

            var chat = Assert.Single(r.Sources);
            Assert.Equal(("chat", 1, 2, 1), (chat.Source, chat.Users7, chat.Users30, chat.NewUsers30));
            Assert.Equal(2, r.Retention.Cohort);
            Assert.Equal(1, r.Retention.NextDay);
            Assert.Equal(50, r.Retention.NextDayPercent);
            Assert.Equal(1, r.Wau);
            Assert.Equal("2026-10-01", r.TrackingSince);
        }
    }
}
