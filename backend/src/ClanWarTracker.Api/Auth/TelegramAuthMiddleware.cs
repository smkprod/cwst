namespace ClanWarTracker.Api.Auth;

public class TelegramAuthMiddleware(RequestDelegate next, IConfiguration config, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            // DEV-режим: пропускаем проверку подписи, подставляем тестового пользователя.
            // Работает ТОЛЬКО при ASPNETCORE_ENVIRONMENT=Development — в проде недоступен.
            if (env.IsDevelopment() && config.GetValue<bool>("Telegram:SkipInitDataValidation"))
            {
                ctx.Items["TelegramUserId"] = config.GetValue<long>("Telegram:DevUserId");
                await next(ctx);
                return;
            }

            var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();

            // Читаем ТОЛЬКО плоскую переменную окружения (как Clash Royale токен)
            var botToken = Environment.GetEnvironmentVariable("[REMOVED-BOT-TOKEN]") 
                           ?? config["[REMOVED-BOT-TOKEN]"];

            // Если плоской нет, пробуем старый локальный вариант из appsettings.json (для ПК)
            if (string.IsNullOrEmpty(botToken))
            {
                botToken = config["Telegram:BotToken"] ?? config.GetSection("Telegram")["BotToken"];
            }

            // Если токен вообще не найден в системе (забыли добавить в Render)
            if (string.IsNullOrEmpty(botToken))
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await ctx.Response.WriteAsJsonAsync(new { error = "Telegram Bot Token is missing in server configuration. Make sure TELEGRAM_BOT_TOKEN is set in Render Environment Variables." });
                return;
            }

            if (initData is null ||
                !TelegramInitDataValidator.TryValidate(initData, botToken, out var userId))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "Invalid Telegram init data" });
                return;
            }

            ctx.Items["TelegramUserId"] = userId;
        }

        await next(ctx);
    }
}