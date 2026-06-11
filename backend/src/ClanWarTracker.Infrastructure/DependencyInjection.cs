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
        var databaseUrl = config["DATABASE_URL"];
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

        // Токены: сперва плоские env-переменные (хостинг), затем appsettings (локально)
        var crToken = config["CLASH_ROYALE_API_TOKEN"] ?? config["ClashRoyale:ApiToken"];
        var botToken = config["TELEGRAM_BOT_TOKEN"] ?? config["Telegram:BotToken"];

        services.AddHttpClient<IClashRoyaleApi, ClashRoyaleApiClient>(http =>
        {
            http.BaseAddress = new Uri("https://api.clashroyale.com/v1/");
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

        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={pass};" +
               "SSL Mode=Require;Trust Server Certificate=true";
    }
}
