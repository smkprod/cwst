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

host.Run();
