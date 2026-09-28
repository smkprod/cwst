using System.Text.Json;
using System.Text.Json.Nodes;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>Одна сторона боя в подробностях.</summary>
/// <param name="PrincessHp">HP уцелевших принцесс-башен (снесённые API не присылает).</param>
/// <param name="TowerTroop">Башенное войско: id и игровой уровень.</param>
/// <param name="Cards">Восемь карт в порядке слотов: id, игровой уровень (0 - неизвестен), форма (0/1 эво/2 герой).</param>
public record MatchSide(
    int? KingHp,
    IReadOnlyList<int>? PrincessHp,
    int? StartingTrophies,
    int? TrophyChange,
    double? Leak,
    int? GlobalRank,
    string? ClanTag,
    string? ClanName,
    int? ClanBadge,
    (int Id, int Level)? TowerTroop,
    IReadOnlyList<(int Id, int Level, int Form)> Cards);

public record MatchDetail(string? GameMode, int? League, MatchSide Me, MatchSide Op);

/// <summary>
/// Подробности боя одной короткой строкой JSON (версия 1): башни, уровни, кубки,
/// клан соперника. Колонку на каждое поле не заводим - читаются они только
/// вместе, для отчёта о матче, а по ним никто не ищет.
///
/// Имён карт внутри нет: имя берём из справочника при показе, и так строка
/// укладывается в ~600 байт на бой.
/// </summary>
public static class MatchDetailCodec
{
    public const int Version = 1;

    public static MatchDetail? From(CrRecentBattle b)
    {
        if (b.Me is null || b.Opp is null) return null;
        return new MatchDetail(b.GameModeName, b.LeagueNumber, Side(b.Me, b.MyDeck), Side(b.Opp, b.OpponentDeck));
    }

    private static MatchSide Side(CrBattleSide s, IReadOnlyList<CrDeckCard> deck) => new(
        s.KingHp, s.PrincessHp, s.StartingTrophies, s.TrophyChange, s.ElixirLeaked, s.GlobalRank,
        s.ClanTag, s.ClanName, s.ClanBadgeId,
        s.TowerTroop is { } tt ? (tt.Id, tt.GameLevel) : null,
        deck.Select(c => (c.Id, c.GameLevel, c.Form)).ToList());

    public static string Encode(MatchDetail d)
    {
        var o = new JsonObject
        {
            ["v"] = Version,
            ["gm"] = d.GameMode,
            ["lg"] = d.League,
            ["me"] = EncodeSide(d.Me),
            ["op"] = EncodeSide(d.Op),
        };
        return o.ToJsonString();
    }

    private static JsonObject EncodeSide(MatchSide s)
    {
        var o = new JsonObject
        {
            ["k"] = s.KingHp,
            ["p"] = s.PrincessHp is null ? null : new JsonArray(s.PrincessHp.Select(x => (JsonNode?)x).ToArray()),
            ["st"] = s.StartingTrophies,
            ["tc"] = s.TrophyChange,
            ["lk"] = s.Leak is double l ? Math.Round(l, 2) : null,
            ["gr"] = s.GlobalRank,
            ["cl"] = s.ClanTag is null ? null : new JsonArray(s.ClanTag, s.ClanName, s.ClanBadge),
            ["tt"] = s.TowerTroop is { } tt ? new JsonArray(tt.Id, tt.Level) : null,
            ["c"] = new JsonArray(s.Cards.Select(c => (JsonNode?)new JsonArray(c.Id, c.Level, c.Form)).ToArray()),
        };
        return o;
    }

    /// <summary>null - строки нет, она битая или неизвестной версии: отчёт покажет только общие поля.</summary>
    public static MatchDetail? Decode(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject o) return null;
            if ((int?)o["v"] != Version) return null;
            if (DecodeSide(o["me"]) is not { } me || DecodeSide(o["op"]) is not { } op) return null;
            return new MatchDetail((string?)o["gm"], (int?)o["lg"], me, op);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static MatchSide? DecodeSide(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var cl = o["cl"] as JsonArray;
        var tt = o["tt"] as JsonArray;
        var cards = (o["c"] as JsonArray ?? new JsonArray())
            .OfType<JsonArray>()
            .Where(a => a.Count >= 3)
            .Select(a => ((int)a[0]!, (int)a[1]!, (int)a[2]!))
            .ToList();
        return new MatchSide(
            (int?)o["k"],
            (o["p"] as JsonArray)?.Select(x => (int)x!).ToList(),
            (int?)o["st"],
            (int?)o["tc"],
            (double?)o["lk"],
            (int?)o["gr"],
            cl is { Count: >= 1 } ? (string?)cl[0] : null,
            cl is { Count: >= 2 } ? (string?)cl[1] : null,
            cl is { Count: >= 3 } ? (int?)cl[2] : null,
            tt is { Count: >= 2 } ? ((int)tt[0]!, (int)tt[1]!) : null,
            cards);
    }
}
