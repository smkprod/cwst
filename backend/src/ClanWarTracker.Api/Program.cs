using System.Threading.RateLimiting;
using ClanWarTracker.Api.Auth;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<WarForecastService>();
builder.Services.AddScoped<GetClanStatusUseCase>();
builder.Services.AddScoped<GetClanDisciplineUseCase>();
builder.Services.AddScoped<GetPlayerStatsUseCase>();
builder.Services.AddScoped<GetClanHistoryUseCase>();
builder.Services.AddScoped<GetSeasonStatsUseCase>();
builder.Services.AddScoped<GetSeasonBreakdownUseCase>();
builder.Services.AddScoped<GetSeasonArchiveUseCase>();
builder.Services.AddScoped<GetGlobalTopUseCase>();
builder.Services.AddScoped<GetAchievementsUseCase>();
builder.Services.AddScoped<GetWhatsNewUseCase>();
builder.Services.AddScoped<GiveRespectUseCase>();
builder.Services.AddScoped<SuggestDecksUseCase>();
builder.Services.AddScoped<NudgePlayersUseCase>();
builder.Services.AddScoped<OwnerBroadcastUseCase>();
builder.Services.AddScoped<GetOwnerDashboardUseCase>();
builder.Services.AddScoped<GetOwnerClanDetailUseCase>();
builder.Services.AddScoped<SetClanPlanUseCase>();
builder.Services.AddScoped<LinkPlayerUseCase>();
builder.Services.AddScoped<SetupClanUseCase>();
builder.Services.AddScoped<TournamentBracketService>();
builder.Services.AddScoped<CreateTournamentUseCase>();
builder.Services.AddScoped<JoinTournamentUseCase>();
builder.Services.AddScoped<LeaveTournamentUseCase>();
builder.Services.AddScoped<UpdateTournamentUseCase>();
builder.Services.AddScoped<GenerateTournamentBracketUseCase>();
builder.Services.AddScoped<StartTournamentUseCase>();
builder.Services.AddScoped<SetTournamentMatchResultUseCase>();
builder.Services.AddScoped<FinishTournamentUseCase>();
builder.Services.AddScoped<CancelTournamentUseCase>();
builder.Services.AddScoped<GetTournamentUseCase>();
builder.Services.AddScoped<GetTournamentListUseCase>();
builder.Services.AddScoped<GetPlayerTournamentHistoryUseCase>();
builder.Services.AddScoped<GameTournamentService>();
builder.Services.AddControllers();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Rate limiting: один пользователь не должен иметь возможность завалить
// общий токен Clash Royale API запросами по произвольным тегам.
// Партиция по проверенному TelegramUserId (фолбэк на IP). Окно: 120 запросов/мин —
// намного выше легитимной нагрузки (Mini App опрашивает /my/status раз в минуту),
// но останавливает перебор тегов. Не-/api пути (статика) не лимитируются.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
            return RateLimitPartition.GetNoLimiter("static");

        var key = ctx.Items.TryGetValue("TelegramUserId", out var uid) && uid is long id
            ? $"user:{id}"
            : $"ip:{ctx.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "rate_limited", message = "Слишком много запросов — подожди немного" }, token);
    };
});

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
// После auth — чтобы партиция лимитера видела проверенный TelegramUserId в ctx.Items
app.UseRateLimiter();
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