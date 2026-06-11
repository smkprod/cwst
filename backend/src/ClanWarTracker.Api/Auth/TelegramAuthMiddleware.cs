namespace ClanWarTracker.Api.Auth;

/// <summary>
/// Авторизация запросов к /api по initData Telegram Mini App.
/// 401 c кодом:
///   no_init_data  — приложение открыто не через бота (обычный браузер / ссылка),
///   bad_init_data — подпись не сошлась или данные устарели.
/// В Development при пустом initData пускаем под Telegram:DevUserId (локальная разработка).
/// </summary>
public class TelegramAuthMiddleware(RequestDelegate next, IConfiguration config, IHostEnvironment env,
    ILogger<TelegramAuthMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        var botToken = ClanWarTracker.Infrastructure.DependencyInjection.CleanToken(
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
            ?? config["TELEGRAM_BOT_TOKEN"]
            ?? config["Telegram:BotToken"]);

        if (string.IsNullOrEmpty(botToken))
        {
            logger.LogError("TELEGRAM_BOT_TOKEN не задан — авторизация невозможна");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "server_misconfigured", message = "Бот-токен не настроен" });
            return;
        }

        var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();

        if (string.IsNullOrEmpty(initData))
        {
            // Локальная разработка: пускаем фиктивного пользователя из конфига
            if (env.IsDevelopment() && long.TryParse(config["Telegram:DevUserId"], out var devUserId) && devUserId != 0)
            {
                ctx.Items["TelegramUserId"] = devUserId;
                await next(ctx);
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "no_init_data",
                message = "Открой приложение через кнопку бота в Telegram"
            });
            return;
        }

        if (!TelegramInitDataValidator.TryValidate(initData, botToken, out var userId))
        {
            logger.LogWarning("Невалидный initData (len={Len})", initData.Length);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "bad_init_data",
                message = "Не удалось проверить данные Telegram — попробуй переоткрыть приложение"
            });
            return;
        }

        ctx.Items["TelegramUserId"] = userId;
        await next(ctx);
    }
}
