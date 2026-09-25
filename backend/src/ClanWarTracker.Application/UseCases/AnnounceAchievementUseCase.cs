using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Объявление открытой награды в чат клана — картинкой и только у спонсора.
///
/// Награды считались и раньше, но жили внутри приложения: человек однажды
/// замечал, что значок стал золотым. В чате их не видел никто, а статус, которого
/// не видят, статусом не работает. Спонсорство это и меняет: его награды видит
/// весь клан, причём не строчкой, а карточкой, которую в ленте не пролистать.
/// </summary>
public class AnnounceAchievementUseCase(
    IClanRepository clans,
    INotificationSender notifier,
    ICardUrls cardUrls)
{
    /// <summary>Подписи наград. Совпадают с теми, что человек видит в приложении.</summary>
    private static readonly Dictionary<string, (string Icon, string Title)> Badges = new()
    {
        ["streak"] = ("🔥", "Недели подряд"),
        ["dailyStreak"] = ("📆", "Дни подряд"),
        ["perfectDays"] = ("💯", "Идеальный день"),
        ["mvpWeeks"] = ("👑", "Лучший недели"),
        ["totalFame"] = ("🏅", "Медали за всё время"),
        ["warsPlayed"] = ("⚔️", "Сыграно войн"),
        ["perfectWeeks"] = ("💎", "Идеальная неделя"),
        ["perfectSeasons"] = ("🏆", "Идеальный сезон"),
        ["boatAttacks"] = ("🚤", "Атаки по лодке"),
    };

    /// <summary>
    /// Награды, у которых первый уровень уже празднуется отдельно.
    ///
    /// Первый идеальный день объявляет SendPerfectDayUseCase — своей карточкой и
    /// с шуткой. Вторая карточка про то же самое событие читается как сбой бота,
    /// а не как двойной повод. Поэтому «Идеальный день» подхватываем только с
    /// пятого и пятнадцатого: там своего объявления нет, и это уже не тот же
    /// момент, а накопленная веха.
    /// </summary>
    private static readonly Dictionary<string, int> AnnouncedElsewhere = new()
    {
        ["perfectDays"] = 1,
    };

    /// <param name="badges">Витрина целиком — из неё берётся уровень открытой награды.</param>
    /// <param name="unlocked">Ключи наград, открытых с прошлого раза.</param>
    public async Task ExecuteAsync(
        Player player,
        IReadOnlyList<AchievementDto> badges,
        IReadOnlyList<string> unlocked,
        CancellationToken ct = default)
    {
        if (player.ClanId is not int clanId) return;

        var clan = await clans.GetByIdAsync(clanId, ct);
        // Клан без привязанного чата — доставить некуда. Это не ошибка: так
        // выглядят кланы, заведённые ботом автоматически.
        if (clan is null || clan.TelegramChatId == 0) return;

        var levels = badges.ToDictionary(b => b.Key, b => b.Level);

        // Объявляем одну, даже если открылось несколько. Три карточки подряд в
        // общем чате читаются как сбой, а не как повод порадоваться.
        var key = unlocked.FirstOrDefault(k =>
            Badges.ContainsKey(k)
            && !(AnnouncedElsewhere.TryGetValue(k, out var covered)
                 && levels.GetValueOrDefault(k) == covered));
        if (key is null || !Badges.TryGetValue(key, out var badge)) return;

        // Эмодзи здесь уместен: это текст Telegram, а не рисованная карточка —
        // шрифт ищет клиент, и квадратов не будет.
        var text = $"★ {player.Name} открыл награду: {badge.Icon} {badge.Title}";
        var photo = cardUrls.Card("achievement", $"{player.PlayerTag.TrimStart('#')}~{key}");

        // Без публичного адреса картинку слать неоткуда — уходит текстом.
        // Сообщение без карточки лучше, чем отсутствие сообщения.
        var sent = photo is not null && await notifier.SendPhotoToChatAsync(
            clan.TelegramChatId, photo, text, clan.TelegramMessageThreadId, ct);

        if (!sent)
            await notifier.SendToChatAsync(clan.TelegramChatId, text, clan.TelegramMessageThreadId, ct: ct);
    }
}
