namespace ClanWarTracker.Application.DTOs;

/// <param name="Title">null - название по умолчанию из перевода приложения.</param>
/// <param name="Status">upcoming, live или ended.</param>
public record ChallengeEventDto(string Id, string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc, string Status);

/// <param name="Streak">Текущая серия побед - сколько до бонусного билета.</param>
public record ChallengeRowDto(
    int Rank, string Name, string Tag, int Tickets, int Wins, int Losses, int Streak, int BestStreak, bool IsMe);

/// <param name="Linked">Тег привязан - можно вступать.</param>
/// <param name="Me">Своя строка, даже если она ниже показанного топа.</param>
public record ChallengeDto(
    ChallengeEventDto Event, bool Linked, bool Joined, ChallengeRowDto? Me,
    List<ChallengeRowDto> Leaders, int Participants, DateTime UpdatedUtc);
