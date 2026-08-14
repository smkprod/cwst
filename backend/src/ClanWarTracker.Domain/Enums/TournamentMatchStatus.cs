namespace ClanWarTracker.Domain.Enums;

public enum TournamentMatchStatus
{
    /// <summary>Один или оба соперника ещё не определены (ждём результат предыдущего раунда).</summary>
    Pending = 0,
    /// <summary>Оба соперника известны, матч можно играть.</summary>
    Ready = 1,
    /// <summary>Бай — соперник отсутствует, участник проходит автоматически.</summary>
    Bye = 2,
    Completed = 3,
}
