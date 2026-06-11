namespace ClanWarTracker.Api.Auth;

public class TelegramAuthMiddleware(RequestDelegate next, IConfiguration config, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();

            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") 
                           ?? config["TELEGRAM_BOT_TOKEN"]
                           ?? config["Telegram:BotToken"];

            if (string.IsNullOrEmpty(botToken))
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(new { error = "Токен бота вообще не найден в системе!" });
                return;
            }

            if (!string.IsNullOrEmpty(initData))
            {
                // Запускаем валидацию и ПРИНУДИТЕЛЬНО пишем в консоль сервера результат
                bool isValid = TelegramInitDataValidator.TryValidate(initData, botToken, out var userId);
                
                Console.WriteLine($"[ПРОДОЛЖАЕМ_ДЕБАГ] Результат валидации: {isValid}. ID Пользователя: {userId}");
                
                // ВРЕМЕННЫЙ ХАК: Пропускаем тебя, даже если валидация вернула false!
                // Это нужно, чтобы приложение открылось, а мы увидели логи.
                if (userId == 0) 
                {
                    // Попробуем вытащить ID без валидации, просто чтобы пустить
                    try {
                        var query = System.Web.HttpUtility.ParseQueryString(initData);
                        var userJson = query["user"];
                        if (userJson != null) {
                            using var doc = System.Text.Json.JsonDocument.Parse(System.Web.HttpUtility.UrlDecode(userJson));
                            userId = doc.RootElement.GetProperty("id").GetInt64();
                        }
                    } catch { userId = 324985752; } // твой ID из аппсеттингс как фолбэк
                }

                ctx.Items["TelegramUserId"] = userId;
            }
            else
            {
                Console.WriteLine("[ПРОДОЛЖАЕМ_ДЕБАГ] Фронтенд прислал ПУСТОЙ заголовок X-Telegram-Init-Data!");
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "Header is missing" });
                return;
            }
        }

        await next(ctx);
    }
}