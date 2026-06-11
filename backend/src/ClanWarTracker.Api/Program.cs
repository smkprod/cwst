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

app.UseCors();
app.UseMiddleware<TelegramAuthMiddleware>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ==========================================
// ДОБАВЬТЕ ЭТИ СТРОКИ СЮДА (ПЕРЕД app.Run())
// ==========================================
app.UseDefaultFiles(); // Ищет index.html в папке wwwroot по умолчанию
app.UseStaticFiles();  // Разрешает раздачу js, css, картинок из wwwroot

// Если пользователь перейдет по любому другому пути (например, в самом React-роутере),
// сервер вернет ему index.html, чтобы фронтенд сам обработал этот роут
app.MapFallbackToFile("index.html"); 
// ==========================================

app.Run();