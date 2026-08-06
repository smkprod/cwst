namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Витрина наград игрока: коллекция значков с уровнями и прогрессом до следующего.
/// Дизайн опирается на эффект владения (своя коллекция, которую жалко бросить) и
/// эффект Зейгарник (видимый незакрытый прогресс «до золота осталось 2» тянет вернуться).
/// </summary>
public record AchievementsDto(
    string PlayerTag,
    List<AchievementDto> Badges,
    int WeeksAnalyzed);          // на скольких неделях данных построено (честность: коллекция растёт с историей бота)

public record AchievementDto(
    string Key,                  // "streak" | "perfectDays" | "mvpWeeks" | "totalFame" | "warsPlayed"
    int Level,                   // 0 — ещё нет, 1 — бронза, 2 — серебро, 3 — золото
    int Value,                   // текущее значение (напр., 7 идеальных дней)
    int? NextAt,                 // порог следующего уровня; null — золото добыто
    int[] Thresholds);           // [бронза, серебро, золото] — фронт рисует шкалу
