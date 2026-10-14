using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Позвать непривязанных»: лидер одной кнопкой пишет в чат клана, кто из состава ещё не
/// привязал аккаунт, с кнопкой привязки. По ней игрок открывает приложение сразу со
/// списком своего клана и выбирает себя - без ввода тега.
///
/// Это пост в общий чат клана, а не личные сообщения: написать первым в личку тому, кто
/// не запускал бота, Telegram и не даст. Не чаще раза в сутки на клан - иначе это спам.
/// </summary>
public class InviteUnlinkedUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    ISentNotificationRepository sentLog,
    INotificationSender notifier)
{
    public const string SentKind = "invitelink";
    private const int NamesShown = 30;

    public enum Outcome { Ok, NotFound, NoChat, TooSoon, NobodyToInvite, SendFailed }

    public record Result(Outcome Outcome, int Unlinked = 0);

    public async Task<Result> ExecuteAsync(int clanId, CancellationToken ct = default)
    {
        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return new Result(Outcome.NotFound);
        if (clan.TelegramChatId == 0) return new Result(Outcome.NoChat);

        var key = clan.Id.ToString();
        var recent = await sentLog.GetKeysAsync(SentKind, DateTime.UtcNow.AddHours(-24), ct);
        if (recent.Contains(key)) return new Result(Outcome.TooSoon);

        var unlinked = await UnlinkedAsync(clan.Id, clan.ClanTag, ct);
        if (unlinked.Count == 0) return new Result(Outcome.NobodyToInvite);

        var t = NotificationSettings.Parse(clan.NotificationSettingsJson).Text;
        var names = string.Join(", ", unlinked.Take(NamesShown).Select(m => m.Name))
                    + (unlinked.Count > NamesShown ? " …" : "");
        var text = string.Format(t.InviteLinkText, unlinked.Count, names);
        var buttons = new List<IReadOnlyList<BotButton>>
        {
            new[] { new BotButton(t.InviteLinkButton, Url: "startapp:" + LinkParam(clan.ClanTag)) },
        };

        // Отметка - до отправки: двойное нажатие не должно дать два поста
        await sentLog.AddAsync(SentKind, key, ct);
        var sent = await notifier.SendToChatWithButtonsAsync(clan.TelegramChatId, text, buttons, clan.TelegramMessageThreadId, ct);
        return new Result(sent ? Outcome.Ok : Outcome.SendFailed, unlinked.Count);
    }

    /// <summary>Параметр запуска приложения «привязка к этому клану»: link_ТЕГ без решётки.</summary>
    public static string LinkParam(string clanTag) => "link_" + clanTag.TrimStart('#').ToUpperInvariant();

    /// <summary>
    /// Кто из текущего состава клана не привязан: ни Telegram-id, ни @username. Состав -
    /// из API игры (кто сейчас в клане), а не из списка войны, где есть и ушедшие.
    /// </summary>
    public async Task<List<Domain.Entities.CrClanMember>> UnlinkedAsync(int clanId, string clanTag, CancellationToken ct)
    {
        var roster = await crApi.GetClanRosterAsync(clanTag, ct);
        var linked = (await players.GetByClanIdAsync(clanId, ct))
            .Where(p => p.TelegramUserId is not null || !string.IsNullOrEmpty(p.TelegramUsername))
            .Select(p => p.PlayerTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roster.Where(m => !linked.Contains(m.Tag)).ToList();
    }
}
