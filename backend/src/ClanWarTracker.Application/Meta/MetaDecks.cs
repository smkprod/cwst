namespace ClanWarTracker.Application.Meta;

/// <summary>
/// База актуальных колод. Официальное API Clash Royale колоды меты не отдаёт вообще —
/// оно знает только про конкретного игрока, — поэтому список собран вручную по открытым
/// источникам (RoyaleAPI Popular Decks, гайды и сезонные разборы) и обновляется правкой
/// этого файла. Здесь ТОЛЬКО имена карт: иконки, стоимость эликсира и наличие эволюции
/// подтягиваются из справочника /cards, чтобы данные не расходились с игрой.
///
/// Имена карт должны совпадать с API до символа. Колода с незнакомым именем отбрасывается
/// на старте (см. SuggestDecksUseCase) — лучше показать 49 колод, чем одну поломанную.
/// </summary>
public static class MetaDecks
{
    /// <summary>Подпись «по состоянию на» — видна в интерфейсе, чтобы никто не принял базу за живую.</summary>
    public const string UpdatedLabel = "Сезон 133 (август 2026)";

    public const string SourceNote = "Подборка по открытым источникам: RoyaleAPI, гайды сообщества";

    public record MetaDeck(
        string Id,
        string Name,
        string Archetype,
        string Note,
        string[] Cards);

    public static readonly IReadOnlyList<MetaDeck> All =
    [
        // --- Цикл и Хог: дёшево по редкости, поэтому доступно почти всем ---
        new("hog-2-6", "Hog 2.6", "Цикл",
            "Классика на все времена: копишь на пуш, давишь Хогом и разменом карт.",
            ["Hog Rider", "Musketeer", "Cannon", "Ice Golem", "Skeletons", "Ice Spirit", "Fireball", "The Log"]),

        new("hog-eq", "Hog Earthquake", "Цикл",
            "Землетрясение выносит здания под Хогом — работает даже против X-Bow и Могилы.",
            ["Hog Rider", "Earthquake", "Valkyrie", "Musketeer", "Cannon", "Ice Spirit", "Skeletons", "The Log"]),

        new("hog-firecracker", "Hog Firecracker", "Цикл",
            "Петарда чистит рой и поддерживает Хога с моста. Простой вход в цикловые колоды.",
            ["Hog Rider", "Firecracker", "Ice Golem", "Skeletons", "Ice Spirit", "Tesla", "Fireball", "The Log"]),

        new("hog-queen", "Archer Queen Hog", "Цикл",
            "Королева невидимкой добивает башню после размена. Нужен чемпион.",
            ["Archer Queen", "Hog Rider", "Ice Golem", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("berserker-hog", "Berserker Hog", "Цикл",
            "Берсерк держит мост и разгоняется — дешёвый цикл для быстрых атак.",
            ["Berserker", "Hog Rider", "Firecracker", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        // --- Приманка ---
        new("log-bait", "Log Bait", "Приманка",
            "Выманиваешь Бревно Гоблинской бочкой, потом Принцесса и Банда добивают.",
            ["Goblin Barrel", "Princess", "Knight", "Ice Spirit", "Goblin Gang", "Inferno Tower", "Rocket", "The Log"]),

        new("mortar-bait", "Mortar Bait", "Приманка",
            "Мортира давит, а мелочь заставляет тратить заклинания не туда.",
            ["Mortar", "Goblin Barrel", "Princess", "Knight", "Skeletons", "Bats", "Rocket", "The Log"]),

        new("wb-bait", "Wall Breakers Bait", "Приманка",
            "Подрывники проходят, пока соперник разбирается с Бочкой скелетов.",
            ["Wall Breakers", "Skeleton Barrel", "Firecracker", "Knight", "Bats", "Tesla", "Fireball", "The Log"]),

        new("rascals-bait", "Rascals Bait", "Приманка",
            "Ребята держат мост и приманивают сплэш — Бочка проходит следом.",
            ["Rascals", "Goblin Barrel", "Dart Goblin", "Knight", "Goblin Gang", "Inferno Tower", "Rocket", "The Log"]),

        new("bush-bait", "Suspicious Bush Bait", "Приманка",
            "Куст добавляет ещё одну цель для заклинаний — соперник просто не успевает.",
            ["Suspicious Bush", "Goblin Barrel", "Princess", "Knight", "Goblin Gang", "Inferno Tower", "Rocket", "The Log"]),

        // --- Осада ---
        new("xbow-3", "X-Bow 3.0", "Осада",
            "Ставишь Арбалет, держишь его дешёвой защитой и циклишь быстрее соперника.",
            ["X-Bow", "Tesla", "Archers", "Knight", "Skeletons", "Ice Spirit", "Fireball", "The Log"]),

        new("xbow-fisherman", "X-Bow Fisherman", "Осада",
            "Рыбак утаскивает танк с Арбалета — надёжнее против тяжёлых колод.",
            ["X-Bow", "Tesla", "Fisherman", "Archers", "Skeletons", "Ice Spirit", "Fireball", "The Log"]),

        new("mortar-cycle", "Mortar Cycle", "Осада",
            "Дешёвая осада: Мортира с моста плюс быстрый цикл на защиту.",
            ["Mortar", "Knight", "Archers", "Skeletons", "Ice Spirit", "Tesla", "Fireball", "The Log"]),

        // --- Битдаун ---
        new("golem-nw", "Golem Night Witch", "Битдаун",
            "Копишь эликсир Шахтой, за Големом идёт весь пуш. Ошибки не прощает.",
            ["Golem", "Night Witch", "Baby Dragon", "Mega Minion", "Lightning", "Tornado", "Barbarian Barrel", "Elixir Collector"]),

        new("lavaloon", "LavaLoon", "Битдаун",
            "Лава тянет урон, Шар доезжает до башни. Всё решает воздух.",
            ["Lava Hound", "Balloon", "Skeleton Dragons", "Mega Minion", "Guards", "Tombstone", "Fireball", "Arrows"]),

        new("lava-miner", "Lava Miner", "Битдаун",
            "Шахтёр добивает башню, пока соперник разбирается с Лавой.",
            ["Lava Hound", "Miner", "Skeleton Dragons", "Mega Minion", "Guards", "Zap", "Arrows", "Tombstone"]),

        new("giant-princes", "Giant Double Prince", "Битдаун",
            "Гигант впереди, два Принца сзади — прямой и понятный пуш.",
            ["Giant", "Prince", "Dark Prince", "Mega Minion", "Electro Wizard", "Zap", "Poison", "Elixir Collector"]),

        new("egiant", "Electro Giant Tornado", "Битдаун",
            "Торнадо стягивает войска в электрощит Гиганта — они сами себя убивают.",
            ["Electro Giant", "Tornado", "Lightning", "Dark Prince", "Bats", "Barbarian Barrel", "Electro Spirit", "Mother Witch"]),

        new("monk-egiant", "Monk Electro Giant", "Битдаун",
            "Монах отражает заклинания в соперника, Гигант доезжает.",
            ["Monk", "Electro Giant", "Tornado", "Lightning", "Bats", "Barbarian Barrel", "Electro Spirit", "Mother Witch"]),

        new("egolem-healer", "Elixir Golem Battle Healer", "Битдаун",
            "Эликсирный голем кормит соперника эликсиром, зато пуш идёт под хилом.",
            ["Elixir Golem", "Battle Healer", "Baby Dragon", "Electro Dragon", "Barbarian Barrel", "Tornado", "Rage", "Lumberjack"]),

        new("sparky-gg", "Goblin Giant Sparky", "Битдаун",
            "Спарки за спиной Гоблина-гиганта: один выстрел решает бой.",
            ["Goblin Giant", "Sparky", "Rage", "Zap", "Electro Wizard", "Bats", "Tornado", "Barbarian Barrel"]),

        // --- Мостовой спам ---
        new("pekka-bs", "PEKKA Bridge Spam", "Мостовой спам",
            "П.Е.К.К.А держит защиту, а Таран и Бандитка бьют с моста на контратаке.",
            ["P.E.K.K.A", "Battle Ram", "Bandit", "Royal Ghost", "Electro Wizard", "Zap", "Poison", "Minions"]),

        new("mk-ram", "Mega Knight Ram Rider", "Мостовой спам",
            "Прыжок Мега-рыцаря на защите, Наездница на баране в ответ. Прощает ошибки.",
            ["Mega Knight", "Ram Rider", "Electro Wizard", "Bandit", "Zap", "Arrows", "Skeletons", "Goblin Cage"]),

        new("mk-miner", "Mega Knight Miner", "Мостовой спам",
            "Мега-рыцарь чистит всё на защите, Шахтёр стабильно капает по башне.",
            ["Mega Knight", "Miner", "Firecracker", "Bats", "Skeletons", "Goblin Cage", "Zap", "Fireball"]),

        new("ram-ghost", "Ram Rider Bridge", "Мостовой спам",
            "Три карты на мост подряд — соперник не успевает отвечать на всё.",
            ["Ram Rider", "Royal Ghost", "Bandit", "Electro Wizard", "Fireball", "Barbarian Barrel", "Skeletons", "Goblin Cage"]),

        new("ebarbs-rage", "Elite Barbarians Rage", "Мостовой спам",
            "Элитные варвары под яростью. Грубо, зато быстро и почти без редких карт.",
            ["Elite Barbarians", "Rage", "Battle Ram", "Royal Ghost", "Zap", "Fireball", "Skeletons", "Goblin Cage"]),

        new("mighty-ram", "Mighty Miner Ram", "Мостовой спам",
            "Могучий шахтёр съедает любой танк, Наездница давит с другой стороны.",
            ["Mighty Miner", "Ram Rider", "Royal Ghost", "Electro Wizard", "Fireball", "Barbarian Barrel", "Skeletons", "Goblin Cage"]),

        new("boss-bandit", "Boss Bandit Spam", "Мостовой спам",
            "Босс-бандитка разгоняется через полкарты — идеально в контратаку.",
            ["Boss Bandit", "P.E.K.K.A", "Battle Ram", "Royal Ghost", "Electro Wizard", "Zap", "Poison", "Minions"]),

        // --- Королевский гигант и Королевские хряки ---
        new("rg-fisherman", "Royal Giant Fisherman", "Королевский гигант",
            "Ставишь Гиганта с моста и разбираешь всё, что к нему бежит.",
            ["Royal Giant", "Fisherman", "Hunter", "Electro Spirit", "Skeletons", "Barbarian Barrel", "Lightning", "Mother Witch"]),

        new("rg-phoenix", "Royal Giant Phoenix", "Королевский гигант",
            "Феникс возрождается и продолжает защищать — Гиганту хватает времени.",
            ["Royal Giant", "Phoenix", "Dart Goblin", "Guards", "Barbarian Barrel", "Tesla", "Electro Spirit", "Lightning"]),

        new("royal-hogs-recruits", "Royal Hogs Recruits", "Королевские хряки",
            "Давишь по всей ширине: Новобранцы спереди, Хряки по флангам.",
            ["Royal Hogs", "Royal Recruits", "Flying Machine", "Zappies", "Barbarian Barrel", "Arrows", "Royal Delivery", "Electro Spirit"]),

        new("royal-hogs-mother", "Royal Hogs Mother Witch", "Королевские хряки",
            "Ведьма-мать превращает защиту в свиней — и они бегут обратно на башню.",
            ["Royal Hogs", "Mother Witch", "Fireball", "Barbarian Barrel", "Electro Spirit", "Skeletons", "Tesla", "Flying Machine"]),

        new("rune-giant", "Rune Giant Recruits", "Королевский гигант",
            "Рунный гигант усиливает Новобранцев — стенка едет к башне сама.",
            ["Rune Giant", "Royal Recruits", "Flying Machine", "Zappies", "Barbarian Barrel", "Arrows", "Royal Delivery", "Electro Spirit"]),

        // --- Шахтёр и контроль ---
        new("miner-poison", "Miner Poison Control", "Контроль",
            "Копишь преимущество на защите, Шахтёр с Ядом добивают башню по чуть-чуть.",
            ["Miner", "Poison", "Bats", "Valkyrie", "Musketeer", "Inferno Tower", "Skeletons", "The Log"]),

        new("miner-wb", "Miner Wall Breakers", "Контроль",
            "Шахтёр забирает урон на себя, Подрывники добегают до башни.",
            ["Miner", "Wall Breakers", "Bomb Tower", "Valkyrie", "Bats", "Skeletons", "Fireball", "The Log"]),

        new("miner-drill", "Miner Goblin Drill", "Контроль",
            "Две карты, которые бьют мимо защиты. Соперник не знает, куда ставить войска.",
            ["Miner", "Goblin Drill", "Bats", "Valkyrie", "Musketeer", "Skeletons", "Earthquake", "The Log"]),

        new("miner-balloon", "Miner Balloon", "Контроль",
            "Шахтёр отвлекает башню, Шар прилетает следом.",
            ["Miner", "Balloon", "Bats", "Skeletons", "Musketeer", "Tombstone", "Barbarian Barrel", "Arrows"]),

        new("golden-miner", "Golden Knight Miner", "Контроль",
            "Золотой рыцарь пробегает сквозь рой, Шахтёр держит давление.",
            ["Golden Knight", "Miner", "Firecracker", "Bats", "Skeletons", "Goblin Cage", "Poison", "The Log"]),

        new("empress-miner", "Spirit Empress Miner", "Контроль",
            "Императрица закрывает воздух и рой, Шахтёр работает по башне.",
            ["Spirit Empress", "Miner", "Poison", "Bats", "Valkyrie", "Inferno Tower", "Skeletons", "The Log"]),

        // --- Могила ---
        new("gy-poison", "Graveyard Poison", "Могила",
            "Могила под Ядом на башню, которая уже занята обороной.",
            ["Graveyard", "Poison", "Knight", "Ice Wizard", "Bats", "Baby Dragon", "Barbarian Barrel", "Tombstone"]),

        new("splashyard", "Splashyard", "Могила",
            "Много сплэша: спокойно держишь оборону и ждёшь свою Могилу.",
            ["Graveyard", "Bowler", "Baby Dragon", "Tornado", "Ice Wizard", "Barbarian Barrel", "Poison", "Tombstone"]),

        new("sk-gy", "Skeleton King Graveyard", "Могила",
            "Король скелетов копит души на защите и выпускает армию с Могилой.",
            ["Skeleton King", "Graveyard", "Poison", "Ice Wizard", "Baby Dragon", "Tornado", "Barbarian Barrel", "Tombstone"]),

        new("giant-gy", "Giant Graveyard", "Могила",
            "Гигант тянет башню на себя, Могила работает вплотную. Просто и больно.",
            ["Giant", "Graveyard", "Poison", "Baby Dragon", "Bats", "Musketeer", "Barbarian Barrel", "Tombstone"]),

        // --- Шар ---
        new("balloon-freeze", "Balloon Freeze", "Шар",
            "Заморозка на защиту — и Шар успевает сделать полный круг ударов.",
            ["Balloon", "Freeze", "Lumberjack", "Baby Dragon", "Bats", "Barbarian Barrel", "Tombstone", "Musketeer"]),

        new("infernoloon", "Inferno Dragon Balloon", "Шар",
            "Дракон-инферно снимает любой танк, Шар наказывает за размен.",
            ["Inferno Dragon", "Balloon", "Lumberjack", "Barbarian Barrel", "Bats", "Musketeer", "Tombstone", "Arrows"]),

        new("lp-lavaloon", "Little Prince LavaLoon", "Шар",
            "Маленький принц вызывает Стража и держит воздух вместе с Лавой.",
            ["Little Prince", "Lava Hound", "Balloon", "Guards", "Tombstone", "Fireball", "Arrows", "Skeleton Dragons"]),

        // --- Гоблины и новые карты ---
        new("goblinstein", "Goblinstein Goblins", "Гоблины",
            "Чисто гоблинская колода: много мелочи и постоянное давление.",
            ["Goblinstein", "Goblin Giant", "Dart Goblin", "Goblin Gang", "Goblin Barrel", "Zap", "Barbarian Barrel", "Goblin Demolisher"]),

        new("goblin-machine", "Goblin Machine Control", "Гоблины",
            "Машина продавливает середину, Подрывник чистит оборону.",
            ["Goblin Machine", "Goblin Demolisher", "Firecracker", "Bats", "Skeletons", "Tesla", "Fireball", "The Log"]),

        new("three-musketeers", "Three Musketeers", "Разделение",
            "Разводишь Мушкетёрш по флангам под Шахту. Нужна дисциплина по эликсиру.",
            ["Three Musketeers", "Elixir Collector", "Royal Hogs", "Ice Golem", "Bandit", "Zap", "Barbarian Barrel", "Electro Spirit"]),

        new("giant-witch", "Giant Witch", "Битдаун",
            "Стартовая колода без легендарок: Гигант впереди, Ведьма сзади.",
            ["Giant", "Witch", "Musketeer", "Mini P.E.K.K.A", "Valkyrie", "Arrows", "Zap", "Cannon"]),

        // --- Ещё варианты популярных архетипов ---
        new("hog-mighty", "Mighty Miner Hog", "Цикл",
            "Могучий шахтёр съедает танк на защите, Хог давит в ответ.",
            ["Mighty Miner", "Hog Rider", "Firecracker", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("hog-cage", "Hog Goblin Cage", "Цикл",
            "Клетка тянет танк на себя, Хог уходит бить башню без сопровождения.",
            ["Hog Rider", "Goblin Cage", "Musketeer", "Ice Spirit", "Skeletons", "Barbarian Barrel", "Fireball", "Electro Spirit"]),

        new("hog-freeze", "Hog Freeze", "Цикл",
            "Заморозка на защитников — и Хог успевает снять полбашни.",
            ["Hog Rider", "Freeze", "Ice Golem", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("miner-mortar", "Miner Mortar", "Контроль",
            "Мортира заставляет отвечать, Шахтёр добивает то, что ей мешает.",
            ["Miner", "Mortar", "Bats", "Knight", "Skeletons", "Ice Spirit", "Rocket", "The Log"]),

        new("miner-eq", "Miner Earthquake", "Контроль",
            "Землетрясение сносит здания и рой, Шахтёр работает по башне.",
            ["Miner", "Earthquake", "Bats", "Knight", "Musketeer", "Inferno Tower", "Skeletons", "The Log"]),

        new("gy-cycle", "Graveyard Cycle", "Могила",
            "Дешёвая версия Могилы: циклишь быстрее и ставишь её чаще соперника.",
            ["Graveyard", "Poison", "Knight", "Musketeer", "Bats", "Ice Spirit", "Barbarian Barrel", "Tesla"]),

        new("gy-golden", "Golden Knight Graveyard", "Могила",
            "Золотой рыцарь пробегает сквозь защиту и расчищает место под Могилу.",
            ["Golden Knight", "Graveyard", "Poison", "Baby Dragon", "Bats", "Tornado", "Barbarian Barrel", "Tombstone"]),

        new("loon-cycle", "Balloon Cycle", "Шар",
            "Быстрый цикл вокруг Шара: он прилетает снова и снова.",
            ["Balloon", "Firecracker", "Ice Golem", "Skeletons", "Ice Spirit", "Tesla", "Fireball", "The Log"]),

        new("lava-clone", "LavaLoon Clone", "Битдаун",
            "Клон удваивает Шар и Лаву в момент, когда защиту уже потратили.",
            ["Lava Hound", "Balloon", "Clone", "Skeleton Dragons", "Mega Minion", "Guards", "Fireball", "Arrows"]),

        new("golem-lightning", "Golem Lightning", "Битдаун",
            "Молния выносит защитные постройки и мушкетёрш перед Големом.",
            ["Golem", "Baby Dragon", "Mega Minion", "Electro Wizard", "Lightning", "Tornado", "Barbarian Barrel", "Elixir Collector"]),

        new("giant-sparky", "Giant Sparky", "Битдаун",
            "Гигант держит удар, Спарки за ним сносит всё одним выстрелом.",
            ["Giant", "Sparky", "Rage", "Zap", "Electro Wizard", "Bats", "Tornado", "Barbarian Barrel"]),

        new("gg-witch", "Goblin Giant Witch", "Битдаун",
            "Гоблин-гигант и Ведьма создают постоянный поток мелочи к башне.",
            ["Goblin Giant", "Witch", "Baby Dragon", "Mega Minion", "Zap", "Fireball", "Barbarian Barrel", "Tombstone"]),

        new("rg-cart", "Royal Giant Cannon Cart", "Королевский гигант",
            "Телега держит мост и добивает то, что осталось после Гиганта.",
            ["Royal Giant", "Cannon Cart", "Hunter", "Electro Spirit", "Skeletons", "Barbarian Barrel", "Fireball", "Mother Witch"]),

        new("rg-furnace", "Royal Giant Furnace", "Королевский гигант",
            "Печка постоянно давит на башню, Гигант доводит дело до конца.",
            ["Royal Giant", "Furnace", "Musketeer", "Guards", "Barbarian Barrel", "Fireball", "Electro Spirit", "Tesla"]),

        new("xbow-ghost", "X-Bow Royal Ghost", "Осада",
            "Призрак защищает Арбалет и незаметно наказывает за перегруз.",
            ["X-Bow", "Tesla", "Royal Ghost", "Archers", "Skeletons", "Ice Spirit", "Fireball", "The Log"]),

        new("pekka-magic", "PEKKA Magic Archer", "Мостовой спам",
            "Волшебный лучник простреливает всю линию, П.Е.К.К.А закрывает защиту.",
            ["P.E.K.K.A", "Battle Ram", "Bandit", "Magic Archer", "Royal Ghost", "Zap", "Poison", "Electro Wizard"]),

        new("mk-bridge", "Mega Knight Bridge", "Мостовой спам",
            "Прыжок Мега-рыцаря на мост сразу после размена — просто и больно.",
            ["Mega Knight", "Bandit", "Royal Ghost", "Electro Wizard", "Zap", "Fireball", "Skeletons", "Battle Ram"]),

        new("lj-ram", "Lumberjack Battle Ram", "Мостовой спам",
            "Дровосек роняет ярость на Таран — тот доезжает почти всегда.",
            ["Lumberjack", "Battle Ram", "Royal Ghost", "Electro Wizard", "Zap", "Fireball", "Skeletons", "Goblin Cage"]),

        new("ram-mother", "Ram Rider Mother Witch", "Мостовой спам",
            "Защита соперника превращается в свиней и бежит обратно к его башне.",
            ["Ram Rider", "Mother Witch", "Valkyrie", "Electro Spirit", "Skeletons", "Barbarian Barrel", "Fireball", "Tesla"]),

        new("exe-tornado", "Executioner Tornado", "Контроль",
            "Торнадо стягивает войска под топор Палача, Хог давит следом.",
            ["Executioner", "Tornado", "Hog Rider", "Ice Golem", "Skeletons", "Musketeer", "Fireball", "The Log"]),

        new("magic-miner", "Magic Archer Miner", "Контроль",
            "Лучник простреливает насквозь, Шахтёр капает по башне каждый цикл.",
            ["Magic Archer", "Miner", "Poison", "Bats", "Valkyrie", "Inferno Tower", "Skeletons", "The Log"]),

        new("hunter-hogs", "Hunter Royal Hogs", "Королевские хряки",
            "Охотник вблизи сносит всё, Хряки идут по двум флангам.",
            ["Hunter", "Fisherman", "Royal Hogs", "Electro Spirit", "Skeletons", "Barbarian Barrel", "Fireball", "Tesla"]),

        new("drill-cycle", "Goblin Drill Cycle", "Контроль",
            "Бур появляется там, где защиты нет, и заставляет тратить карты впустую.",
            ["Goblin Drill", "Valkyrie", "Musketeer", "Bats", "Skeletons", "Fireball", "The Log", "Tesla"]),

        new("sk-hog", "Skeleton King Hog", "Цикл",
            "Король скелетов копит души на защите и выпускает армию под Хога.",
            ["Skeleton King", "Hog Rider", "Firecracker", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("queen-gy", "Archer Queen Graveyard", "Могила",
            "Королева невидимкой прикрывает Могилу и добивает башню.",
            ["Archer Queen", "Graveyard", "Poison", "Knight", "Bats", "Baby Dragon", "Barbarian Barrel", "Tombstone"]),

        new("monk-hog", "Monk Hog", "Цикл",
            "Монах отражает заклинания обратно, Хог работает по башне.",
            ["Monk", "Hog Rider", "Firecracker", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("lp-mortar", "Little Prince Mortar", "Осада",
            "Маленький принц со Стражем держит Мортиру дольше, чем сопернику удобно.",
            ["Little Prince", "Mortar", "Knight", "Archers", "Skeletons", "Ice Spirit", "Fireball", "The Log"]),

        new("empress-hog", "Spirit Empress Hog", "Цикл",
            "Императрица закрывает воздух и рой, Хог заходит без помех.",
            ["Spirit Empress", "Hog Rider", "Firecracker", "Skeletons", "Ice Spirit", "Cannon", "Fireball", "The Log"]),

        new("golden-drill", "Golden Knight Drill", "Контроль",
            "Бур и рывок Золотого рыцаря бьют туда, где защиты уже не осталось.",
            ["Golden Knight", "Goblin Drill", "Bats", "Valkyrie", "Musketeer", "Skeletons", "Earthquake", "The Log"]),

        // --- Стартовые колоды: без легендарок и чемпионов, собираются на низких аренах ---
        new("starter-hog", "Хог для старта", "Стартовая",
            "Всё дешёвое и обычное: Хог бьёт, остальное защищает. Хороший первый вариант.",
            ["Hog Rider", "Musketeer", "Valkyrie", "Cannon", "Arrows", "Fireball", "Skeletons", "Bomber"]),

        new("starter-giant", "Гигант с волшебником", "Стартовая",
            "Гигант впереди, Волшебник сзади — понятная схема без редких карт.",
            ["Giant", "Wizard", "Musketeer", "Mini P.E.K.K.A", "Valkyrie", "Arrows", "Zap", "Cannon"]),

        new("starter-rg", "Королевский гигант для старта", "Стартовая",
            "Ставишь Гиганта у моста и защищаешь его — работает с самых низких арен.",
            ["Royal Giant", "Musketeer", "Valkyrie", "Mini P.E.K.K.A", "Arrows", "Fireball", "Skeletons", "Tesla"]),

        new("starter-barbs", "Варвары и Гигант", "Стартовая",
            "Варвары держат любую атаку, Гигант ведёт свою. Ничего сложного.",
            ["Barbarians", "Giant", "Musketeer", "Valkyrie", "Arrows", "Fireball", "Skeletons", "Bomber"]),

        new("starter-balloon", "Гигант и Шар", "Стартовая",
            "Гигант забирает внимание башни, Шар прилетает следом.",
            ["Balloon", "Giant", "Musketeer", "Valkyrie", "Arrows", "Zap", "Skeletons", "Tombstone"]),

        new("starter-prince", "Гигант и Принц", "Стартовая",
            "Принц за Гигантом — самый прямой способ снести башню на низких аренах.",
            ["Prince", "Giant", "Musketeer", "Valkyrie", "Arrows", "Fireball", "Skeletons", "Cannon"]),

        new("starter-hut", "Ведьма и хижина", "Стартовая",
            "Постоянный поток скелетов и гоблинов: соперник не успевает чистить.",
            ["Witch", "Goblin Hut", "Giant", "Musketeer", "Arrows", "Fireball", "Valkyrie", "Cannon"]),

        new("starter-mortar", "Мортира для старта", "Стартовая",
            "Дешёвая осада на обычных картах — учит считать эликсир.",
            ["Mortar", "Knight", "Archers", "Skeletons", "Arrows", "Fireball", "Bomber", "Musketeer"]),

        new("starter-ebarbs", "Элитные варвары", "Стартовая",
            "Королевский гигант тянет защиту, Элитные варвары добивают.",
            ["Elite Barbarians", "Royal Giant", "Musketeer", "Valkyrie", "Arrows", "Fireball", "Skeletons", "Tesla"]),

        new("starter-horde", "Орда миньонов", "Стартовая",
            "Орда сносит любой танк, Гигант ведёт атаку. Всё карты обычные.",
            ["Minion Horde", "Giant", "Musketeer", "Valkyrie", "Arrows", "Zap", "Skeletons", "Cannon"]),

        new("starter-dragon", "Гигант и малыш дракон", "Стартовая",
            "Малыш дракон закрывает рой, Мини П.Е.К.К.А — танков. Универсально.",
            ["Giant", "Mini P.E.K.K.A", "Musketeer", "Baby Dragon", "Valkyrie", "Arrows", "Zap", "Cannon"]),
    ];
}
