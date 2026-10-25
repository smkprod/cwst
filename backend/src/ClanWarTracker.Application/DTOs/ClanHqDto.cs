namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// «Штаб клана» (Клан Про, видят глава, замы и спонсор): кто из реальных игроков тянет
/// войну вниз и почему, кто держит клан, плюс дисциплина - кто пропускает, кого пинают
/// и кто доигрывает в последний час.
/// </summary>
public record ClanHqDto(
    int WeeksAnalyzed,
    int ClanFamePerDeck,                 // медиана медалей за колоду по реальным игрокам
    List<HqPlayerDto> Attention,         // худшие по войне, самые проблемные первыми
    List<HqPlayerDto> Pillars,           // на ком держится клан
    int Excluded,                        // сколько не взяли: новички и заходящие на одну войну
    ClanDisciplineDto Discipline);

/// <summary>
/// Игрок в штабе. Причины - коды, слова собирает фронт: low_fame (берёт мало медалей
/// за колоду), losing (проигрывает больше, чем выигрывает), skips (недоигрывает колоды),
/// late (тянет до последнего часа), falling (прошлая неделя хуже его обычной).
/// </summary>
public record HqPlayerDto(
    string Tag,
    string Name,
    string? Role,
    int Weeks,                           // недель в составе из разобранных
    int Decks,                           // военных колод за них
    int DecksPossible,                   // сколько можно было сыграть
    int FamePerDeck,
    int LastWeekFamePerDeck,             // 0 - на прошлой неделе не играл
    int? WinRate,                        // % побед в войне; null - мало боёв
    int LastMinute,                      // боёв в последний час дня
    List<string> Reasons);
