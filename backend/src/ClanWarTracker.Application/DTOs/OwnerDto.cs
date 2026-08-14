namespace ClanWarTracker.Application.DTOs;

/// <summary>Детальная сводка сервиса для панели владельца.</summary>
public record OwnerStatsDto(
    // Кланы
    int TotalClans,
    int ProClans,
    int FreeClans,
    int ChatsWithBot,
    int ActiveClans7d,          // есть свежие снимки войны за неделю
    int SilentClans,            // подключены, но данных за неделю нет

    // Пользователи
    int TotalLinkedUsers,
    int UsersWithClan,
    int UsersWithoutClan,
    int UsersWithUsername,      // сколько можно тегнуть в чате
    int InvitedUsers,           // пришли по реферальной ссылке

    // Рост (только по записям с известной датой — старые не считаем)
    int NewClans7d,
    int NewClans30d,
    int NewUsers7d,
    int NewUsers30d,
    int ClansWithKnownDate,     // честность: по скольким кланам дата вообще есть
    int UsersWithKnownDate,

    // Pro
    int ProExpiring7d,
    int ProExpired,             // были Pro, срок вышел
    int ProForever,             // Pro без срока

    // Вовлечённость
    int Respects7d,
    double AvgLinkedPerClan);

/// <summary>Строка списка кланов в панели владельца.</summary>
public record OwnerClanDto(
    int Id,
    string ClanTag,
    string Name,
    string Plan,                // "pro" | "free"
    DateTime? PlanExpiresAtUtc,
    int? DaysLeft,              // сколько дней Pro осталось; null — бессрочно/Free
    int LinkedPlayers,
    bool HasChat,
    DateTime? CreatedAtUtc,
    DateTime? LastActivityUtc,  // последний снимок войны
    bool IsActive);             // активность за последнюю неделю

/// <summary>Детали клана: с кем связаться и что происходит.</summary>
public record OwnerClanDetailDto(
    int Id,
    string ClanTag,
    string Name,
    string Plan,
    DateTime? PlanExpiresAtUtc,
    long TelegramChatId,
    int? TelegramMessageThreadId,
    DateTime? CreatedAtUtc,
    DateTime? LastActivityUtc,
    int ClanMemberCount,        // всего в клане по данным CR (0 — API недоступен)
    List<OwnerMemberDto> Members);

public record OwnerMemberDto(
    string PlayerTag,
    string Name,
    string? TelegramUsername,   // без @; null — не задан
    long? TelegramUserId,
    string? Role,               // "leader" | "coLeader" | "elder" | "member" | null
    bool IsLeader,              // лидер или соруководитель — с кем говорить о Pro
    DateTime? LinkedAtUtc);
