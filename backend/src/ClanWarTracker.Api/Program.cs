using ClanWarTracker.Api.Auth;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<WarForecastService>();
builder.Services.AddScoped<GetClanStatusUseCase>();
builder.Services.AddScoped<GetPlayerStatsUseCase>();
builder.Services.AddScoped<GetClanHistoryUseCase>();
builder.Services.AddScoped<GetSeasonStatsUseCase>();
builder.Services.AddScoped<GetSeasonBreakdownUseCase>();
builder.Services.AddScoped<GetGlobalTopUseCase>();
builder.Services.AddScoped<NudgePlayersUseCase>();
builder.Services.AddScoped<SetClanPlanUseCase>();
builder.Services.AddScoped<LinkPlayerUseCase>();
builder.Services.AddScoped<SetupClanUseCase>();
builder.Services.AddControllers();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Схема БД при старте: SQLite — миграции, PostgreSQL (Render) — EnsureCreated, с ретраями
await DependencyInjection.InitDatabaseAsync(app.Services);

// Логируем outbound IP — нужно для настройки токена Clash Royale API
try
{
    using var ipHttp = new HttpClient();
    var ip = await ipHttp.GetStringAsync("https://api.ipify.org");
    app.Services.GetRequiredService<ILogger<Program>>()
        .LogInformation("=== OUTBOUND IP: {Ip} (добавь в токен CR API на developer.clashroyale.com) ===", ip.Trim());
}
catch { /* не критично */ }

app.UseCors();
app.UseMiddleware<TelegramAuthMiddleware>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Кэширование статики: WebView Телеграма агрессивно кэширует index.html и после
// деплоя продолжает грузить старый JS-бандл. index.html — всегда перепроверять;
// /assets/* безопасно кэшировать навсегда (имена файлов содержат хэш содержимого).
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl =
            ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                ? "no-cache, no-store, must-revalidate"
                : "public, max-age=31536000, immutable";
    }
};

app.UseDefaultFiles(); // Ищет index.html в папке wwwroot по умолчанию
app.UseStaticFiles(staticFiles);

// Любой нефайловый путь (роуты React) → index.html
app.MapFallbackToFile("index.html", staticFiles);

app.Run();