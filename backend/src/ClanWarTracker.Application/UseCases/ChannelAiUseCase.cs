using System.Text;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Нейро-посты из панели: владелец пишет, что нужно («итоги недели в лиге с юмором»),
/// бот собирает свежие данные - мировой топ и мету, лигу 1×1, челлендж, статистику
/// бота, последние новости игры - и нейросеть пишет пост только по ним. Готовый пост
/// ложится черновиком: в канал без владельца он не уходит.
///
/// Данные - текстом, а не «доступом к базе»: модель видит ровно то, что мы ей дали,
/// и не может ни выдумать игрока, ни утащить лишнего (Telegram ID в данные не попадают).
/// </summary>
public class ChannelAiUseCase(
    INewsTranslator ai,
    IChannelPostRepository posts,
    ITopPlayerRepository top,
    IDuelRepository duels,
    ChallengeUseCase challenge,
    GetTopMetaUseCase meta,
    GetOwnerDashboardUseCase dashboard)
{
    private const int TzOffsetMinutes = 180;
    public const int MaxRequest = 1500;

    public enum Result { Ok, NoAi, Empty, NotFound, Failed }

    /// <returns>Итог и, если не вышло, причина по-русски.</returns>
    public async Task<(Result Result, string? Error)> GenerateAsync(string? request, CancellationToken ct = default)
    {
        if (!ai.Enabled) return (Result.NoAi, null);
        if (string.IsNullOrWhiteSpace(request)) return (Result.Empty, null);
        var req = request.Trim();
        if (req.Length > MaxRequest) req = req[..MaxRequest];

        var data = await DataAsync(ct);
        var post = await ai.ComposeAsync(req, data, ct);
        if (post is not { } p) return (Result.Failed, ai.LastError);

        await posts.TryAddAsync(new ChannelPost
        {
            Kind = "ai",
            State = ChannelPostState.Draft,
            Title = Cut(p.Title.Length > 0 ? p.Title : req, 200),
            Text = p.Text,
            ButtonText = ChannelUseCase.DefaultButton,
            ButtonUrl = "startapp:",
            CreatedUtc = DateTime.UtcNow,
        }, ct);
        return (Result.Ok, null);
    }

    /// <summary>Переписать черновик по указанию: «короче», «смешнее», «добавь призыв вступить».</summary>
    public async Task<(Result Result, string? Error)> RewriteAsync(int id, string? instruction, string? currentText,
        CancellationToken ct = default)
    {
        if (!ai.Enabled) return (Result.NoAi, null);
        if (string.IsNullOrWhiteSpace(instruction)) return (Result.Empty, null);
        var post = await posts.GetAsync(id, ct);
        if (post is null || post.State == ChannelPostState.Published) return (Result.NotFound, null);

        // Переписываем то, что владелец видит в поле сейчас, даже если ещё не сохранил
        var text = string.IsNullOrWhiteSpace(currentText) ? post.Text : currentText.Trim();
        var data = await DataAsync(ct);
        var result = await ai.RewriteAsync(Cut(instruction.Trim(), MaxRequest), text, data, ct);
        if (result is not { } r) return (Result.Failed, ai.LastError);

        post.Text = r.Text;
        if (r.Title.Length > 0) post.Title = Cut(r.Title, 200);
        await posts.SaveChangesAsync(ct);
        return (Result.Ok, null);
    }

    /* ---------------- Данные для модели ---------------- */

    /// <summary>
    /// Свежие данные бота одним текстом. Каждый раздел собирается отдельно: нет снимка
    /// топа или упал API - пропадает этот раздел, а не весь пост.
    /// </summary>
    public async Task<string> DataAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var local = now.AddMinutes(TzOffsetMinutes);
        var sb = new StringBuilder();
        sb.AppendLine($"Сегодня: {ChannelText.Day(local)} {local:yyyy}, {local:HH:mm} по Киеву.");

        await Section(sb, "Бот Clanify", async () =>
        {
            var s = await dashboard.GetStatsAsync(ct);
            return
            [
                $"Игроков привязано: {s.TotalLinkedUsers}, кланов подключено: {s.TotalClans}, новых игроков за 7 дней: {s.NewUsers7d}.",
                "Что умеет бот: трекер клановой войны с напоминаниями и отчётами в чат клана; итоги дня клана; утренний дайджест боёв в личку;",
                "разбор поражений (уровни карт, контры, тильт); «Стоп-тильт» - сигнал остановиться после серии поражений;",
                "лига дуэлей 1×1 - вызов любого игрока прямо в чате через @бота, счёт по журналу боёв, кубки лиги и ранги от Бронзы до Легенды;",
                "челленджи выходных с призами; турниры и челленджи стримеров; мировой топ и мета топ-1000; Clanify Plus - расширенные функции за звёзды Telegram.",
            ];
        });

        await Section(sb, "Мировой топ", async () =>
        {
            var days = await top.DaysAsync(2, ct);
            if (days.Count == 0) return [];
            var today = (await top.GetDayAsync(days[0], ct)).OrderBy(p => p.Rank).ToList();
            if (today.Count == 0) return [];
            var lines = new List<string> { $"Снимок за {days[0]} (UTC):" };
            lines.AddRange(today.Take(10).Select(p =>
                $"{p.Rank}. {p.Name}{(string.IsNullOrWhiteSpace(p.ClanName) ? "" : $" ({p.ClanName})")} - {p.Trophies}"));
            lines.Add($"Порог входа в топ-{today[^1].Rank}: {today[^1].Trophies}.");
            if (days.Count > 1)
            {
                var prev = (await top.GetDayAsync(days[1], ct)).ToDictionary(p => p.PlayerTag, p => p);
                var climbers = today
                    .Where(p => prev.ContainsKey(p.PlayerTag))
                    .Select(p => (p, Up: prev[p.PlayerTag].Rank - p.Rank))
                    .Where(x => x.Up >= 20)
                    .OrderByDescending(x => x.Up)
                    .Take(3)
                    .Select(x => $"{x.p.Name}: с #{prev[x.p.PlayerTag].Rank} на #{x.p.Rank}");
                var c = string.Join("; ", climbers);
                if (c.Length > 0) lines.Add($"Взлёты за сутки: {c}.");
            }
            return lines;
        });

        await Section(sb, "Мета топ-1000: популярные карты", async () =>
        {
            var m = await meta.ExecuteAsync(ct);
            if (m is null || m.Cards.Count == 0) return [];
            var lines = new List<string> { $"Доля колод с картой{(m.ComparedToDayUtc is null ? "" : $", изменение с {m.ComparedToDayUtc}")}:" };
            lines.AddRange(m.Cards.Take(15).Select(c =>
                $"{c.Name} {c.Percent}%{(c.DeltaPercent is double d ? $" ({(d >= 0 ? "+" : "")}{d})" : "")}"));
            var risers = m.Cards.Where(c => c.DeltaPercent is > 0.0).OrderByDescending(c => c.DeltaPercent).Take(3).ToList();
            var fallers = m.Cards.Where(c => c.DeltaPercent is < 0.0).OrderBy(c => c.DeltaPercent).Take(3).ToList();
            if (risers.Count > 0) lines.Add("Растут: " + string.Join(", ", risers.Select(c => $"{c.Name} +{c.DeltaPercent}")));
            if (fallers.Count > 0) lines.Add("Падают: " + string.Join(", ", fallers.Select(c => $"{c.Name} {c.DeltaPercent}")));
            return lines;
        });

        await Section(sb, "Лига дуэлей 1×1", async () =>
        {
            var leaders = await duels.GetTopAsync(10, ct);
            var recent = await duels.GetRecentAsync(500, ct);
            var week = recent.Where(d => d.FinishedUtc >= now.AddDays(-7)).ToList();
            if (leaders.Count == 0 && week.Count == 0) return [];
            var lines = new List<string>();
            var rank = 0;
            foreach (var p in leaders)
                lines.Add($"{++rank}. {p.Name} - {p.Rating} кубков лиги, {DuelUseCase.RankName(p.Rating, DuelText.Ru)}, побед {p.Wins}, поражений {p.Losses}");
            lines.Add($"Дуэлей за сутки: {recent.Count(d => d.FinishedUtc >= now.AddDays(-1))}, за неделю: {week.Count}.");
            var gains = new Dictionary<string, int>();
            foreach (var d in week.Where(d => d.Rated))
            {
                gains[d.AName] = gains.GetValueOrDefault(d.AName) + d.DeltaA;
                gains[d.BName] = gains.GetValueOrDefault(d.BName) + d.DeltaB;
            }
            var best = gains.OrderByDescending(g => g.Value).FirstOrDefault();
            if (best.Value > 0) lines.Add($"Рывок недели: {best.Key} +{best.Value} кубков лиги.");
            foreach (var d in recent.Take(5))
                lines.Add($"Недавняя дуэль: {d.AName} {d.ScoreA}:{d.ScoreB} {d.BName} (bo{d.BestOf})");
            return lines;
        });

        await Section(sb, "Челлендж", async () =>
        {
            var e = await challenge.CurrentAsync(ct);
            var rule = ChallengeRules.Normalize(e.Rule);
            var status = e.Status(now) switch { "live" => "идёт", "upcoming" => "скоро", _ => "закончился" };
            var start = e.StartUtc.AddMinutes(TzOffsetMinutes);
            var end = e.EndUtc.AddMinutes(TzOffsetMinutes);
            var lines = new List<string>
            {
                $"«{(string.IsNullOrWhiteSpace(e.Title) ? "Челлендж выходных" : e.Title)}» - {status}, с {start:dd.MM HH:mm} до {end:dd.MM HH:mm} по Киеву.",
                $"Формат: {ChannelText.RuleLine(rule)}",
            };
            if (!string.IsNullOrWhiteSpace(e.Prize)) lines.Add($"Приз: {e.Prize}.");
            lines.Add($"Участников: {await challenge.ParticipantsAsync(e, ct)}.");
            if (e.Status(now) != "upcoming")
            {
                var rows = (await challenge.ResultsAsync(e, ct)).Where(r => r.Tickets > 0).OrderBy(r => r.Rank).Take(5);
                foreach (var r in rows) lines.Add($"{r.Rank}. {r.Name} - {ChannelText.Points(r.Tickets, rule)}");
            }
            return lines;
        });

        await Section(sb, "Последние новости Clash Royale (заголовки)", async () =>
            (await posts.ListAsync([ChannelPostState.Draft, ChannelPostState.Published], 40, ct))
                .Where(p => p.Kind == "news" && !string.IsNullOrWhiteSpace(p.Title))
                .Take(8)
                .Select(p => $"- {p.Title} ({p.CreatedUtc.AddMinutes(TzOffsetMinutes):dd.MM})")
                .ToList());

        await Section(sb, "Уже опубликовано в канале (не повторяйся)", async () =>
            (await posts.ListAsync([ChannelPostState.Published], 10, ct))
                .Where(p => !string.IsNullOrWhiteSpace(p.Title))
                .Select(p => $"- {p.Title}")
                .ToList());

        return sb.ToString();
    }

    private static async Task Section(StringBuilder sb, string title, Func<Task<List<string>>> build)
    {
        try
        {
            var lines = await build();
            if (lines.Count == 0) return;
            sb.AppendLine().AppendLine($"== {title} ==");
            foreach (var l in lines) sb.AppendLine(l);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* раздел не собрался - пост будет без него */ }
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
