using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum ClanMessageError
{
    PlayerNotLinked,
    NoClan,
    TargetNotFound,
    SameClan,
    NotAllowed,
    TargetOptedOut,
    TooSoon,
    DailyLimit,
    EmptyText,
    TooLong,
    ChallengeNeedsSponsor,
    TargetHasNoChat,
}

/// <summary>
/// Сообщение от клана клану через бота.
///
/// Фича по своей природе — канал рассылки, и именно поэтому обвешана
/// ограничениями. Через чужого бота рассылают ровно до первой жалобы, после
/// которой Telegram блокирует не отправителя, а бота: пострадают все шестнадцать
/// кланов, а не тот, кто написал лишнего. Поэтому здесь три независимых предела —
/// кто вправе писать, как часто паре и сколько всего в сутки, — плюс выключатель
/// у получателя, чтобы у недовольного был выход, не доходящий до кнопки «пожаловаться».
/// </summary>
public class SendClanMessageUseCase(
    IPlayerRepository players,
    IClanRepository clans,
    IClanMessageRepository messages,
    IClashRoyaleApi crApi,
    INotificationSender notifier)
{
    /// <summary>Сколько ждать до следующего сообщения той же паре.</summary>
    private static readonly TimeSpan PairCooldown = TimeSpan.FromHours(24);

    /// <summary>
    /// Сколько разных кланов можно обойти за сутки.
    ///
    /// Без этого предела ограничение на пару обходится веером: шестнадцать
    /// сообщений шестнадцати кланам — формально по одному каждому, фактически
    /// рассылка по всему сервису.
    /// </summary>
    private const int MaxPerDay = 3;

    private const int MaxLength = 300;

    public async Task<ClanMessageError?> ExecuteAsync(
        long telegramUserId, int toClanId, string text, ClanMessageKind kind, CancellationToken ct = default)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0) return ClanMessageError.EmptyText;
        if (text.Length > MaxLength) return ClanMessageError.TooLong;

        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return ClanMessageError.PlayerNotLinked;
        if (player.ClanId is null) return ClanMessageError.NoClan;
        if (player.ClanId.Value == toClanId) return ClanMessageError.SameClan;

        var from = await clans.GetByIdAsync(player.ClanId.Value, ct);
        var to = await clans.GetByIdAsync(toClanId, ct);
        if (from is null || to is null) return ClanMessageError.TargetNotFound;
        if (!to.AcceptsClanMail) return ClanMessageError.TargetOptedOut;
        // Некуда доставить: клан заведён автоматически и к чату не привязан.
        if (to.TelegramChatId == 0) return ClanMessageError.TargetHasNoChat;

        var now = DateTime.UtcNow;
        var sponsor = player.IsSponsor(now);

        // Писать вправе те, кто и так говорит от имени клана, плюс спонсор: за это
        // он и платит. Рядовой участник от имени клана не пишет — иначе «сообщение
        // клана» перестаёт что-либо значить.
        if (!sponsor && !await IsClanLeaderAsync(from, player, ct)) return ClanMessageError.NotAllowed;

        // Вызов — возможность клана со спонсором, а не любого желающего.
        if (kind == ClanMessageKind.Challenge)
        {
            var clanSponsors = (await players.GetByClanIdAsync(from.Id, ct)).Any(p => p.IsSponsor(now));
            if (!clanSponsors) return ClanMessageError.ChallengeNeedsSponsor;
        }

        var lastToTarget = await messages.GetLastSentAtAsync(from.Id, to.Id, ct);
        if (lastToTarget is { } last && now - last < PairCooldown) return ClanMessageError.TooSoon;

        var sentToday = await messages.CountSentSinceAsync(from.Id, now.AddDays(-1), ct);
        if (sentToday >= MaxPerDay) return ClanMessageError.DailyLimit;

        await messages.AddAsync(new ClanMessage
        {
            FromClanId = from.Id,
            ToClanId = to.Id,
            SentByTelegramUserId = telegramUserId,
            SentByName = player.Name,
            Kind = kind,
            Text = text,
            SentAtUtc = now,
        }, ct);
        await messages.SaveChangesAsync(ct);

        // Доставляем после записи: сообщение, которое ушло, но не попало в журнал,
        // обошло бы ограничение — отправитель повторил бы его сразу же.
        await notifier.SendToChatAsync(
            to.TelegramChatId,
            Compose(from, player.Name, kind, text, sponsor),
            to.TelegramMessageThreadId,
            ct: ct);

        return null;
    }

    private async Task<bool> IsClanLeaderAsync(Clan clan, Player player, CancellationToken ct)
    {
        try
        {
            var role = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player.PlayerTag, ct);
            return role is "leader" or "coLeader";
        }
        catch
        {
            // CR API молчит — не пускаем. Ошибиться в сторону «не отправили»
            // дешевле, чем в сторону «написали от имени клана кому попало».
            return false;
        }
    }

    private static string Compose(Clan from, string author, ClanMessageKind kind, string text, bool sponsor)
    {
        var head = kind == ClanMessageKind.Challenge
            ? $"⚔️ Вызов от клана {from.Name} ({from.ClanTag})"
            : $"✉️ Сообщение от клана {from.Name} ({from.ClanTag})";

        var mark = sponsor ? " ★" : "";

        // Последняя строка не вежливость, а предохранитель: у того, кому сообщение
        // не нужно, должен быть выход ближе, чем кнопка «пожаловаться» на бота.
        return $"{head}\n\n{text}\n\n— {author}{mark}\n\n"
             + "Не хотите получать такие сообщения — выключите их в настройках бота.";
    }
}
