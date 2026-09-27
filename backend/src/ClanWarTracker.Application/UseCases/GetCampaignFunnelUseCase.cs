using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Воронка по источникам: сколько пришло, привязали тег, подключили клан, заплатили.
///
/// Главная цифра - подключённые кланы, а не пришедшие. Один глава приводит
/// сразу 30-50 человек, случайный игрок из рекламы - одного себя и бота в чат
/// добавить не может. Реклама, которая даёт сотню стартов и ни одного клана,
/// выглядит успешной по кликам и бесполезна по делу.
///
/// Считается в памяти: людей с меткой сотни, оплат десятки, и группировка на
/// стороне базы здесь ничего не сэкономила бы.
/// </summary>
public class GetCampaignFunnelUseCase(
    IAcquisitionRepository acquisitions,
    ICampaignRepository campaigns,
    ISponsorPaymentRepository payments)
{
    /// <param name="Code">Код кампании; null у строки рефералов.</param>
    /// <param name="Known">Кампания заведена в панели. false — код пришёл из ссылки с опечаткой.</param>
    /// <param name="Payers">Сколько пришедших купили спонсорство хоть раз.</param>
    public record Row(
        string Source, string? Code, string Name, bool Known, DateTime? CreatedAtUtc,
        int Started, int Linked, int ClansConnected, int Payers, long Stars);

    public async Task<List<Row>> ExecuteAsync(CancellationToken ct = default)
    {
        var all = await acquisitions.GetAllAsync(ct);
        var known = await campaigns.GetAllAsync(ct);
        var paid = await payments.GetAllAsync(ct);

        var starsByPayer = paid
            .GroupBy(p => p.PayerTelegramUserId)
            .ToDictionary(g => g.Key, g => g.Sum(p => (long)p.Stars));

        var bySource = all.GroupBy(a => a.Source).ToDictionary(g => g.Key, g => g.ToList());

        Row Build(string source, string? code, string name, bool isKnown, DateTime? created)
        {
            var people = bySource.GetValueOrDefault(source) ?? [];
            var payers = people.Where(a => starsByPayer.ContainsKey(a.TelegramUserId)).ToList();
            return new Row(
                source, code, name, isKnown, created,
                Started: people.Count,
                Linked: people.Count(a => a.LinkedAtUtc is not null),
                ClansConnected: people.Count(a => a.ClanConnectedAtUtc is not null),
                Payers: payers.Count,
                Stars: payers.Sum(a => starsByPayer[a.TelegramUserId]));
        }

        var rows = known
            .Select(c => Build(TrackStartUseCase.AdSource(c.Code), c.Code, c.Name, true, c.CreatedAtUtc))
            .ToList();

        // Коды, которых нет среди кампаний: ссылка с опечаткой или кампания,
        // заведённая до этой панели. Молча их терять нельзя — это чьи-то люди.
        var knownSources = known.Select(c => TrackStartUseCase.AdSource(c.Code)).ToHashSet();
        rows.AddRange(bySource.Keys
            .Where(s => s.StartsWith("ad:", StringComparison.Ordinal) && !knownSources.Contains(s))
            .OrderBy(s => s)
            .Select(s => Build(s, s[3..], s[3..], false, null)));

        rows.Add(Build(TrackStartUseCase.RefSource, null, "", true, null));
        return rows;
    }
}
