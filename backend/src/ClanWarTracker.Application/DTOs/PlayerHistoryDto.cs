namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// История войн игрока по данным сервиса: в каких кланах играл и сколько набивал.
/// Покрывает только кланы, подключённые к сервису (свои снапшоты);
/// полная история — по ссылке RoyaleApiUrl.
/// </summary>
public record PlayerHistoryDto(
    string PlayerTag,
    string RoyaleApiUrl,           // https://royaleapi.com/player/TAG — полный профиль
    List<PlayerWeekHistoryDto> Weeks);

public record PlayerWeekHistoryDto(
    int SeasonId,
    int SectionIndex,              // неделя внутри сезона (0..3)
    bool IsColosseum,
    string ClanTag,
    string ClanName,
    int Fame,                      // финальная слава игрока за неделю
    int DecksUsed,
    double AvgFamePerAttack,       // слава / военные колоды недели
    double ClanAvgFamePerAttack);  // средняя слава/колода по всему клану за ту же неделю
