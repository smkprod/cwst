# Развёртывание на VPS (DigitalOcean / любой Ubuntu 24.04)

Стек: PostgreSQL + API + воркер (Telegram-бот) + Caddy (HTTPS).
Mini App доступен по адресу `https://165-245-222-154.sslip.io` — sslip.io
бесплатно резолвит этот домен в IP сервера, Caddy сам получает сертификат.

## 1. Подключение и подготовка сервера

```bash
ssh root@165.245.222.154

# Docker
curl -fsSL https://get.docker.com | sh

# Swap 2GB — обязательно при 1GB RAM
fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab

# Firewall: только SSH и веб
ufw allow 22 && ufw allow 80 && ufw allow 443 && ufw --force enable
```

## 2. Код и настройки

```bash
git clone https://github.com/smkprod/cwst.git
cd cwst
cp .env.example .env
nano .env   # заполни три значения, сохрани: Ctrl+O, Enter, Ctrl+X
```

Токен Clash Royale: создай на developer.clashroyale.com новый ключ
с IP `165.245.222.154` (прокси больше не нужен).

## 3. Перенос данных с Render (по желанию)

Если хочешь сохранить историю войн и привязки игроков — ДО первого запуска
приложений. Возьми External Database URL из Render (страница базы → Info).

```bash
docker compose -f docker-compose.prod.yml up -d db
docker run --rm postgres:18-alpine pg_dump "ВНЕШНИЙ_URL_БАЗЫ_RENDER" \
  --no-owner --no-privileges > dump.sql
docker compose -f docker-compose.prod.yml exec -T db psql -U clanwar -d clanwar < dump.sql
```

Если история не нужна — пропусти, схема создастся сама, клан привяжешь
заново через /setup.

## 4. Запуск

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

Первая сборка — 5–10 минут. Проверка:

```bash
docker compose -f docker-compose.prod.yml ps          # все сервисы Up
docker compose -f docker-compose.prod.yml logs worker # ищем "Bot polling started"
curl -s https://165-245-222-154.sslip.io/health       # {"status":"ok"}
```

## 5. Telegram

В @BotFather → /mybots → твой бот → Bot Settings → Menu Button
(и Configure Mini App, если настраивал) → поменяй URL на:

```
https://165-245-222-154.sslip.io
```

## Обновление после изменений кода

```bash
cd ~/cwst && git pull && docker compose -f docker-compose.prod.yml up -d --build
```

## Если сменится IP сервера

Поменяй домен в Caddyfile (дефисы вместо точек + .sslip.io), обнови IP
в ключе Clash Royale и URL в BotFather.
