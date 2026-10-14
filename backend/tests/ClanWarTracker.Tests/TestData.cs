using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClanWarTracker.Tests;

/// <summary>Сборщики тестовых данных: бой игрока, бой из журнала API, база SQLite в памяти.</summary>
internal static class TestData
{
    public static readonly DateTime T0 = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <param name="result">1 - победа, -1 - поражение, 0 - ничья.</param>
    public static PlayerBattle Battle(int minute, int result, string type = "PvP", int crownsFor = 1,
        int crownsAgainst = 0, string tag = "#P1", string deck = "1,2,3,4,5,6,7,8") => new()
    {
        PlayerTag = tag,
        BattleTimeUtc = T0.AddMinutes(minute),
        Type = type,
        Result = result,
        CrownsFor = crownsFor,
        CrownsAgainst = crownsAgainst,
        DeckKey = deck,
        OppDeckKey = "11,12,13,14,15,16,17,18",
    };

    public static List<CrDeckCard> Deck(params int[] ids) =>
        ids.Select(id => new CrDeckCard { Id = Math.Abs(id), Name = $"c{id}", EvolutionLevel = id < 0 ? 1 : 0 }).ToList();

    public static CrRecentBattle Recent(int minute, string me, string opp, bool won, int[] myDeck, int[] oppDeck,
        string type = "PvP", int crownsFor = 1, int crownsAgainst = 0) => new()
    {
        BattleTimeUtc = T0.AddMinutes(minute),
        Type = type,
        Won = won,
        CrownsFor = crownsFor,
        CrownsAgainst = crownsAgainst,
        TeamTags = [me],
        OpponentTags = [opp],
        MyDeck = Deck(myDeck),
        OpponentDeck = Deck(oppDeck),
    };

    public static readonly int[] DeckA = [1, 2, 3, 4, 5, 6, 7, 8];
    public static readonly int[] DeckB = [11, 12, 13, 14, 15, 16, 17, 18];
}

/// <summary>
/// Настоящая схема на SQLite в памяти: запросы репозиториев проверяются тем же EF, что
/// и в проде (там Postgres, но LINQ → SQL у нас переносимый).
/// </summary>
internal sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;

    public TestDb()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = New();
        db.Database.EnsureCreated();
    }

    /// <summary>
    /// Новый контекст на ту же базу - как новый scope в приложении. Запрос с двумя
    /// коллекциями в Include без AsSplitQuery - ошибка, а не предупреждение: это то
    /// декартово произведение, что однажды вытянуло из базы 191 ГБ.
    /// </summary>
    public AppDbContext New() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options);

    public void Dispose() => _conn.Dispose();
}
