#!/usr/bin/env bash
# Деплой на VPS: пулл, пересборка, перезапуск + уборка за докером.
# Использование: ./deploy.sh [ветка]   (по умолчанию — текущая)
set -euo pipefail
cd "$(dirname "$0")"

BRANCH="${1:-$(git rev-parse --abbrev-ref HEAD)}"

echo "==> git pull origin $BRANCH"
git pull origin "$BRANCH"

echo "==> docker compose build + up"
docker compose -f docker-compose.prod.yml up -d --build

# Уборка: висячие образы от прошлых сборок и build cache старше 7 дней.
# Без этого каждый `--build` оставляет на диске старый образ (~300 МБ × 2 сервиса).
echo "==> docker cleanup"
docker image prune -f
docker builder prune -f --filter "until=168h"

echo "==> done"
docker compose -f docker-compose.prod.yml ps
df -h / | tail -1
