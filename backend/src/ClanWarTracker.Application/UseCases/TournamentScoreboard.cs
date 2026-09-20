using System.Text;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Живое табло турнира: одно сообщение в чате, которое переписывается после каждого
/// матча, вместо потока новых.
///
/// Поток сообщений о результатах и табло решают разные задачи. Поток отвечает на
/// «что сейчас произошло» и тонет в истории; табло отвечает на «кто кому должен
/// прямо сейчас» и всегда лежит в закрепе. Порознь они не заменяют друг друга.
/// </summary>
public static class TournamentScoreboard
{
    /// <summary>
    /// Предел Telegram на текст сообщения — 4096 символов. Турнир на 64 команды даёт
    /// 63 матча, и в лимит они не влезут, поэтому длинное табло сворачиваем до
    /// текущего раунда: именно он и нужен тому, кто смотрит в закреп.
    /// </summary>
    private const int MaxLength = 3800;

    /// <summary>
    /// До скольких символов ужимаем название команды. Разрешено до 64, но в строке
    /// сетки такое имя всё равно не читается, а шестьдесят три матча по два таких
    /// имени не влезают ни в какой лимит.
    /// </summary>
    private const int MaxName = 22;

    /// <summary>Сколько матчей печатаем, когда не помогло уже ничего.</summary>
    private const int MaxLines = 40;

    public static string Render(Tournament t)
    {
        var sb = new StringBuilder();
        sb.Append("🏆 ").Append(t.Name).Append('\n');
        sb.Append(StatusLine(t)).Append("\n\n");

        var rounds = t.Matches
            .GroupBy(m => m.Round)
            .OrderBy(g => g.Key)
            .ToList();

        if (rounds.Count == 0)
        {
            sb.Append("Жеребьёвки ещё не было. Участников: ")
              .Append(t.Participants.Count(p => p.Status != TournamentParticipantStatus.Withdrawn));
            return sb.ToString();
        }

        var maxRound = rounds[^1].Key;
        var budget = MaxLength - sb.Length;

        // Сворачиваем ступенями, пока не влезет. Одного шага мало: в турнире на
        // шестьдесят четыре команды до первого сыгранного матча сворачивать некуда —
        // текущий раунд и есть первый, и «показать с текущего» ничего не сокращает.
        var body = Rounds(rounds, maxRound, from: 1, limit: int.MaxValue);
        if (body.Length > budget)
        {
            var live = rounds.FirstOrDefault(g =>
                g.Any(m => m.Status is TournamentMatchStatus.Ready or TournamentMatchStatus.Pending));
            var from = live?.Key ?? maxRound;

            body = Folded(rounds, maxRound, from, int.MaxValue);

            // Всё ещё не влезло — оставляем один текущий раунд.
            if (body.Length > budget)
                body = Folded(rounds, maxRound, from, int.MaxValue, only: from);

            // И даже он длинный — обрезаем число строк, честно сказав сколько скрыто.
            if (body.Length > budget)
                body = Folded(rounds, maxRound, from, MaxLines, only: from);
        }

        sb.Append(body);
        return sb.ToString();
    }

    private static string Folded(
        List<IGrouping<int, TournamentMatch>> rounds, int maxRound, int from, int limit, int? only = null)
    {
        var head = from > 1 ? "…предыдущие раунды сыграны\n\n" : "";
        return head + Rounds(rounds, maxRound, only ?? from, limit, only);
    }

    private static string Rounds(
        List<IGrouping<int, TournamentMatch>> rounds, int maxRound, int from, int limit, int? only = null)
    {
        var sb = new StringBuilder();
        var printed = 0;
        var hidden = 0;

        foreach (var round in rounds.Where(r => r.Key >= from && (only is null || r.Key == only)))
        {
            sb.Append(round.Key == maxRound ? "— ФИНАЛ —" : $"— Раунд {round.Key} —").Append('\n');
            foreach (var m in round.OrderBy(m => m.SlotIndex))
            {
                if (printed >= limit) { hidden++; continue; }
                sb.Append(Line(m)).Append('\n');
                printed++;
            }
            sb.Append('\n');
        }

        if (hidden > 0) sb.Append("…и ещё ").Append(hidden).Append(" матчей\n");
        return sb.ToString();
    }

    private static string StatusLine(Tournament t) => t.Status switch
    {
        TournamentStatus.Completed => Champion(t) is { } c ? $"👑 Чемпион — {c}" : "Турнир завершён",
        TournamentStatus.Cancelled => "Турнир отменён",
        TournamentStatus.RegistrationOpen => "Идёт регистрация",
        TournamentStatus.BracketReady => "Сетка готова, ждём старта",
        _ => Playing(t),
    };

    private static string Playing(Tournament t)
    {
        var total = t.Matches.Count(m => m.Status != TournamentMatchStatus.Bye);
        var done = t.Matches.Count(m => m.Status == TournamentMatchStatus.Completed);
        return $"Сыграно {done} из {total}";
    }

    private static string? Champion(Tournament t) =>
        t.Participants.FirstOrDefault(p => p.FinalPlacement == 1) is { } c
            ? c.TeamName ?? c.PlayerName
            : null;

    /// <summary>
    /// Строка матча. Галочка у победителя, а не цвет: в тексте Telegram цвета нет,
    /// и отличать выигравшего нужно значком.
    /// </summary>
    private static string Line(TournamentMatch m)
    {
        var a = Name(m.ParticipantA, m.SlotAVacated);
        var b = Name(m.ParticipantB, m.SlotBVacated);

        return m.Status switch
        {
            TournamentMatchStatus.Completed => m.WinnerParticipantId == m.ParticipantAId
                ? $"✅ {a} {m.ScoreA}:{m.ScoreB} {b}"
                : $"{a} {m.ScoreA}:{m.ScoreB} {b} ✅",
            TournamentMatchStatus.Bye => $"⏭ {Name(m.WinnerParticipant, false)} — без игры",
            // Серия идёт — показываем её счёт. «1:0» отличает начатый матч от того,
            // к которому ещё не приступали, а именно это и хотят видеть в закрепе.
            TournamentMatchStatus.Ready when m.ScoreA > 0 || m.ScoreB > 0
                => $"⚔️ {a} {m.ScoreA}:{m.ScoreB} {b}",
            TournamentMatchStatus.Ready => $"⚔️ {a} vs {b}",
            _ => $"⏳ {a} vs {b}",
        };
    }

    private static string Name(TournamentParticipant? p, bool vacated)
    {
        var name = p?.TeamName ?? p?.PlayerName;
        if (name is null) return vacated ? "—" : "?";
        return name.Length <= MaxName ? name : name[..(MaxName - 1)] + "…";
    }
}
