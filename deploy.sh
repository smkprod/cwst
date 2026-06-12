#!/usr/bin/env bash
# Деплой на VPS: пулл, пересборка, перезапуск + уборка за докером.
# Использование:
#   ./deploy.sh            — pull + пересборка + перезапуск (текущая ветка)
#   ./deploy.sh --no-build — pull + перезапуск без пересборки (если код образов не менялся)
#   ./deploy.sh ветка      — то же, но с явной веткой
set -euo pipefail
cd "$(dirname "$0")"

BUILD=1
BRANCH="$(git rev-parse --abbrev-ref HEAD)"
for arg in "$@"; do
  case "$arg" in
    --no-build) BUILD=0 ;;
    *) BRANCH="$arg" ;;
  esac
done

# --- Свап: на VPS с 1 ГБ RAM сборка .NET без свапа вешает сервер намертво ---
if [ "$(swapon --show --noheadings | wc -l)" -eq 0 ]; then
  echo "==> Свапа нет — создаю 2 ГБ /swapfile (одноразово)"
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  # Переживёт перезагрузку
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  # Свап только как страховка, не вместо RAM
  sysctl -w vm.swappiness=10 >/dev/null
  grep -q '^vm.swappiness' /etc/sysctl.conf || echo 'vm.swappiness=10' >> /etc/sysctl.conf
fi

echo "==> git pull origin $BRANCH"
git pull origin "$BRANCH"

if [ "$BUILD" -eq 1 ]; then
  # Собираем ПО ОДНОМУ: параллельная сборка api+worker съедает всю память на 1 ГБ VPS.
  # Контейнеры при этом продолжают работать — даунтайм только на up.
  echo "==> build api (последовательно, чтобы не съесть всю RAM)"
  docker compose -f docker-compose.prod.yml build api
  echo "==> build worker"
  docker compose -f docker-compose.prod.yml build worker
fi

echo "==> docker compose up"
docker compose -f docker-compose.prod.yml up -d

# Уборка: висячие образы от прошлых сборок и build cache старше 7 дней.
# Без этого каждый `--build` оставляет на диске старый образ (~300 МБ × 2 сервиса).
echo "==> docker cleanup"
docker image prune -f
docker builder prune -f --filter "until=168h"

echo "==> done"
docker compose -f docker-compose.prod.yml ps
free -h | head -2
df -h / | tail -1
