using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Закрывает сыгранные матчи турнира сам, по боевому логу участников.
///
/// Матч опознаётся полным совпадением тегов с обеих сторон, так что засчитать чужой бой
/// нельзя: чтобы совпали и оба напарника, и оба соперника, эти четверо должны были сесть
/// играть именно друг против друга. Всё, что не сошлось однозначно, остаётся организатору —
/// кнопка «Внести результат» никуда не девается, и его счёт всегда перекрывает наш.
///
/// Темп опроса подстраивается под матч (см. TournamentPollState): пара в разгаре серии
/// проверяется раз в полминуты, чтобы счёт появлялся сразу после боя, а матч, до которого
/// никто не дошёл третий час, — раз в пять минут. Запрос идёт по тегу одной стороны: в её
/// журнале есть весь матч, второй профиль читать незачем.
/// </summary>
public class AutoResolveTournamentMatchesUseCase(
    ITournamentRepository tournaments,
    IClashRoyaleApi crApi,
    TournamentBracketService bracket,
    TournamentPollState poll,
    TournamentNotifier notify)
{
    /// <returns>Сколько матчей закрыто.</returns>
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (!poll.ShouldScan(now)) return 0;

        var active = await tournaments.GetForAutoResultsAsync(ct);
        poll.ScanDone(now, active.Count > 0);
        poll.Prune(now);
        if (active.Count == 0) return 0;

        var closed = 0;

        foreach (var tournament in active)
        {
            // Берём матчи по одному снимку: ApplyResult двигает сетку и может сделать
            // готовым следующий матч. Закрывать его в этом же проходе незачем — до
            // следующего цикла его всё равно никто не сыграет.
            var ready = tournament.Matches
                .Where(m => m.Status == TournamentMatchStatus.Ready
                            && m.ParticipantA is not null && m.ParticipantB is not null)
                // Каждый матч опрашивается со своей частотой: пара в разгаре серии —
                // раз в полминуты, матч, до которого не дошли третий час, — раз в пять.
                .Where(m => poll.ShouldCheck(m.Id, now))
                .ToList();

            // Уведомления копим и шлём после сохранения: сообщение «вы прошли дальше»
            // по матчу, который не записался, отозвать уже нельзя.
            var pending = new List<(TournamentMatch Match, MatchOutcome Outcome)>();

            foreach (var match in ready)
            {
                try
                {
                    if (await TryResolveAsync(tournament, match, now, ct) is { } outcome)
                    {
                        pending.Add((match, outcome));
                        closed++;
                    }
                }
                catch
                {
                    // Один недоступный профиль не должен останавливать остальные матчи:
                    // закрываем что можем, остальное подождёт следующего цикла.
                }
            }

            if (pending.Count == 0) continue;

            await tournaments.SaveChangesAsync(ct);

            foreach (var (match, outcome) in pending)
            {
                await notify.MatchResultAsync(tournament, match, outcome, ct);
                if (outcome.NextReady is { } next) await notify.MatchReadyAsync(tournament, next, ct);
            }

            // Табло одно на турнир — обновляем один раз после всей пачки, а не на
            // каждый матч: иначе одно и то же сообщение переписывалось бы подряд.
            if (await notify.UpdateScoreboardAsync(tournament, ct))
                await tournaments.SaveChangesAsync(ct);
        }

        return closed;
    }

    /// <returns>Исход матча, если он закрыт; null — ещё не доиграли или опознать не вышло.</returns>
    private async Task<MatchOutcome?> TryResolveAsync(
        Tournament tournament, TournamentMatch match, DateTime now, CancellationToken ct)
    {
        var tagsA = Tags(match.ParticipantA!, tournament.Mode);
        var tagsB = Tags(match.ParticipantB!, tournament.Mode);

        // Неполная заявка в парном турнире (напарник не указан) — опознавать нечем.
        // Запоминаем надолго: пока заявку не поправят, смотреть тут нечего.
        if (tagsA.Count == 0 || tagsB.Count == 0)
        {
            poll.Checked(match.Id, now, readyAt: null, sawBattles: false);
            return null;
        }

        var since = Since(tournament, match);
        if (since is null)
        {
            poll.Checked(match.Id, now, readyAt: null, sawBattles: false);
            return null;
        }

        var battles = await crApi.GetBattlesForAutoResultAsync(match.ParticipantA!.PlayerTag, ct);
        if (battles.Count == 0)
        {
            poll.Checked(match.Id, now, match.ReadyAtUtc, sawBattles: false);
            return null;
        }

        var facts = battles.Select(b => new TournamentAutoResult.BattleFact(
            b.BattleTimeUtc,
            b.TeamTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.OpponentTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.CrownsFor,
            b.CrownsAgainst));

        var outcome = TournamentAutoResult.Resolve(facts, tagsA, tagsB, tournament.BestOf, since.Value);
        if (!outcome.Decided)
        {
            // Бои пары уже есть, но серия не доиграна — следующий бой через минуты,
            // так что переходим на быстрый темп независимо от возраста матча.
            poll.Checked(match.Id, now, match.ReadyAtUtc, sawBattles: outcome.Counted > 0);
            return null;
        }

        var applied = bracket.ApplyResult(tournament, match, outcome.ScoreA, outcome.ScoreB, auto: true);
        poll.Forget(match.Id);
        return applied;
    }

    /// <summary>
    /// С какого момента бои считаются матчем: позже того, как пара сошлась, и не раньше
    /// фактического старта турнира. Второе важно, когда сетку собрали заранее: встреча
    /// тех же команд за день до турнира — не матч этого турнира.
    ///
    /// Именно фактического, а не объявленного. Организатор объявляет «начало в 20:00»,
    /// а жмёт «Начать», когда все собрались, — нередко раньше. По объявленной дате бои,
    /// сыгранные до неё, не засчитывались бы вовсе, и выглядело бы это как сломанный
    /// автозачёт, а не как несовпадение часов.
    ///
    /// StartedAtUtc может быть пуст: статус переходит в «идёт» и когда организатор
    /// просто внёс первый результат, минуя кнопку. Тогда границей служит жеребьёвка —
    /// раньше неё пары не существовало.
    /// </summary>
    private static DateTime? Since(Tournament tournament, TournamentMatch match)
    {
        var ready = match.ReadyAtUtc;
        if (ready is null) return null;                 // старый матч без отметки — только вручную

        var start = tournament.StartedAtUtc ?? tournament.BracketGeneratedAtUtc;
        return start is not null && start > ready ? start : ready;
    }

    /// <summary>Теги стороны: в парном — капитан и напарник, в одиночном — один игрок.</summary>
    private static List<string> Tags(TournamentParticipant p, TournamentMode mode)
    {
        var tags = new List<string> { LinkPlayerUseCase.Normalize(p.PlayerTag) };
        if (mode != TournamentMode.Duo) return tags;

        if (string.IsNullOrWhiteSpace(p.PartnerPlayerTag)) return [];
        tags.Add(LinkPlayerUseCase.Normalize(p.PartnerPlayerTag));
        return tags;
    }
}
