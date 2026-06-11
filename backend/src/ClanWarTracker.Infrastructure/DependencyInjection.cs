using ClanWarTracker.Domain.Interfaces;
using ClanWarTracker.Infrastructure.ClashRoyale;
using ClanWarTracker.Infrastructure.Persistence;
using ClanWarTracker.Infrastructure.Persistence.Repositories;
using ClanWarTracker.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace ClanWarTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // БД: на хостинге (Render) — PostgreSQL через env DATABASE_URL,
        // локально — SQLite из ConnectionStrings:Default
        // Trim: хвостовой пробел/перенос строки при вставке в Render ломает строку подключения
        var databaseUrl = config["DATABASE_URL"]?.Trim();
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(ConvertPostgresUrl(databaseUrl)));
        }
        else
        {
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=clanwar.db"));
        }

        services.AddMemoryCache();

        // Токены: сперва плоские env-переменные (хостинг), затем appsettings (локально).
        // CleanToken убирает переносы строк/пробелы — длинные ключи часто вставляют с разрывами,
        // а заголовок Authorization падает с FormatException на любом \n.
        var crToken = CleanToken(config["CLASH_ROYALE_API_TOKEN"]) ?? CleanToken(config["ClashRoyale:ApiToken"]);
        var botToken = CleanToken(config["TELEGRAM_BOT_TOKEN"]) ?? CleanToken(config["Telegram:BotToken"]);

        // База CR API настраивается: на хостинге с плавающим IP (Render free) используем
        // прокси RoyaleAPI (https://proxy.royaleapi.dev/v1/) — в ключе тогда один IP 45.79.218.79.
        var crBaseUrl = config["CLASH_ROYALE_API_BASE_URL"]?.Trim();
        if (string.IsNullOrEmpty(crBaseUrl)) crBaseUrl = "https://api.clashroyale.com/v1/";
        if (!crBaseUrl.EndsWith('/')) crBaseUrl += "/";

        services.AddHttpClient<IClashRoyaleApi, ClashRoyaleApiClient>(http =>
        {
            http.BaseAddress = new Uri(crBaseUrl);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", crToken);
        });

        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken!));

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddScoped<IClanRepository, ClanRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IWarSnapshotRepository, WarSnapshotRepository>();

        return services;
    }

    /// <summary>
    /// Создаёт/обновляет схему БД при старте. SQLite — через миграции;
    /// PostgreSQL — EnsureCreated (миграции в репо сгенерированы под SQLite).
    /// Ретраи: на хостинге БД может подниматься позже сервиса.
    /// </summary>
    public static async Task InitDatabaseAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (db.Database.IsNpgsql())
                    await db.Database.EnsureCreatedAsync();
                else
                    await db.Database.MigrateAsync();
                return;
            }
            catch when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
        }
    }

    /// <summary>Убирает все пробельные символы (включая \r\n) из токена. null, если пусто.</summary>
    public static string? CleanToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = string.Concat(raw.Where(c => !char.IsWhiteSpace(c)));
        return cleaned.Length > 0 ? cleaned : null;
    }

    /// <summary>
    /// Render даёт DATABASE_URL вида postgres://user:pass@host:5432/db —
    /// Npgsql ждёт классическую строку подключения.
    /// </summary>
    private static string ConvertPostgresUrl(string url)
    {
        if (!url.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            return url; // уже обычная строка подключения — отдаём как есть

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        // Internal Render host (dpg-*) не требует SSL; внешние хосты — требуют.
        // SSL Mode=Prefer: используем SSL если доступен, иначе без него.
        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={pass};" +
               "SSL Mode=Prefer;Trust Server Certificate=true";
    }
}
