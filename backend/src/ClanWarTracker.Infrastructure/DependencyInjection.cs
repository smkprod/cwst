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
        // PostgreSQL (для локальной разработки можно заменить на SQLite — см. README)
        services.AddDbContext<AppDbContext>(o =>
           o.UseSqlite(config.GetConnectionString("Default")));

        services.AddMemoryCache();

        services.AddHttpClient<IClashRoyaleApi, ClashRoyaleApiClient>(http =>
        {
            http.BaseAddress = new Uri("https://api.clashroyale.com/v1/");
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", config["ClashRoyale:ApiToken"]);
        });

        services.AddSingleton<ITelegramBotClient>(
            new TelegramBotClient(config["Telegram:BotToken"]!));

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddScoped<IClanRepository, ClanRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IWarSnapshotRepository, WarSnapshotRepository>();

        return services;
    }
}
