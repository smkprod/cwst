using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Api.Auth;

/// <summary>
/// Авторизация запросов к /api по initData Telegram Mini App.
/// 401 c кодом:
///   no_init_data  — приложение открыто не через бота (обычный браузер / ссылка),
///   bad_init_data — подпись не сошлась или данные устарели.
/// В Development при пустом initData пускаем под Telegram:DevUserId (локальная разработка).
/// </summary>
public class TelegramAuthMiddleware(RequestDelegate next, IConfiguration config, IHostEnvironment env,
    ILogger<TelegramAuthMiddleware> logger, IMemoryCache cache)
{
    /// <summary>Как часто перепроверяем @username одного пользователя (он меняется редко).</summary>
    private static readonly TimeSpan UsernameSyncInterval = TimeSpan.FromHours(6);

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        // Картинки для inline-режима качает сам Telegram со своих серверов — initData
        // у него взяться неоткуда. Отдаём там только то, что и так открыто в публичном
        // профиле игрока Clash Royale, так что закрывать эту ручку нечего.
        if (ctx.Request.Path.StartsWithSegments("/api/img"))
        {
            await next(ctx);
            return;
        }

        // Версия сборки: нужна ровно тогда, когда что-то пошло не так и надо понять,
        // какой код крутится на сервере. Требовать для этого Telegram — значит лишить
        // себя диагностики в тот момент, когда она нужнее всего.
        if (ctx.Request.Path.StartsWithSegments("/api/version"))
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

        if (!TelegramInitDataValidator.TryValidate(initData, botToken, out var userId, out var username))
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
        await SyncUsernameAsync(ctx, userId, username);
        await TrackActivityAsync(ctx, userId);
        await next(ctx);
    }

    /// <summary>
    /// Отмечает активный день игрока.
    ///
    /// Считаем здесь, а не в каждом контроллере: любой запрос из Mini App проходит
    /// через авторизацию, и это единственное место, где видно вообще всех.
    ///
    /// Изменяющим действием считаем не-GET: пинок, респект, ответ в игре, настройки.
    /// Открытие приложения тоже пишем, но без счётчика — сам факт строки уже значит,
    /// что человек заходил.
    ///
    /// Кэш нужен, чтобы не ходить в БД на каждый запрос: за день на человека
    /// получается максимум одна запись о заходе плюс редкие инкременты действий.
    /// </summary>
    private async Task TrackActivityAsync(HttpContext ctx, long userId)
    {
        var isAction = !HttpMethods.IsGet(ctx.Request.Method);
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Заход отмечаем раз в сутки, действия пишем всегда: их количество и есть
        // то, что отличает «зашёл посмотреть» от «поработал».
        var cacheKey = $"act:{userId}:{day}";
        if (!isAction && cache.TryGetValue(cacheKey, out _)) return;

        try
        {
            var players = ctx.RequestServices.GetRequiredService<IPlayerRepository>();
            var player = await players.GetByTelegramIdAsync(userId, ctx.RequestAborted);
            if (player is null) return;   // ещё не привязался — считать нечего

            await ctx.RequestServices.GetRequiredService<IActivityRepository>()
                .TouchAsync(player.Id, day, isAction, ctx.RequestAborted);

            cache.Set(cacheKey, true, new MemoryCacheEntryOptions
            {
                Size = 1,
                // До конца суток плюс запас: раньше сбросится — просто лишний запрос
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
            });
        }
        catch (Exception ex)
        {
            // Статистика посещаемости не повод ронять запрос пользователя
            logger.LogWarning(ex, "Не удалось отметить активность {UserId}", userId);
        }
    }

    /// <summary>
    /// Подтягивает @username из initData в БД. Раньше он записывался только при /link,
    /// поэтому у привязавшихся другим путём (или сменивших ник) его не было — а без него
    /// бот не может тегнуть человека в чате. Пишем только при реальном изменении и не чаще
    /// раза в 6 часов на пользователя, чтобы не дёргать БД на каждый запрос Mini App.
    /// </summary>
    private async Task SyncUsernameAsync(HttpContext ctx, long userId, string? username)
    {
        if (string.IsNullOrEmpty(username)) return;

        var cacheKey = $"tguser:{userId}";
        if (cache.TryGetValue(cacheKey, out string? known) && known == username) return;

        try
        {
            var players = ctx.RequestServices.GetRequiredService<IPlayerRepository>();
            var player = await players.GetByTelegramIdAsync(userId, ctx.RequestAborted);
            if (player is not null && player.TelegramUsername != username)
            {
                player.TelegramUsername = username;
                await players.SaveChangesAsync(ctx.RequestAborted);
            }
            cache.Set(cacheKey, username, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = UsernameSyncInterval,
            });
        }
        catch (Exception ex)
        {
            // Не роняем запрос: юзернейм — приятный бонус, а не условие авторизации
            logger.LogWarning(ex, "Не удалось обновить @username для {UserId}", userId);
        }
    }
}
