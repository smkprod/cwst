using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Построение и обслуживание сетки турнира (single elimination).
/// Жеребьёвка случайна (турнир дружеский, не рейтинговый), но баи распределяются
/// по стандартному алгоритму сидирования, чтобы они не скапливались в одном углу сетки.
/// </summary>
public class TournamentBracketService
{
    /// <summary>
    /// Строит сетку с нуля по текущему списку активных участников. Вызывающий код должен
    /// сам очистить t.Matches перед вызовом, если перестраивает уже существующую сетку.
    /// </summary>
    public void GenerateBracket(Tournament t, Random? rng = null)
    {
        var active = t.Participants.Where(p => p.Status == TournamentParticipantStatus.Active).ToList();
        if (active.Count < 2)
            throw new InvalidOperationException("Нужно минимум 2 участника для жеребьёвки.");

        rng ??= new Random();
        var shuffled = active.OrderBy(_ => rng.Next()).ToList();
        for (var i = 0; i < shuffled.Count; i++) shuffled[i].Seed = i;

        var bracketSize = 1;
        while (bracketSize < shuffled.Count) bracketSize *= 2;

        var seedOrder = BuildSeedOrder(bracketSize);
        var slots = new TournamentParticipant?[bracketSize];
        for (var i = 0; i < bracketSize; i++)
        {
            var seed = seedOrder[i];
            slots[i] = seed < shuffled.Count ? shuffled[seed] : null;
        }

        var roundCount = (int)Math.Log2(bracketSize);
        var roundsByNumber = new Dictionary<int, List<TournamentMatch>>();

        var round1 = new List<TournamentMatch>();
        for (var i = 0; i < bracketSize / 2; i++)
        {
            var a = slots[2 * i];
            var b = slots[2 * i + 1];
            var match = new TournamentMatch
            {
                Round = 1,
                SlotIndex = i,
                ParticipantAId = a?.Id,
                ParticipantA = a,
                ParticipantBId = b?.Id,
                ParticipantB = b,
            };
            if (a is null || b is null)
            {
                var winner = a ?? b;
                match.Status = TournamentMatchStatus.Bye;
                match.WinnerParticipantId = winner!.Id;
                match.WinnerParticipant = winner;
            }
            else
            {
                match.Status = TournamentMatchStatus.Ready;
            }
            round1.Add(match);
            t.Matches.Add(match);
        }
        roundsByNumber[1] = round1;

        for (var r = 2; r <= roundCount; r++)
        {
            var prev = roundsByNumber[r - 1];
            var current = new List<TournamentMatch>();
            for (var i = 0; i < prev.Count / 2; i++)
            {
                var match = new TournamentMatch { Round = r, SlotIndex = i, Status = TournamentMatchStatus.Pending };
                current.Add(match);
                t.Matches.Add(match);

                prev[2 * i].NextMatch = match;
                prev[2 * i].NextMatchSlot = 0;
                prev[2 * i + 1].NextMatch = match;
                prev[2 * i + 1].NextMatchSlot = 1;
            }
            roundsByNumber[r] = current;
        }

        // Баи первого раунда резолвим сразу — победитель проходит дальше без игры.
        foreach (var bye in round1.Where(m => m.Status == TournamentMatchStatus.Bye))
            AdvanceWinner(bye, bye.WinnerParticipant!);

        t.Status = TournamentStatus.BracketReady;
        t.BracketGeneratedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Можно ли (пере)строить сетку: только пока ни один реальный матч не сыгран.</summary>
    public static bool CanRegenerateBracket(Tournament t) =>
        t.Matches.All(m => m.Status != TournamentMatchStatus.Completed);

    /// <summary>
    /// Проводит победителя в следующий раунд. Для финала (NextMatch == null) ничего не делает —
    /// завершение турнира и расстановку мест обрабатывает вызывающий use case.
    /// </summary>
    public void AdvanceWinner(TournamentMatch match, TournamentParticipant winner)
    {
        var next = match.NextMatch;
        if (next is null) return;

        if (match.NextMatchSlot == 0)
        {
            next.ParticipantAId = winner.Id;
            next.ParticipantA = winner;
        }
        else
        {
            next.ParticipantBId = winner.Id;
            next.ParticipantB = winner;
        }

        if (next.ParticipantA is not null && next.ParticipantB is not null)
            next.Status = TournamentMatchStatus.Ready;
    }

    /// <summary>
    /// Откатывает всё, что зависело от результата этого матча: снимает победителя со следующих
    /// матчей и каскадно очищает их результаты вплоть до финала. Нужно перед исправлением
    /// уже введённого результата создателем турнира.
    /// </summary>
    public void ClearDownstream(TournamentMatch match)
    {
        var next = match.NextMatch;
        if (next is null) return;

        if (next.Status == TournamentMatchStatus.Completed)
            ClearDownstream(next);

        if (match.NextMatchSlot == 0) { next.ParticipantAId = null; next.ParticipantA = null; }
        else { next.ParticipantBId = null; next.ParticipantB = null; }

        next.ScoreA = 0;
        next.ScoreB = 0;
        next.WinnerParticipantId = null;
        next.WinnerParticipant = null;
        next.Status = TournamentMatchStatus.Pending;
        next.UpdatedAtUtc = null;
    }

    /// <summary>
    /// Стандартный порядок сидирования для турнирной сетки размера size (степень двойки):
    /// возвращает посев (0-based) для каждой позиции в сетке так, чтобы баи (несуществующие
    /// верхние посевы) равномерно распределились, а не скопились в одном конце.
    /// </summary>
    private static int[] BuildSeedOrder(int size)
    {
        var seeds = new List<int> { 0 };
        while (seeds.Count < size)
        {
            var count = seeds.Count * 2;
            var next = new List<int>(count);
            foreach (var s in seeds)
            {
                next.Add(s);
                next.Add(count - 1 - s);
            }
            seeds = next;
        }
        return seeds.ToArray();
    }
}
