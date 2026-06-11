# ⚔️ Clan War Tracker — Telegram Mini App для Clash Royale

MVP-приложение: показывает, кто из клана сыграл Clan War (River Race), и автоматически напоминает тем, кто не сыграл.

## Стек

| Слой | Технология |
|---|---|
| Backend | ASP.NET Core 8 Web API, Clean Architecture |
| Worker | .NET BackgroundService (напоминания + бот long polling) |
| БД | PostgreSQL (EF Core) |
| Bot | Telegram.Bot 22 |
| Frontend | React + TypeScript + Vite, Telegram WebApp SDK |

## Структура проекта

```
le-Project/
├── backend/
│   ├── ClanWarTracker.sln
│   └── src/
│       ├── ClanWarTracker.Domain/          # сущности, enum'ы, интерфейсы — ноль зависимостей
│       │   ├── Entities/   Player, Clan, WarStatus
│       │   ├── Enums/      WarPlayStatus
│       │   └── Interfaces/ IClashRoyaleApi, INotificationSender, репозитории
│       ├── ClanWarTracker.Application/     # use cases — чистая бизнес-логика
│       │   ├── UseCases/   GetClanStatus, SendReminders, LinkPlayer, SetupClan
│       │   └── DTOs/
│       ├── ClanWarTracker.Infrastructure/  # внешний мир
│       │   ├── ClashRoyale/  HTTP-клиент официального CR API (+ кэш 2 мин)
│       │   ├── Persistence/  EF Core DbContext + репозитории
│       │   └── Telegram/     отправка уведомлений
│       ├── ClanWarTracker.Api/             # Web API для Mini App
│       │   ├── Controllers/  ClanController, PlayerController
│       │   └── Auth/         валидация Telegram initData (HMAC-SHA256)
│       └── ClanWarTracker.Worker/          # фон: проверка войн каждые 30 мин + команды бота
├── frontend/                               # Mini App (React + TS)
│   └── src/
│       ├── App.tsx, types.ts
│       ├── components/  WarHeader, PlayerList, LinkPrompt
│       └── lib/         telegram.ts (SDK), api.ts (fetch + initData header)
└── docker-compose.yml                      # PostgreSQL для локальной разработки
```

## API endpoints

| Метод | Путь | Описание |
|---|---|---|
| GET | `/api/clans/my/status` | Статус войны + статистика + прогноз (по Telegram initData) |
| GET | `/api/clans/{tag}/status` | Статус по тегу клана (без `#`) |
| GET | `/api/clans/my/history?weeks=8` | История войн по неделям из снапшотов (**Pro**) |
| POST | `/api/clans/my/nudge` | «Пнуть» не сыгравших — только админ группы (**Pro**) |
| GET | `/api/players/me` | Привязанный игрок текущего пользователя |
| GET | `/api/players/me/stats` | Личная статистика: ранг, вклад, прогнозы |
| GET | `/api/owner/clans` | Панель владельца: все кланы и тарифы (только `Owner:TelegramUserId`) |
| POST | `/api/owner/clans/{id}/plan` | Выдать тариф: `{ "tier": "pro"\|"free", "days": 30 }` |
| GET | `/health` | Healthcheck (для Render) |

## Тарифы (SaaS)

- **Free**: статус войны, список игроков, напоминания, рейтинг.
- **Pro**: прогноз славы (день/неделя), история войн, кнопка «Пнуть лентяев».
- Новый клан при `/setup` получает **триал Pro на 14 дней**.
- Выдача тарифов — вкладка «⚙️ Панель» в Mini App. Видна только аккаунту из `Owner:TelegramUserId` (свой ID узнай у @userinfobot и впиши в `appsettings.json` API).
- Снапшоты войны собираются воркером каждые 30 минут для **всех** кланов (и Free тоже) — когда клан купит Pro, история уже будет на месте.

Все `/api/*` запросы требуют заголовок `X-Telegram-Init-Data` — Mini App шлёт его автоматически. Подпись проверяется на сервере (HMAC-SHA256 с bot token), подделать нельзя.

Логика статусов (`GetClanStatusUseCase.Classify`):
- ✅ `played` — 4/4 колоды сыграны
- ⏳ `timeLeft` — < 4 колод, но до конца дня > 4 часов (или тренировочные дни)
- ❌ `notPlayed` — < 4 колод и осталось ≤ 4 часов

## Команды бота

- `/setup #CLANTAG` — привязать клан к группе (только админ группы)
- `/link #PLAYERTAG` — привязать свой аккаунт CR к Telegram
- `/status` — текстовая сводка войны прямо в чат

## Запуск локально

### 1. Получи токены
- **Telegram Bot**: @BotFather → `/newbot` → токен. Там же: Bot Settings → Menu Button → задай URL Mini App.
- **Clash Royale API**: https://developer.clashroyale.com → создай ключ. ⚠️ Ключ привязан к IP! Для разработки добавь свой IP, для Render — статический IP через прокси (см. «Подводные камни»).

### 2. База + бэкенд
```bash
docker compose up -d                          # PostgreSQL

cd backend
# вставь токены в src/ClanWarTracker.Api/appsettings.json и src/ClanWarTracker.Worker/appsettings.json

dotnet tool install --global dotnet-ef        # если ещё нет
dotnet ef migrations add Init -p src/ClanWarTracker.Infrastructure -s src/ClanWarTracker.Api
dotnet run --project src/ClanWarTracker.Api   # API на http://localhost:5000
dotnet run --project src/ClanWarTracker.Worker # отдельный терминал: бот + напоминания
```

### 3. Фронтенд
```bash
cd frontend
npm install
npm run dev    # http://localhost:5173
```
Для теста внутри Telegram нужен HTTPS — прокинь локалку через `ngrok http 5173` и укажи URL в BotFather.

## Деплой (Vercel + Render)

### Frontend → Vercel
1. Запушь репозиторий на GitHub.
2. Vercel → New Project → выбери repo, **Root Directory: `frontend`**.
3. Env-переменная: `VITE_API_URL=https://<твой-api>.onrender.com`.
4. Deploy → получаешь `https://xxx.vercel.app` → вставь этот URL в BotFather (Menu Button / Mini App URL).

### Backend → Render
1. Render → New → **Web Service** → repo, Root Directory `backend`.
   - Build: `dotnet publish src/ClanWarTracker.Api -c Release -o out`
   - Start: `dotnet out/ClanWarTracker.Api.dll`
   - Env: `ConnectionStrings__Default`, `ClashRoyale__ApiToken`, `Telegram__BotToken`, `Frontend__Origin=https://xxx.vercel.app`, `ASPNETCORE_URLS=http://0.0.0.0:10000`
2. Render → New → **Background Worker** → то же repo.
   - Build: `dotnet publish src/ClanWarTracker.Worker -c Release -o out`
   - Start: `dotnet out/ClanWarTracker.Worker.dll`
3. Render → New → **PostgreSQL** → скопируй Internal Connection String в env обоих сервисов.

### ⚠️ Подводные камни деплоя
- **CR API и IP**: ключ Clash Royale API работает только с whitelisted IP. У Render бесплатного статического исходящего IP нет → варианты: (a) Render Static Outbound IP (платный план), (b) прокси RoyaleAPI `proxy.royaleapi.dev` — официально рекомендован Supercell-сообществом, у него фиксированные IP, (c) свой дешёвый VPS как прокси.
- **Free tier Render** усыпляет Web Service после 15 минут простоя — для API это терпимо (Mini App разбудит), но **Worker должен быть Background Worker** (не засыпает на платном тарифе) — иначе напоминания не отправятся.
- Бот шлёт ЛС только тем, кто хоть раз написал ему `/start` в личке — предупреждай игроков при `/link`.

## Масштабирование (100+ кланов)

Что уже заложено в MVP:
- **Кэш CR API на 2 минуты** (`IMemoryCache`) — N открытых Mini App одного клана = 1 запрос к Supercell.
- **Анти-спам напоминаний** (`LastReminderSentAt`, cooldown 6 ч).
- Worker отделён от API — их можно масштабировать независимо.

Что менять при росте:
1. **Rate limit CR API** (~есть лимиты на ключ): при 100+ кланах проверять кланы не все сразу, а очередью — замени цикл в `SendRemindersUseCase` на выборку батчами + `Channel<T>`/Hangfire. Хранить снимки войны в БД (таблица `WarSnapshots`) и отдавать Mini App из БД, а не из CR API напрямую.
2. **Кэш → Redis**, когда появится больше одного инстанса API.
3. **Webhook вместо long polling** для бота (меньше задержка, дешевле при нагрузке): endpoint `/api/bot/webhook` в API-проекте, `bot.SetWebhook(...)`.
4. **Шардирование проверок**: 100 кланов × проверка раз в 30 мин = ~3,3 запроса/мин — CR API выдержит легко; узкое место наступит ближе к 1000+ кланов, тогда — несколько API-ключей и распределение кланов по ним.
5. Метрики/логирование: Serilog + Seq или Grafana Cloud free tier.

## Roadmap после MVP
- [x] Кнопка «Пнуть всех» в Mini App для админа (Pro)
- [x] История участия по неделям (таблицы WarSnapshots + PlayerWarSnapshots)
- [x] Статистика игрока + прогноз славы клана/игроков
- [x] Тарифы Free/Pro + панель владельца
- [ ] Оплата через Telegram Stars (сейчас тарифы выдаются вручную через панель)
- [ ] Авто-определение клана игрока по player tag (CR API отдаёт клан игрока)
- [ ] Настройка `ReminderHoursBeforeEnd` командой `/remind 5`
