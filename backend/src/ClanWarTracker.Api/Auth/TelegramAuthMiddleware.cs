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
            var botToken = config["Telegram:BotToken"]!;

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