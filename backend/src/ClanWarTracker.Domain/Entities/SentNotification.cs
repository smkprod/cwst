namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Отметка «это сообщение уже отправлено». Существует ради одного: не написать в чат
/// дважды одно и то же.
///
/// Раньше такие отметки жили только в памяти воркера, и любой рестарт — деплой, падение,
/// перезапуск контейнера хостингом — стирал их подчистую. После рестарта бот заново
/// поздравлял всех, кто уже набрал 900 в этот день, потому что условие «набрал 900»
/// остаётся истинным до конца военного дня. То же самое грозило отчётам, напоминаниям
/// и последнему звонку.
/// </summary>
public class SentNotification
{
    public int Id { get; set; }

    /// <summary>Вид уведомления: perfectday, dailyreport, finalcall, reminder, briefing, respectdigest.</summary>
    public required string Kind { get; set; }

    /// <summary>Ключ конкретного события внутри вида — например «клан:сезон:неделя:день:тег».</summary>
    public required string Key { get; set; }

    public DateTime SentAtUtc { get; set; }
}
