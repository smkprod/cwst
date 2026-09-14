namespace ClanWarTracker.Domain.Enums;

/// <summary>
/// Формат турнира. В парном игроки регистрируются командой из двоих, и в сетке
/// стоит команда, а не человек — поэтому это свойство турнира, а не матча.
/// </summary>
public enum TournamentMode
{
    Solo = 0,
    Duo = 1,
}
