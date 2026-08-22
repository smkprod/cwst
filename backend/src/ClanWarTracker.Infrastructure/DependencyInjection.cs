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

        // SizeLimit ограничивает число записей в кэше: ключи содержат теги от пользователя
        // (war:{tag}, playerinfo:{tag}…), поэтому без лимита поток запросов с разными тегами
        // раздувает память до OOM. Каждая запись имеет Size=1; 20k — потолок намного выше
        // легитимной нагрузки (сотни кланов), но защищает от исчерпания памяти.
        services.AddMemoryCache(o => o.SizeLimit = 20_000);

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

        // Таймаут обязателен: по умолчанию HttpClient ждёт 100 секунд, и подвисший
        // CR API держал бы наш запрос всё это время — Mini App успевал показать ошибку.
        // 12 секунд с запасом хватает живому API и быстро отсекает мёртвый.
        services.AddHttpClient<IClashRoyaleApi, ClashRoyaleApiClient>(http =>
        {
            http.BaseAddress = new Uri(crBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(12);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", crToken);
        })
        .AddHttpMessageHandler(() => new TransientRetryHandler());

        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken!));

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddScoped<IClanRepository, ClanRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IWarSnapshotRepository, WarSnapshotRepository>();
        services.AddScoped<IRecruitmentRepository, RecruitmentRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<IGameTournamentRepository, GameTournamentRepository>();
        services.AddScoped<IWarBattleRepository, WarBattleRepository>();
        services.AddScoped<IRespectRepository, RespectRepository>();
        services.AddScoped<ISentNotificationRepository, SentNotificationRepository>();

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
                {
                    await db.Database.EnsureCreatedAsync();
                    // EnsureCreated не добавляет колонки в уже существующие таблицы.
                    // Идемпотентно дотягиваем схему до текущей модели (для Postgres миграций нет).
                    await EnsureNpgsqlColumnsAsync(db);
                }
                else
                {
                    await db.Database.MigrateAsync();
                }
                return;
            }
            catch when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
        }
    }

    /// <summary>
    /// Идемпотентно добавляет недостающие колонки в существующую PostgreSQL-схему.
    /// Нужно потому, что для Postgres схема создаётся через EnsureCreated (без миграций),
    /// а он не умеет дотягивать новые поля в уже созданные таблицы.
    /// Каждая новая колонка модели — одна строка ADD COLUMN IF NOT EXISTS здесь.
    /// </summary>
    private static async Task EnsureNpgsqlColumnsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Clans\" ADD COLUMN IF NOT EXISTS \"PlanReminderStageSent\" integer NOT NULL DEFAULT 0;");

        // Тема (Topic) форума группы, куда слать напоминания/отчёты — если /setup был выполнен внутри темы.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Clans\" ADD COLUMN IF NOT EXISTS \"TelegramMessageThreadId\" integer;");

        // Гибкие настройки уведомлений (вкл/выкл + канал по каждому типу) в JSON.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Clans\" ADD COLUMN IF NOT EXISTS \"NotificationSettingsJson\" text;");

        // Дедуп анонса начала КВ (чтобы не повторялся при деплое/рестарте воркера).
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Clans\" ADD COLUMN IF NOT EXISTS \"LastWarStartKey\" text;");

        // Журнал отправленных уведомлений: дедуп пережил бы рестарт воркера.
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""SentNotifications"" (
    ""Id"" serial PRIMARY KEY,
    ""Kind"" varchar(32) NOT NULL,
    ""Key"" varchar(200) NOT NULL,
    ""SentAtUtc"" timestamptz NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SentNotifications_Kind_Key\" ON \"SentNotifications\" (\"Kind\", \"Key\");");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_SentNotifications_SentAtUtc\" ON \"SentNotifications\" (\"SentAtUtc\");");

        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""RecruitmentProfiles"" (
    ""Id"" serial PRIMARY KEY,
    ""PlayerTag"" varchar(16) NOT NULL,
    ""TelegramUserId"" bigint NOT NULL,
    ""Name"" varchar(64) NOT NULL,
    ""Note"" varchar(500),
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""CreatedAtUtc"" timestamptz NOT NULL,
    ""UpdatedAtUtc"" timestamptz NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_RecruitmentProfiles_PlayerTag\" ON \"RecruitmentProfiles\" (\"PlayerTag\");");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_RecruitmentProfiles_TelegramUserId\" ON \"RecruitmentProfiles\" (\"TelegramUserId\");");

        // Игроки без клана: ClanId теперь nullable
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ALTER COLUMN \"ClanId\" DROP NOT NULL;");

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"LastSmartAlertSentAt\" timestamptz;");

        // Реферальная атрибуция: кто пригласил игрока (добавлено после первого релиза).
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"ReferrerTelegramUserId\" bigint;");

        // @username игрока в Telegram — для тегов по юзернейму в чате.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"TelegramUsername\" text;");

        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""Tournaments"" (
    ""Id"" serial PRIMARY KEY,
    ""Name"" varchar(80) NOT NULL,
    ""Description"" varchar(2000),
    ""PrizeInfo"" varchar(500),
    ""ClanInviteLink"" varchar(300) NOT NULL,
    ""CreatorTelegramUserId"" bigint NOT NULL,
    ""CreatorPlayerTag"" varchar(16) NOT NULL,
    ""CreatorName"" varchar(64) NOT NULL,
    ""BestOf"" integer NOT NULL DEFAULT 1,
    ""MaxParticipants"" integer NOT NULL DEFAULT 16,
    ""Status"" integer NOT NULL DEFAULT 0,
    ""CreatedAtUtc"" timestamptz NOT NULL,
    ""BracketGeneratedAtUtc"" timestamptz,
    ""CompletedAtUtc"" timestamptz
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Tournaments_CreatorTelegramUserId\" ON \"Tournaments\" (\"CreatorTelegramUserId\");");

        // Колонки, добавленные после первого релиза турниров (CREATE TABLE выше не дотягивает их
        // в уже существующую таблицу) — старт турнира вручную и минимум участников для запуска.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Tournaments\" ADD COLUMN IF NOT EXISTS \"StartedAtUtc\" timestamptz;");

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Tournaments\" ADD COLUMN IF NOT EXISTS \"MinParticipants\" integer NOT NULL DEFAULT 2;");

        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""TournamentParticipants"" (
    ""Id"" serial PRIMARY KEY,
    ""TournamentId"" integer NOT NULL REFERENCES ""Tournaments"" (""Id"") ON DELETE CASCADE,
    ""TelegramUserId"" bigint NOT NULL,
    ""PlayerTag"" varchar(16) NOT NULL,
    ""PlayerName"" varchar(64) NOT NULL,
    ""Seed"" integer NOT NULL DEFAULT 0,
    ""Status"" integer NOT NULL DEFAULT 0,
    ""FinalPlacement"" integer,
    ""JoinedAtUtc"" timestamptz NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TournamentParticipants_TournamentId_PlayerTag\" ON \"TournamentParticipants\" (\"TournamentId\", \"PlayerTag\");");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TournamentParticipants_TournamentId_TelegramUserId\" ON \"TournamentParticipants\" (\"TournamentId\", \"TelegramUserId\");");

        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""TournamentMatches"" (
    ""Id"" serial PRIMARY KEY,
    ""TournamentId"" integer NOT NULL REFERENCES ""Tournaments"" (""Id"") ON DELETE CASCADE,
    ""Round"" integer NOT NULL,
    ""SlotIndex"" integer NOT NULL,
    ""ParticipantAId"" integer REFERENCES ""TournamentParticipants"" (""Id""),
    ""ParticipantBId"" integer REFERENCES ""TournamentParticipants"" (""Id""),
    ""ScoreA"" integer NOT NULL DEFAULT 0,
    ""ScoreB"" integer NOT NULL DEFAULT 0,
    ""WinnerParticipantId"" integer REFERENCES ""TournamentParticipants"" (""Id""),
    ""Status"" integer NOT NULL DEFAULT 0,
    ""NextMatchId"" integer REFERENCES ""TournamentMatches"" (""Id""),
    ""NextMatchSlot"" integer NOT NULL DEFAULT 0,
    ""UpdatedAtUtc"" timestamptz
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TournamentMatches_TournamentId_Round_SlotIndex\" ON \"TournamentMatches\" (\"TournamentId\", \"Round\", \"SlotIndex\");");

        // Отслеживаемые игровые турниры (живые данные тянутся из CR API по тегу).
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""GameTournaments"" (
    ""Id"" serial PRIMARY KEY,
    ""TournamentTag"" varchar(16) NOT NULL,
    ""Name"" varchar(120) NOT NULL,
    ""Password"" varchar(64),
    ""CreatorTelegramUserId"" bigint NOT NULL,
    ""CreatorName"" varchar(64) NOT NULL,
    ""CreatedAtUtc"" timestamptz NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameTournaments_TournamentTag\" ON \"GameTournaments\" (\"TournamentTag\");");

        // Журнал военных боёв (кто/когда отыграл КВ и исход).
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""WarBattles"" (
    ""Id"" serial PRIMARY KEY,
    ""ClanId"" integer NOT NULL,
    ""PlayerTag"" varchar(16) NOT NULL,
    ""PlayerName"" varchar(64) NOT NULL,
    ""BattleTimeUtc"" timestamptz NOT NULL,
    ""Won"" boolean NOT NULL,
    ""CrownsFor"" integer NOT NULL DEFAULT 0,
    ""CrownsAgainst"" integer NOT NULL DEFAULT 0,
    ""SeasonId"" integer NOT NULL,
    ""SectionIndex"" integer NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_WarBattles_ClanId_PlayerTag_BattleTimeUtc\" ON \"WarBattles\" (\"ClanId\", \"PlayerTag\", \"BattleTimeUtc\");");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WarBattles_ClanId_SeasonId_SectionIndex\" ON \"WarBattles\" (\"ClanId\", \"SeasonId\", \"SectionIndex\");");

        // Привязка, сделанная лидером через /bind (а не самим игроком).
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"LinkedByLeader\" boolean NOT NULL DEFAULT false;");

        // Даты подключения клана / привязки игрока — для статистики роста в панели владельца.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Clans\" ADD COLUMN IF NOT EXISTS \"CreatedAtUtc\" timestamptz;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"CreatedAtUtc\" timestamptz;");

        // Источник данных недели: "live" (текущая война) / "log" (подтверждённый финал).
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"WarSnapshots\" ADD COLUMN IF NOT EXISTS \"Source\" text NOT NULL DEFAULT 'live';");

        // «Что нового»: снимок прошлого визита игрока в Mini App.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"LastVisitAtUtc\" timestamptz;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"LastVisitFame\" integer;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Players\" ADD COLUMN IF NOT EXISTS \"LastVisitRank\" integer;");

        // Респекты 👏 — социальная награда между согильдийцами (1 в сутки).
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""Respects"" (
    ""Id"" serial PRIMARY KEY,
    ""ClanId"" integer NOT NULL,
    ""FromPlayerTag"" varchar(16) NOT NULL,
    ""FromName"" varchar(64) NOT NULL,
    ""ToPlayerTag"" varchar(16) NOT NULL,
    ""ToName"" varchar(64) NOT NULL,
    ""DayUtc"" varchar(10) NOT NULL,
    ""CreatedAtUtc"" timestamptz NOT NULL
);");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Respects_FromPlayerTag_DayUtc\" ON \"Respects\" (\"FromPlayerTag\", \"DayUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Respects_ClanId_DayUtc\" ON \"Respects\" (\"ClanId\", \"DayUtc\");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Respects_ToPlayerTag\" ON \"Respects\" (\"ToPlayerTag\");");
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
