namespace ClanWarTracker.Application.DTOs;

/// <param name="Title">null - название по умолчанию из перевода приложения.</param>
/// <param name="Status">upcoming, live или ended.</param>
/// <param name="GiftPlus">Участникам Плюс в подарок до конца челленджа.</param>
/// <param name="Code">Код челленджа блогера; null - общий челлендж.</param>
/// <param name="Host">Кто проводит челлендж блогера.</param>
public record ChallengeEventDto(string Id, string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc, string Status, bool GiftPlus = false,
    string? Code = null, string? Host = null);

/// <param name="Streak">Текущая серия побед - сколько до бонусного билета.</param>
public record ChallengeRowDto(
    int Rank, string Name, string Tag, int Tickets, int Wins, int Losses, int Streak, int BestStreak, bool IsMe);

/// <param name="Linked">Тег привязан - можно вступать.</param>
/// <param name="Me">Своя строка, даже если она ниже показанного топа.</param>
public record ChallengeDto(
    ChallengeEventDto Event, bool Linked, bool Joined, ChallengeRowDto? Me,
    List<ChallengeRowDto> Leaders, int Participants, DateTime UpdatedUtc);
