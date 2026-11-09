using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Живая карточка КВ клана (Telegram-виджет). Первый показ создаёт общий виджет клана,
/// следующие показывают его же - поэтому во всех чатах и пересылках одни и те же цифры,
/// которые воркер обновляет, пока карточку недавно показывали.
///
/// Без виджетов (настройка выключена, Telegram их не принял) ShowAsync отвечает false,
/// и бот присылает прежнюю текстовую сводку.
/// </summary>
public class WarWidgetUseCase(
    IWidgetSender widgets,
    IWarWidgetRepository repo,
    IClanRepository clans,
    GetClanStatusUseCase status)
{
    /// <summary>Сколько живёт карточка после последнего показа: дольше её уже не смотрят.</summary>
    public static readonly TimeSpan KeepLive = TimeSpan.FromDays(2);

    /// <summary>Как часто клиенты перечитывают данные карточки, с.</summary>
    public const int ClientPollSeconds = 60;

    /// <summary>Не чаще одного обновления карточки за этот срок: CR API отдаёт свежее раз в пару минут.</summary>
    public static readonly TimeSpan MinPushGap = TimeSpan.FromMinutes(4);

    public bool Enabled => widgets.Enabled;

    public static BotLang LangOf(BotText t) =>
        ReferenceEquals(t, BotText.Uk) ? BotLang.Uk : ReferenceEquals(t, BotText.En) ? BotLang.En : BotLang.Ru;

    /// <summary>Показать карточку клана в чате. false - карточки не вышло, пусть будет текст.</summary>
    public async Task<bool> ShowAsync(Clan clan, BotLang lang, long chatId, int? threadId, string botUsername,
        CancellationToken ct = default)
    {
        if (!widgets.Enabled) return false;

        var s = await status.ExecuteAsync(clan.ClanTag, ct);
        if (s is null) return false;

        var wire = BotText.ToWire(lang);
        var caption = WarWidgetCard.Caption(lang, s.ClanName);
        var plain = WarWidgetCard.Data(s, 0);
        var now = DateTime.UtcNow;
        var row = await repo.GetAsync(clan.Id, wire, ct);

        if (row is not null)
        {
            // Свежие цифры - до показа, чтобы новая копия не пришла со вчерашними
            if (row.LastData != plain)
            {
                var edit = await widgets.EditSharedAsync(row.WidgetId,
                    WarWidgetCard.Data(s, row.Revision + 1), ClientPollSeconds, ct);
                if (edit is WidgetEditResult.Ok or WidgetEditResult.NotModified)
                {
                    row.Revision++;
                    row.LastData = plain;
                    row.LastPushedUtc = now;
                }
            }

            if (await widgets.SendSharedAsync(chatId, row.WidgetId, caption, threadId, ct))
            {
                row.LastShownUtc = now;
                await repo.SaveChangesAsync(ct);
                return true;
            }
            // Виджет пропал на стороне Telegram - заводим новый вместо него
        }

        var revision = (row?.Revision ?? 0) + 1;
        var html = WarWidgetCard.Html(lang, botUsername);
        var id = await widgets.SendSharedNewAsync(chatId, html, WarWidgetCard.Data(s, revision), caption, threadId, ct);
        if (id is null) return false;

        if (row is null)
        {
            await repo.AddAsync(new WarWidget
            {
                ClanId = clan.Id,
                Lang = wire,
                WidgetId = id,
                Revision = revision,
                LastData = plain,
                CreatedUtc = now,
                LastShownUtc = now,
                LastPushedUtc = now,
            }, ct);
        }
        else
        {
            row.WidgetId = id;
            row.Revision = revision;
            row.LastData = plain;
            row.LastShownUtc = now;
            row.LastPushedUtc = now;
        }
        try
        {
            await repo.SaveChangesAsync(ct);
        }
        catch (Exception) when (row is null)
        {
            // Два /status одного клана разом: карточку завёл и соседний запрос, а эта уже
            // отправлена - запоминать её не нужно, дальше живёт та, что записана
        }
        return true;
    }

    /// <summary>
    /// Обновить данные карточек, которые недавно показывали. Сводка клана берётся одна на
    /// все языки его карточек. Возвращает, сколько карточек обновлено.
    /// </summary>
    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        if (!widgets.Enabled) return 0;

        var now = DateTime.UtcNow;
        var rows = await repo.ShownSinceAsync(now - KeepLive, ct);
        var pushed = 0;

        foreach (var group in rows.GroupBy(r => r.ClanId))
        {
            var due = group.Where(r => r.LastPushedUtc is null || now - r.LastPushedUtc >= MinPushGap).ToList();
            if (due.Count == 0) continue;

            var clan = await clans.GetByIdAsync(group.Key, ct);
            if (clan is null) continue;

            var s = await status.ExecuteAsync(clan.ClanTag, ct);
            if (s is null) continue;
            var plain = WarWidgetCard.Data(s, 0);

            foreach (var row in due.Where(r => r.LastData != plain))
            {
                var edit = await widgets.EditSharedAsync(row.WidgetId,
                    WarWidgetCard.Data(s, row.Revision + 1), ClientPollSeconds, ct);
                switch (edit)
                {
                    case WidgetEditResult.Ok or WidgetEditResult.NotModified:
                        row.Revision++;
                        row.LastData = plain;
                        row.LastPushedUtc = now;
                        pushed++;
                        break;
                    case WidgetEditResult.Gone:
                        // Обновлять больше нечего; следующий /status заведёт новую карточку
                        row.LastShownUtc = DateTime.UnixEpoch;
                        break;
                }
            }
        }

        await repo.SaveChangesAsync(ct);
        return pushed;
    }
}
