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

// Какая БД используется — видно сразу в логах (диагностика DATABASE_URL на Render)
{
    var dbUrl = builder.Configuration["DATABASE_URL"]?.Trim();
    var dbInfo = string.IsNullOrEmpty(dbUrl)
        ? "SQLite (DATABASE_URL не задан — на Render это ошибка конфигурации!)"
        : $"PostgreSQL, host={(Uri.TryCreate(dbUrl, UriKind.Absolute, out var u) ? u.Host : "не удалось разобрать URL")}";
    host.Services.GetRequiredService<ILogger<Program>>().LogInformation("=== DATABASE: {Db} ===", dbInfo);
}

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

// Разрешённые IP внутри токена CR (JWT payload) — сверить с OUTBOUND IP выше
try
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    var crToken = DependencyInjection.CleanToken(builder.Configuration["CLASH_ROYALE_API_TOKEN"])
                  ?? DependencyInjection.CleanToken(builder.Configuration["ClashRoyale:ApiToken"]);
    if (crToken is null)
    {
        logger.LogWarning("=== CR TOKEN: не задан вообще! ===");
    }
    else
    {
        var payload = crToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
        var cidrs = json.RootElement.GetProperty("limits").EnumerateArray()
            .Where(l => l.TryGetProperty("cidrs", out _))
            .SelectMany(l => l.GetProperty("cidrs").EnumerateArray().Select(c => c.GetString()))
            .ToList();
        logger.LogInformation("=== CR TOKEN: разрешённые IP: {Cidrs} — должны совпадать с OUTBOUND IP ===",
            string.Join(", ", cidrs));
    }
}
catch (Exception ex)
{
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogWarning("Не удалось разобрать токен CR: {Error}", ex.Message);
}

host.Run();
