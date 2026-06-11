using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Infrastructure;
using ClanWarTracker.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<WarForecastService>();
builder.Services.AddScoped<GetClanStatusUseCase>();
builder.Services.AddScoped<GetPlayerStatsUseCase>();
builder.Services.AddScoped<GetSeasonStatsUseCase>();
builder.Services.AddScoped<CaptureWarSnapshotsUseCase>();
builder.Services.AddScoped<SendRemindersUseCase>();
builder.Services.AddScoped<LinkPlayerUseCase>();
builder.Services.AddScoped<SetupClanUseCase>();

builder.Services.AddHostedService<WarCheckWorker>();
builder.Services.AddHostedService<BotUpdateHandler>();

var host = builder.Build();

// Воркер тоже создаёт схему БД: на хостинге он может стартовать раньше API
await DependencyInjection.InitDatabaseAsync(host.Services);

// Логируем outbound IP — нужно для настройки токена Clash Royale API
try
{
    using var http = new HttpClient();
    var ip = await http.GetStringAsync("https://api.ipify.org");
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogInformation("=== OUTBOUND IP: {Ip} (добавь в токен CR API на developer.clashroyale.com) ===", ip.Trim());
}
catch { /* не критично */ }

host.Run();
