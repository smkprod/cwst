using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/app")]
public class AppController(
    ITelegramBotClient bot,
    IMemoryCache cache,
    IServiceSettingRepository settings,
    IPlayerRepository players,
    IConfiguration config) : ControllerBase
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

        // Состав вкладок владелец меняет из панели, поэтому он приезжает сюда, а не
        // зашит во фронт: иначе на каждую перестановку значка нужен был бы передеплой.
        var tabs = AppTabs.Parse(await settings.GetAsync(AppTabs.SettingKey, ct));

        // Своё спонсорство фронт спрашивает здесь же: оно решает, показывать ли
        // выбор фона и рисовать ли ярлык, а отдельный запрос ради двух полей лишний.
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var me = await players.GetByTelegramIdAsync(userId, ct);
        var isSponsor = me?.IsSponsor(DateTime.UtcNow) == true;

        return Ok(new
        {
            botUsername = username ?? "",
            tabs,
            playerBackgrounds = SponsorBackground.ForPlayers,
            clanBackgrounds = SponsorBackground.ForClans,
            isSponsor,
            sponsorUntil = isSponsor ? me!.SponsorUntilUtc : null,
            myBackground = isSponsor ? SponsorBackground.NormalizePlayer(me!.SponsorBackgroundKey) : null,
            myClanBackground = isSponsor ? SponsorBackground.NormalizeClan(me!.SponsorClanBackgroundKey) : null,
            // Кому писать за спонсорством. Пусто — кнопку не рисуем: ссылка в никуда
            // хуже отсутствующей кнопки.
            sponsorContact = config["Owner:Username"]?.Trim().TrimStart('@') ?? "",
        });
    }
}
