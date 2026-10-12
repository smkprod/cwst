namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Роли карт для конструктора колод: победная карта, танк, ПВО, сплеш, заклинания…
/// API игры таких свойств не отдаёт, поэтому основа - таблица ниже, а карты, которых в
/// ней нет (вышли позже), размечает нейросеть по тому же фиксированному списку ролей
/// (CardKnowledgeUseCase). Без разметки - запасные правила по типу карты.
/// </summary>
public static class CardRoles
{
    public const string Win = "win";
    public const string Tank = "tank";
    public const string MiniTank = "minitank";
    public const string Air = "air";
    public const string AntiAir = "antiair";
    public const string Splash = "splash";
    public const string Dps = "dps";
    public const string Swarm = "swarm";
    public const string Building = "building";
    public const string SmallSpell = "smallspell";
    public const string BigSpell = "bigspell";

    public static readonly string[] All =
        [Win, Tank, MiniTank, Air, AntiAir, Splash, Dps, Swarm, Building, SmallSpell, BigSpell];

    /// <summary>Роли известных карт - по имени из справочника игры (английскому).</summary>
    private static readonly Dictionary<string, string[]> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Победные карты и танки
        ["Giant"] = [Win, Tank], ["Golem"] = [Win, Tank], ["Royal Giant"] = [Win, Tank],
        ["Lava Hound"] = [Win, Tank, Air], ["Electro Giant"] = [Win, Tank], ["Elixir Golem"] = [Win, Tank],
        ["Goblin Giant"] = [Win, Tank, AntiAir], ["Hog Rider"] = [Win], ["Balloon"] = [Win, Air],
        ["Miner"] = [Win], ["Battle Ram"] = [Win], ["Ram Rider"] = [Win], ["Wall Breakers"] = [Win],
        ["Royal Hogs"] = [Win, Swarm], ["Skeleton Barrel"] = [Win, Air], ["Prince"] = [Win, Dps],
        ["Goblin Barrel"] = [Win], ["Graveyard"] = [Win], ["Goblin Drill"] = [Win, Building],
        ["X-Bow"] = [Win, Building, AntiAir], ["Mortar"] = [Win, Building, Splash],
        ["Three Musketeers"] = [Win, AntiAir], ["Mega Knight"] = [Tank, Splash], ["P.E.K.K.A"] = [Tank, Dps],
        ["Giant Skeleton"] = [Tank, Splash], ["Rune Giant"] = [Tank],

        // Мини-танки и бойцы
        ["Knight"] = [MiniTank], ["Valkyrie"] = [MiniTank, Splash], ["Dark Prince"] = [MiniTank, Splash],
        ["Ice Golem"] = [MiniTank], ["Bandit"] = [MiniTank, Dps], ["Fisherman"] = [MiniTank],
        ["Battle Healer"] = [MiniTank], ["Monk"] = [MiniTank], ["Royal Ghost"] = [MiniTank, Splash],
        ["Mini P.E.K.K.A"] = [Dps], ["Lumberjack"] = [Dps], ["Elite Barbarians"] = [Dps],
        ["Night Witch"] = [Dps], ["Cannon Cart"] = [Dps], ["Mighty Miner"] = [Dps, MiniTank],
        ["Golden Knight"] = [Dps, MiniTank], ["Berserker"] = [Dps], ["Barbarians"] = [Swarm, MiniTank],
        ["Royal Recruits"] = [Swarm, MiniTank], ["Skeleton King"] = [Tank, Splash],

        // ПВО и поддержка
        ["Archers"] = [AntiAir], ["Musketeer"] = [AntiAir], ["Dart Goblin"] = [AntiAir],
        ["Hunter"] = [AntiAir, Dps], ["Electro Wizard"] = [AntiAir], ["Zappies"] = [AntiAir],
        ["Mother Witch"] = [AntiAir], ["Electro Spirit"] = [AntiAir], ["Ice Spirit"] = [AntiAir],
        ["Archer Queen"] = [AntiAir, Dps], ["Little Prince"] = [AntiAir], ["Rascals"] = [AntiAir, MiniTank],
        ["Spear Goblins"] = [AntiAir, Swarm], ["Goblin Gang"] = [AntiAir, Swarm],
        ["Firecracker"] = [AntiAir, Splash], ["Wizard"] = [AntiAir, Splash], ["Ice Wizard"] = [AntiAir, Splash],
        ["Witch"] = [AntiAir, Splash], ["Executioner"] = [AntiAir, Splash], ["Princess"] = [AntiAir, Splash],
        ["Magic Archer"] = [AntiAir, Splash], ["Fire Spirit"] = [AntiAir, Splash], ["Bomber"] = [Splash],
        ["Bowler"] = [Splash], ["Sparky"] = [Splash, Dps], ["Heal Spirit"] = [AntiAir],

        // Летающие
        ["Minions"] = [Air, AntiAir, Swarm], ["Minion Horde"] = [Air, AntiAir, Swarm], ["Bats"] = [Air, AntiAir, Swarm],
        ["Mega Minion"] = [Air, AntiAir], ["Baby Dragon"] = [Air, AntiAir, Splash], ["Inferno Dragon"] = [Air, AntiAir, Dps],
        ["Electro Dragon"] = [Air, AntiAir, Splash], ["Skeleton Dragons"] = [Air, AntiAir, Splash],
        ["Flying Machine"] = [Air, AntiAir], ["Phoenix"] = [Air, AntiAir, Splash],

        // Рой
        ["Skeletons"] = [Swarm], ["Skeleton Army"] = [Swarm], ["Goblins"] = [Swarm], ["Guards"] = [Swarm],

        // Здания
        ["Cannon"] = [Building], ["Tesla"] = [Building, AntiAir], ["Inferno Tower"] = [Building, AntiAir, Dps],
        ["Bomb Tower"] = [Building, Splash], ["Goblin Cage"] = [Building], ["Tombstone"] = [Building, Swarm],
        ["Furnace"] = [Building, Splash], ["Goblin Hut"] = [Building, AntiAir], ["Barbarian Hut"] = [Building],
        ["Elixir Collector"] = [Building],

        // Заклинания
        ["Zap"] = [SmallSpell, AntiAir], ["The Log"] = [SmallSpell], ["Arrows"] = [SmallSpell, AntiAir, Splash],
        ["Barbarian Barrel"] = [SmallSpell], ["Giant Snowball"] = [SmallSpell, AntiAir], ["Royal Delivery"] = [SmallSpell],
        ["Tornado"] = [SmallSpell, AntiAir], ["Earthquake"] = [SmallSpell], ["Rage"] = [SmallSpell],
        ["Freeze"] = [SmallSpell], ["Goblin Curse"] = [SmallSpell], ["Clone"] = [SmallSpell], ["Mirror"] = [SmallSpell],
        ["Fireball"] = [BigSpell, AntiAir, Splash], ["Poison"] = [BigSpell, AntiAir, Splash],
        ["Lightning"] = [BigSpell, AntiAir], ["Rocket"] = [BigSpell, AntiAir], ["Void"] = [BigSpell, AntiAir],
    };

    /// <summary>
    /// Роли карты: из таблицы, затем из разметки нейросети, затем по типу (здание - по id
    /// 27…, заклинание - по id 28… и стоимости). Пусто - карта неизвестна.
    /// </summary>
    public static string[] For(int id, string name, int elixir, IReadOnlyDictionary<string, string[]>? learned)
    {
        if (Known.TryGetValue(name, out var roles)) return roles;
        if (learned is not null && learned.TryGetValue(name, out var ai) && ai.Length > 0) return ai;
        if (id is >= 27000000 and < 28000000) return [Building];
        if (id is >= 28000000 and < 29000000) return [elixir >= 4 ? BigSpell : SmallSpell];
        return [];
    }

    /// <summary>Карта есть в таблице - нейросеть о ней спрашивать не нужно.</summary>
    public static bool IsKnown(string name) => Known.ContainsKey(name);
}
