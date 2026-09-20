using System.Collections.Concurrent;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Темп опроса матчей для автозачёта: когда в следующий раз смотреть каждый матч.
///
/// Одна частота на всех не годится в обе стороны. Пара, которая играет прямо сейчас,
/// ждёт результат секунды — десять минут ожидания сводят на нет всю затею. Матч, до
/// которого никто не дошёл третий час, теми же десятью минутами просто жжёт лимит
/// токена впустую. Поэтому частота своя у каждого матча и зависит от того, что мы
/// о нём уже знаем.
///
/// Состояние живёт в памяти и намеренно не сохраняется: после перезапуска все матчи
/// начинают с быстрого темпа. Это безопасная сторона ошибки — лишние запросы против
/// пропущенного результата.
/// </summary>
public class TournamentPollState
{
    /// <summary>Пара играет прямо сейчас: только что сошлись или уже есть сыгранные бои.</summary>
    private static readonly TimeSpan Hot = TimeSpan.FromSeconds(30);

    /// <summary>Сошлись недавно, но ещё не начали.</summary>
    private static readonly TimeSpan Warm = TimeSpan.FromMinutes(2);

    /// <summary>Ждут уже часы — скорее всего, играют не сегодня.</summary>
    private static readonly TimeSpan Cool = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Cold = TimeSpan.FromMinutes(15);

    /// <summary>Сколько после готовности матч считается «вот-вот начнут».</summary>
    private static readonly TimeSpan JustReady = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan SameEvening = TimeSpan.FromHours(2);
    private static readonly TimeSpan SameDay = TimeSpan.FromHours(8);

    /// <summary>Записи, которых давно не касались, удаляем — иначе словарь растёт вечно.</summary>
    private static readonly TimeSpan Forgettable = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<int, DateTime> _nextCheck = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastTouched = new();

    /// <summary>Когда можно снова сходить в базу за списком турниров.</summary>
    private DateTime _nextScan = DateTime.MinValue;

    /// <summary>
    /// Пора ли опрашивать матчи вообще. Когда активных турниров нет, незачем раз в
    /// двадцать секунд дёргать базу: отдыхаем минуту.
    /// </summary>
    public bool ShouldScan(DateTime now) => now >= _nextScan;

    public void ScanDone(DateTime now, bool foundAnything) =>
        _nextScan = now + (foundAnything ? TimeSpan.Zero : TimeSpan.FromMinutes(1));

    public bool ShouldCheck(int matchId, DateTime now) =>
        !_nextCheck.TryGetValue(matchId, out var at) || now >= at;

    /// <summary>
    /// Назначает следующую проверку по тому, что видно о матче.
    /// </summary>
    /// <param name="sawBattles">
    /// В журнале уже есть бои этой пары. Значит, серия идёт прямо сейчас и следующий
    /// бой — вопрос минут: держим быстрый темп, не глядя на то, сколько матч висит.
    /// </param>
    public void Checked(int matchId, DateTime now, DateTime? readyAt, bool sawBattles)
    {
        _lastTouched[matchId] = now;
        _nextCheck[matchId] = now + Delay(now, readyAt, sawBattles);
    }

    /// <summary>Матч закрыт — следить за ним больше не нужно.</summary>
    public void Forget(int matchId)
    {
        _nextCheck.TryRemove(matchId, out _);
        _lastTouched.TryRemove(matchId, out _);
    }

    /// <summary>Чистка записей о матчах, которых уже нет (турнир завершён, сетка пересобрана).</summary>
    public void Prune(DateTime now)
    {
        foreach (var (id, touched) in _lastTouched)
            if (now - touched > Forgettable)
                Forget(id);
    }

    private static TimeSpan Delay(DateTime now, DateTime? readyAt, bool sawBattles)
    {
        if (sawBattles) return Hot;
        if (readyAt is null) return Cold;

        var waiting = now - readyAt.Value;
        if (waiting < JustReady) return Hot;
        if (waiting < SameEvening) return Warm;
        if (waiting < SameDay) return Cool;
        return Cold;
    }
}
