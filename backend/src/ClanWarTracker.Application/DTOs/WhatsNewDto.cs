namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Персональная карточка «Что нового» — дельта с прошлого визита в Mini App.
/// Каждый вход начинается с нового факта лично про тебя (персонализация ленты:
/// «показывай пользователю то, что ему интересно» — Montag et al.).
/// </summary>
public record WhatsNewDto(
    bool IsFirstVisit,           // первый заход — показываем приветствие вместо дельт
    DateTime? LastVisitAtUtc,
    int FameDelta,               // сколько медалей набрал с прошлого визита
    int RankDelta,               // +N = поднялся на N мест (rank стал меньше)
    int Rank,
    int RespectsSince,           // сколько респектов получил с прошлого визита
    string? PassedByName,        // кто обошёл тебя в рейтинге, пока тебя не было
    int DecksLeftToday,          // сколько колод осталось сегодня (призыв к действию)
    List<string> BadgesEarned);  // значки, поднявшиеся в уровне с прошлого визита (ключи)
