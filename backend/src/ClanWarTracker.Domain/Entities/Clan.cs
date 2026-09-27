using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class Clan
{
    public int Id { get; set; }
    public required string ClanTag { get; set; }         // #CLAN123
    public required string Name { get; set; }
    public long TelegramChatId { get; set; }             // группа, к которой привязан клан

    /// <summary>
    /// ID темы (Topic) форума в группе, куда слать напоминания/отчёты, если /setup был
    /// выполнен внутри темы. Null — группа без тем или /setup выполнен в общем чате.
    /// </summary>
    public int? TelegramMessageThreadId { get; set; }
    public int ReminderHoursBeforeEnd { get; set; } = 3; // отправлять за X часов до конца дня

    /// <summary>
    /// Принимает ли клан сообщения от других кланов.
    ///
    /// Включено по умолчанию, но выключатель обязан быть и обязан быть заметным:
    /// без него единственный способ прекратить нежелательные сообщения — жалоба
    /// на бота, и наказан будет бот, а не тот, кто написал.
    /// </summary>
    public bool AcceptsClanMail { get; set; } = true;

    /// <summary>
    /// Гибкие настройки уведомлений в JSON (вкл/выкл и канал ЛС/чат по каждому типу).
    /// Null — используются значения по умолчанию (всё включено, канал «везде»).
    /// Хранится как JSON, чтобы расширять без новых миграций БД.
    /// </summary>
    public string? NotificationSettingsJson { get; set; }

    /// <summary>
    /// Ключ последней недели ("season:section"), для которой уже отправлен анонс начала КВ.
    /// Хранится в БД, чтобы анонс НЕ повторялся при перезапуске воркера (деплое). null — ещё не слали.
    /// </summary>
    public string? LastWarStartKey { get; set; }

    // --- Бывший тариф Pro ---
    // Тарифа больше нет: платная модель - спонсорство, всё, что закрывал Pro, открыто
    // всем. Свойства остаются ТОЛЬКО ради колонок: в базах, созданных через
    // EnsureCreated, они NOT NULL без значения по умолчанию, и если EF перестанет их
    // писать, вставка нового клана упадёт - то есть сломается /setup. Не читать.
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
    public DateTime? PlanExpiresAtUtc { get; set; }
    public PlanReminderStage PlanReminderStageSent { get; set; } = PlanReminderStage.None;

    /// <summary>Когда клан подключили к боту. null — подключён до появления поля.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    // --- Страница клана на Аллее славы ---

    /// <summary>
    /// Оформление страницы клана — см. <see cref="ClanPageDesign"/>. null — обычная.
    ///
    /// Лежит на клане, а не на спонсоре, специально: спонсорство заканчивается, а
    /// страница клана после этого меняться сама не должна.
    /// </summary>
    public string? PageDesignKey { get; set; }

    /// <summary>Девиз на странице клана. null — строки нет вообще, а не пустая.</summary>
    public string? Motto { get; set; }

    public List<Player> Players { get; set; } = [];

}
