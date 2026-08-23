using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/app")]
public class AppController(ITelegramBotClient bot, IMemoryCache cache) : ControllerBase
{
    /// <summary>
    /// GET /api/app/config — то, что фронту нужно знать о боте во время работы.
    ///
    /// Юзернейм бота раньше подставлялся только на этапе сборки (VITE_BOT_USERNAME).
    /// Незаданная переменная в CI бесшумно убирала из интерфейса три кнопки —
    /// приглашение друга и обе кнопки «попросить подключить клан», — и заметить это
    /// можно было лишь случайно, по нулю приглашений за месяц. Сервер своего бота
    /// знает всегда, так что терять это в сборке больше негде.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        var username = await cache.GetOrCreateAsync("botusername", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            try
            {
                return (await bot.GetMe(ct)).Username;
            }
            catch
            {
                // Bot API не ответил — держим промах минуту, а не сутки
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return null;
            }
        });

        return Ok(new { botUsername = username ?? "" });
    }
}
