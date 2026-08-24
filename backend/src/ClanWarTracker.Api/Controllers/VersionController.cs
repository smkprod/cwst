using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Какая сборка сейчас работает.
///
/// Появилась после того, как выкат несколько раз «не приезжал», и понять это можно было
/// только по внешнему виду карточек — то есть никак, потому что вид зависит ещё и от
/// кэша браузера и от кэша Telegram. Один честный ответ сервера снимает весь этот спор.
///
/// Без авторизации намеренно (см. исключение в TelegramAuthMiddleware): диагностика
/// нужна именно тогда, когда что-то сломано, и требовать для неё Telegram — значит
/// остаться без неё в самый неподходящий момент. Секретов здесь нет.
/// </summary>
[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    /// <summary>Момент запуска процесса — видно, перезапускался ли контейнер при деплое.</summary>
    private static readonly DateTime StartedUtc = DateTime.UtcNow;

    [HttpGet]
    public IActionResult Get()
    {
        var sha = Environment.GetEnvironmentVariable("GIT_SHA");
        return Ok(new
        {
            version = string.IsNullOrWhiteSpace(sha) ? "unknown" : sha,
            startedUtc = StartedUtc,
            uptimeMinutes = (int)(DateTime.UtcNow - StartedUtc).TotalMinutes,
        });
    }
}
