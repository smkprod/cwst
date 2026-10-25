using System.Reflection;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using ClanWarTracker.Infrastructure.Persistence;
using ClanWarTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Задания дня: что выпадает, как считаются по журналу, серия и награда за неё.</summary>
public class DailyQuestsTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Pick_IsStablePerDay_WarQuestOnlyForClanOnWarDays()
    {
        // 2026-10-29 - четверг, 2026-10-27 - вторник
        var thu = DailyQuestsUseCase.Pick("2026-10-29", 42, inClan: true);
        Assert.Equal(thu, DailyQuestsUseCase.Pick("2026-10-29", 42, inClan: true));
        Assert.Equal(DailyQuestsUseCase.War, thu[0]);
        Assert.True(DailyQuestsUseCase.IsApp(thu[2]));

        Assert.DoesNotContain(DailyQuestsUseCase.War, DailyQuestsUseCase.Pick("2026-10-29", 42, inClan: false));
        var tue = DailyQuestsUseCase.Pick("2026-10-27", 42, inClan: true);
        Assert.DoesNotContain(DailyQuestsUseCase.War, tue);
        Assert.Equal(3, tue.Distinct().Count());

        // У разных людей в один день задания расходятся - хотя бы у кого-то из десятка
        var sets = Enumerable.Range(1, 10).Select(tg => string.Join(",", DailyQuestsUseCase.Pick("2026-10-27", tg, true)));
        Assert.True(sets.Distinct().Count() > 1);
    }

    [Fact]
    public void Count_ReadsTheBattleLog()
    {
        List<PlayerBattle> own =
        [
            Battle(0, 1, crownsFor: 1, crownsAgainst: 1),
            Battle(5, 1, type: "pathOfLegend", crownsFor: 3),
            Battle(10, -1),
            Battle(15, 1, type: "challenge", crownsFor: 2, crownsAgainst: 0),
        ];
        Assert.Equal(2, DailyQuestsUseCase.Count(DailyQuestsUseCase.Win, own, []));     // испытание не в счёт
        Assert.Equal(4, DailyQuestsUseCase.Count(DailyQuestsUseCase.Play, own, []));
        Assert.Equal(1, DailyQuestsUseCase.Count(DailyQuestsUseCase.Crowns, own, []));
        Assert.Equal(1, DailyQuestsUseCase.Count(DailyQuestsUseCase.Flawless, own, []));
        Assert.Equal(2, DailyQuestsUseCase.Count(DailyQuestsUseCase.Streak, own, []));  // поражение обрывает серию
        Assert.Equal(1, DailyQuestsUseCase.Count(DailyQuestsUseCase.War, own,
            [new CrBattle { PlayerTag = "#P1", PlayerName = "P1", Type = "riverRaceDuel" }]));
    }

    [Fact]
    public void Rewards_ThirdDayAndEverySeventh()
    {
        Assert.Equal([0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 3],
            Enumerable.Range(1, 14).Select(DailyQuestsUseCase.RewardFor).ToArray());
        Assert.Equal((3, 1), DailyQuestsUseCase.NextReward(0));
        Assert.Equal((4, 3), DailyQuestsUseCase.NextReward(3));
        Assert.Equal((7, 3), DailyQuestsUseCase.NextReward(7));
    }

    [Fact]
    public void Window_IsTheLocalDayInUtc()
    {
        var (from, to) = DailyQuestsUseCase.Window("2026-10-27", 180);
        Assert.Equal(new DateTime(2026, 10, 26, 21, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(from.AddDays(1), to);
        Assert.Equal("2026-10-27", DailyQuestsUseCase.DayOf(new DateTime(2026, 10, 26, 21, 30, 0, DateTimeKind.Utc), 180));
    }

    [Fact]
    public async Task FullDay_ClosesByBattlesAndAppPing_ExtendsStreak_AndPaysOnThirdDay()
    {
        const long tg = 777;
        var now = DateTime.UtcNow;
        var tz = 180;
        var today = DailyQuestsUseCase.DayOf(now, tz);
        var (from, _) = DailyQuestsUseCase.Window(today, tz);
        var mid = from + (now - from) / 2;

        using (var db = _db.New())
        {
            var clan = new Clan { ClanTag = "#CLAN", Name = "Clan" };
            db.Clans.Add(clan);
            db.SaveChanges();
            db.Players.Add(new Player { PlayerTag = "#P1", Name = "Me", TelegramUserId = tg, ClanId = clan.Id });
            db.Players.Add(new Player { PlayerTag = "#P2", Name = "Mate", TelegramUserId = 778, ClanId = clan.Id });
            // Вчера и позавчера закрыты: сегодня - третий день серии
            for (var d = 1; d <= 2; d++)
                db.QuestDays.Add(new QuestDay
                {
                    TelegramUserId = tg, PlayerTag = "#P1", ClanId = clan.Id,
                    Day = DailyQuestsUseCase.DayOf(now.AddDays(-d), tz), Quests = "win2,crowns3,trainer", DoneMask = 7,
                    CompletedUtc = now.AddDays(-d), Streak = 3 - d, CreatedUtc = now.AddDays(-d),
                });
            // Пять побед с тремя коронами всухую - закрывают любое задание в игре
            for (var i = 0; i < 5; i++)
                db.PlayerBattles.Add(new PlayerBattle
                {
                    PlayerTag = "#P1", BattleTimeUtc = mid.AddSeconds(i), Type = "PvP", Result = 1, CrownsFor = 3,
                    CrownsAgainst = 0, DeckKey = "1,2,3,4,5,6,7,8", OppDeckKey = "11,12,13,14,15,16,17,18",
                });
            db.SaveChanges();
        }

        await using var ctx = _db.New();
        var war = Enumerable.Range(0, 2)
            .Select(i => new CrBattle { PlayerTag = "#P1", PlayerName = "Me", BattleTimeUtc = mid.AddMinutes(i), Type = "riverRacePvP" })
            .ToList();
        var uc = Build(ctx, war);

        var first = await uc.GetAsync(tg);
        Assert.NotNull(first);
        Assert.Equal(3, first.Items.Count);
        Assert.All(first.Items.Where(i => i.Kind != "app"), i => Assert.True(i.Done));
        Assert.False(first.Completed);
        Assert.True(first.AtRisk);
        Assert.Equal(2, first.Streak);
        Assert.Equal((1, 1), (first.NextRewardIn, first.NextRewardDays));

        // Не то задание из приложения - ничего не меняет
        var app = first.Items.Single(i => i.Kind == "app").Key;
        var other = DailyQuestsUseCase.AppKeys.First(k => k != app);
        Assert.False((await uc.PingAsync(tg, other))!.Completed);

        var done = await uc.PingAsync(tg, app);
        Assert.NotNull(done);
        Assert.True(done.Completed);
        Assert.True(done.JustCompleted);
        Assert.Equal(3, done.Streak);
        Assert.Equal(1, done.RewardDays);
        Assert.Equal(3, done.BestStreak);
        Assert.NotNull(done.Clan);
        Assert.Equal(1, done.Clan.DoneToday);
        Assert.Equal("Me", done.Clan.Top.Single().Name);

        // Повторный запрос не платит второй раз и не празднует заново
        var again = await uc.GetAsync(tg);
        Assert.False(again!.JustCompleted);
        await using var check = _db.New();
        Assert.Equal(1, await check.Entitlements.CountAsync(e => e.TelegramUserId == tg && e.Source == Entitlement.Sources.Quest));
    }

    [Fact]
    public async Task NotLinked_ReturnsNull()
    {
        await using var ctx = _db.New();
        Assert.Null(await Build(ctx, []).GetAsync(123));
    }

    private static DailyQuestsUseCase Build(AppDbContext ctx, List<CrBattle> war)
    {
        var api = Stub<IClashRoyaleApi>.Make(s => s.On["GetPlayerBattlelogAsync"] = _ => Task.FromResult(war));
        var players = new PlayerRepository(ctx);
        var settings = new ServiceSettingRepository(ctx);
        return new DailyQuestsUseCase(
            new QuestRepository(ctx), players, new PlayerAlertPrefsRepository(ctx), new PlayerBattleRepository(ctx),
            new CollectPlayerBattlesUseCase(api, players, new PlayerBattleRepository(ctx)), api, new ClanRepository(ctx),
            new PlusAccess(new EntitlementRepository(ctx), players, settings), new SentNotificationRepository(ctx),
            Stub<INotificationSender>.Make(), settings);
    }
}

/// <summary>
/// Заглушка интерфейса: что не задано - пустой ответ (пустой список, null, 0). Внешние
/// API в тестах не нужны целиком - только пара методов, которые трогает проверяемый код.
/// </summary>
public class Stub<T> : DispatchProxy where T : class
{
    public Dictionary<string, Func<object?[]?, object?>> On { get; } = new();

    public static T Make(Action<Stub<T>>? setup = null)
    {
        var proxy = Create<T, Stub<T>>();
        setup?.Invoke((Stub<T>)(object)proxy);
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        On.TryGetValue(method!.Name, out var f) ? f(args) : Default(method.ReturnType);

    private static object? Default(Type t)
    {
        if (t == typeof(void)) return null;
        if (t == typeof(Task)) return Task.CompletedTask;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = t.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [Empty(inner)]);
        }
        return Empty(t);
    }

    private static object? Empty(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (t.IsGenericType && t.GetGenericArguments().Length == 1 && t.IsAssignableFrom(typeof(List<>).MakeGenericType(t.GetGenericArguments())))
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(t.GetGenericArguments()));
        return null;
    }
}
