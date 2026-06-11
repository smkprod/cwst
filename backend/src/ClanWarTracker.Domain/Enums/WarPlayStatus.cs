namespace ClanWarTracker.Domain.Enums;

public enum WarPlayStatus
{
    Played,      // ✅ все 4 колоды сыграны
    TimeLeft,    // ⏳ ещё есть время (0–3 колоды, дедлайн не близко)
    NotPlayed    // ❌ не сыграл, времени почти не осталось
}
