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
/// Лог хранит около двадцати пяти последних боёв, поэтому проверяем часто: если пара
/// доиграет матч и уйдёт кататься в ладдер, их бои из лога вымоются. Запрос идёт по тегу
/// одной стороны (в её логе есть весь матч) и на пять минут оседает в кэше клиента, так
/// что частый цикл воркера не превращается в частые обращения к API.
/// </summary>
public class AutoResolveTournamentMatchesUseCase(
    ITournamentRepository tournaments,
    IClashRoyaleApi crApi,
    TournamentBracketService bracket)
{
    /// <returns>Сколько матчей закрыто.</returns>
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var active = await tournaments.GetForAutoResultsAsync(ct);
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
                .ToList();

            var changed = false;

            foreach (var match in ready)
            {
                try
                {
                    if (await TryResolveAsync(tournament, match, ct))
                    {
                        changed = true;
                        closed++;
                    }
                }
                catch
                {
                    // Один недоступный профиль не должен останавливать остальные матчи:
                    // закрываем что можем, остальное подождёт следующего цикла.
                }
            }

            if (changed) await tournaments.SaveChangesAsync(ct);
        }

        return closed;
    }

    private async Task<bool> TryResolveAsync(Tournament tournament, TournamentMatch match, CancellationToken ct)
    {
        var tagsA = Tags(match.ParticipantA!, tournament.Mode);
        var tagsB = Tags(match.ParticipantB!, tournament.Mode);

        // Неполная заявка в парном турнире (напарник не указан) — опознавать нечем.
        if (tagsA.Count == 0 || tagsB.Count == 0) return false;

        var since = Since(tournament, match);
        if (since is null) return false;

        var battles = await crApi.GetRecentBattlesAsync(match.ParticipantA!.PlayerTag, ct);
        if (battles.Count == 0) return false;

        var facts = battles.Select(b => new TournamentAutoResult.BattleFact(
            b.BattleTimeUtc,
            b.TeamTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.OpponentTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.CrownsFor,
            b.CrownsAgainst));

        var outcome = TournamentAutoResult.Resolve(facts, tagsA, tagsB, tournament.BestOf, since.Value);
        if (!outcome.Decided) return false;

        bracket.ApplyResult(tournament, match, outcome.ScoreA, outcome.ScoreB, auto: true);
        return true;
    }

    /// <summary>
    /// С какого момента бои считаются матчем: позже того, как пара сошлась, и не раньше
    /// объявленного старта турнира. Второе важно, когда сетку собрали заранее: встреча
    /// тех же команд за день до турнира — не матч этого турнира.
    /// </summary>
    private static DateTime? Since(Tournament tournament, TournamentMatch match)
    {
        var ready = match.ReadyAtUtc;
        if (ready is null) return null;                 // старый матч без отметки — только вручную

        var start = tournament.StartsAtUtc;
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
