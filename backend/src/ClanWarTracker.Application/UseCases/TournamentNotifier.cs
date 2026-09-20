using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Рассылка по ходу турнира: чем кончился матч и какой играть следующий.
///
/// Без неё автозачёт наполовину бесполезен: бот закрыл матч, а победитель сидит и
/// не знает, что уже можно идти дальше, — ровно то ожидание, ради устранения
/// которого автозачёт и делался.
///
/// Личные сообщения уходят капитанам: напарник в парном турнире хранится тегом,
/// своей привязки к Telegram у него может не быть вовсе. Сбой отправки одному
/// не должен мешать остальным и тем более откатывать результат матча — он уже
/// сохранён к моменту рассылки.
/// </summary>
public class TournamentNotifier(
    INotificationSender notifier,
    IPlayerRepository players,
    IClanRepository clans,
    ICardUrls cardUrls)
{
    /// <summary>Итог матча: обоим капитанам в личку и, если разрешено, в чат клана.</summary>
    public async Task MatchResultAsync(
        Tournament tournament, TournamentMatch match, MatchOutcome outcome, CancellationToken ct = default)
    {
        var winner = Name(outcome.Winner);
        var loser = Name(outcome.Loser);
        var score = Score(match, outcome);
        var how = match.AutoResolved ? "\n\nСчёт записан ботом по вашим боям." : "";

        if (outcome.TournamentCompleted)
        {
            // Картинку рисует уже сохранённый турнир, поэтому её адрес можно собирать
            // прямо здесь: ручка сама возьмёт из базы чемпиона, счёт и любимые карты.
            var card = cardUrls.Card("champion", tournament.Id.ToString());
            var caption = $"👑 Турнир «{tournament.Name}» завершён!\n" +
                          $"Чемпион — {winner} ({score} в финале против {loser}).";

            await DmAsync(outcome.Winner,
                $"👑 Вы выиграли турнир «{tournament.Name}»!\n\nФинал: {winner} — {loser} {score}", ct);
            await DmAsync(outcome.Loser,
                $"🥈 Второе место в турнире «{tournament.Name}».\n\nФинал: {winner} — {loser} {score}\n" +
                "Обидно, но до финала дошли не все.", ct);

            // Чемпиону карточка уходит и в личку: её кидают в чаты, и удобно, когда
            // она уже лежит в переписке с ботом, а не только в клановой беседе.
            if (card is not null) await PhotoAsync(outcome.Winner.TelegramUserId, card, caption, ct);

            await ChatAsync(tournament, caption, card, ct);
            return;
        }

        await DmAsync(outcome.Winner, $"✅ Победа: {winner} — {loser} {score}{how}\n\n{NextLine(outcome)}", ct);
        await DmAsync(outcome.Loser,
            $"❌ Поражение: {winner} — {loser} {score}{how}\n\n" +
            $"На этом турнир «{tournament.Name}» для вас закончен. Спасибо за игру!", ct);

        await ChatAsync(tournament, $"🏆 «{tournament.Name}»: {winner} — {loser} {score}", null, ct);
    }

    /// <summary>
    /// Пара собралась — зовём играть. Отдельное сообщение, а не приписка к итогу
    /// прошлого матча: второй соперник обычно доигрывает свой матч позже, и к моменту
    /// первой рассылки его ещё не существует.
    /// </summary>
    public async Task MatchReadyAsync(Tournament tournament, TournamentMatch match, CancellationToken ct = default)
    {
        if (match.ParticipantA is null || match.ParticipantB is null) return;

        var a = Name(match.ParticipantA);
        var b = Name(match.ParticipantB);
        var format = tournament.BestOf > 1 ? $"до {tournament.BestOf} побед" : "один бой";
        var link = string.IsNullOrWhiteSpace(tournament.ClanInviteLink)
            ? ""
            : $"\nКлан турнира: {tournament.ClanInviteLink}";

        var text =
            $"⚔️ «{tournament.Name}» — ваш матч готов!\n\n{a} vs {b}\nФормат: {format}." +
            $"{link}\n\nИграйте — результат бот запишет сам.";

        await DmAsync(match.ParticipantA, text, ct);
        await DmAsync(match.ParticipantB, text, ct);
    }

    /// <summary>
    /// Команде, попавшей на бай: играть в этом раунде не с кем.
    ///
    /// Без этого сообщения она не получает вообще ничего — «ваш матч готов» уходит
    /// только парам, — и в сетке на шесть команд треть участников осталась бы гадать,
    /// ждать им соперника или уже что-то пропустили.
    /// </summary>
    public async Task ByeAsync(Tournament tournament, TournamentParticipant p, CancellationToken ct = default)
    {
        await DmAsync(p,
            $"⏭ «{tournament.Name}»: в первом раунде вам не досталось соперника — " +
            "вы проходите дальше без игры.\n\nЖдите: позовём, когда соперник определится.", ct);
    }

    /// <summary>
    /// Счёт всегда со стороны победителя: в сетке ScoreA относится к участнику A,
    /// а в сообщении «2:0» должно читаться как «победитель — проигравший».
    /// </summary>
    private static string Score(TournamentMatch match, MatchOutcome outcome)
    {
        var winnerIsA = outcome.Winner.Id == match.ParticipantAId;
        return winnerIsA ? $"{match.ScoreA}:{match.ScoreB}" : $"{match.ScoreB}:{match.ScoreA}";
    }

    private static string NextLine(MatchOutcome outcome) =>
        outcome.NextReady is { } next && next.ParticipantA is not null && next.ParticipantB is not null
            ? $"Следующий матч: {Name(next.ParticipantA)} vs {Name(next.ParticipantB)} — можно играть."
            : "Следующий соперник ещё доигрывает свой матч. Позовём, когда определится.";

    private static string Name(TournamentParticipant p) => p.TeamName ?? p.PlayerName;

    private async Task DmAsync(TournamentParticipant p, string text, CancellationToken ct)
    {
        try { await notifier.SendToUserAsync(p.TelegramUserId, text, ct); }
        catch { /* заблокировал бота, удалил аккаунт — остальных это касаться не должно */ }
    }

    /// <summary>
    /// Объявление в чат клана организатора. Клан ищем в момент отправки, а не храним
    /// в турнире: организатор может сменить клан, и правильный адрес — тот, где он
    /// сейчас, а не тот, где был при создании.
    /// </summary>
    private async Task ChatAsync(Tournament tournament, string text, string? photoUrl, CancellationToken ct)
    {
        if (!tournament.AnnounceResults) return;

        try
        {
            // Туда же, где висит табло, если оно уже опубликовано: результаты и табло
            // должны жить в одном месте, а не в двух разных чатах.
            var place = tournament.ScoreboardChatId is { } known
                ? (known, tournament.ScoreboardThreadId)
                : await ClanChatAsync(tournament, ct);
            if (place is not { } target) return;

            // Картинка не ушла — шлём текстом. Объявление чемпиона без картинки
            // лучше, чем отсутствие объявления.
            if (photoUrl is not null && await notifier.SendPhotoToChatAsync(
                    target.Item1, photoUrl, text, target.Item2, ct))
                return;

            await notifier.SendToChatAsync(target.Item1, text, target.Item2, ct: ct);
        }
        catch { /* бота выгнали из группы и т.п. — личные сообщения уже ушли */ }
    }

    /// <summary>
    /// Обновляет живое табло. Первый вызов публикует его и закрепляет, дальше только
    /// переписывает — поэтому чат в закрепе не засоряется.
    ///
    /// Вызывающий обязан сохранить турнир после: здесь проставляются id сообщения.
    /// </summary>
    public async Task<bool> UpdateScoreboardAsync(Tournament tournament, CancellationToken ct = default)
    {
        if (!tournament.AnnounceResults) return false;

        var text = TournamentScoreboard.Render(tournament);

        if (tournament.ScoreboardChatId is { } chatId && tournament.ScoreboardMessageId is { } messageId)
        {
            if (await notifier.EditAsync(chatId, messageId, text, ct)) return false;

            // Не переписалось — сообщение удалили или бота выгнали. Публикуем заново,
            // но только в тот же чат: переезжать табло само не должно.
            tournament.ScoreboardMessageId = null;
        }

        var target = tournament.ScoreboardChatId is { } known
            ? (known, tournament.ScoreboardThreadId)
            : await ClanChatAsync(tournament, ct);
        if (target is not { } place) return false;

        var posted = await notifier.PostAsync(place.Item1, text, place.Item2, ct);
        if (posted is not { } id) return false;

        await notifier.PinAsync(place.Item1, id, ct);

        tournament.ScoreboardChatId = place.Item1;
        tournament.ScoreboardThreadId = place.Item2;
        tournament.ScoreboardMessageId = id;
        return true;
    }

    /// <summary>Чат клана организатора и тема в нём; null — клана или чата нет.</summary>
    private async Task<(long, int?)?> ClanChatAsync(Tournament tournament, CancellationToken ct)
    {
        try
        {
            var creator = await players.GetByTelegramIdAsync(tournament.CreatorTelegramUserId, ct);
            if (creator?.ClanId is not { } clanId) return null;

            var clan = await clans.GetByIdAsync(clanId, ct);
            if (clan is null || clan.TelegramChatId == 0) return null;

            return (clan.TelegramChatId, clan.TelegramMessageThreadId);
        }
        catch { return null; }
    }

    private async Task PhotoAsync(long chatId, string photoUrl, string caption, CancellationToken ct)
    {
        try { await notifier.SendPhotoToChatAsync(chatId, photoUrl, caption, null, ct); }
        catch { /* личка могла быть закрыта — объявление в чат это не отменяет */ }
    }
}
