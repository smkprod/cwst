#!/usr/bin/env bash
# Деплой на VPS: скачивает готовые образы с ghcr.io (собраны в GitHub Actions)
# и перезапускает контейнеры. На сервере ничего не компилируется.
#
# Использование: ./deploy.sh [ветка]   (по умолчанию — текущая)
#
# Перед первым запуском один раз залогинься в ghcr.io:
#   docker login ghcr.io -u smkprod
#   (пароль — GitHub PAT с правом read:packages)
set -euo pipefail
cd "$(dirname "$0")"

BRANCH="${1:-$(git rev-parse --abbrev-ref HEAD)}"

# --- Свап-страховка: пики памяти не должны вешать сервер ---
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

echo "==> git fetch + reset --hard origin/$BRANCH (для compose/Caddyfile)"
# reset вместо pull: на сервере правок нет, а PR-ы мержатся squash'ем,
# из-за чего история локального main расходится с GitHub и pull падает.
git fetch origin "$BRANCH"
git reset --hard "origin/$BRANCH"

echo "==> docker compose pull (скачиваем свежие образы с ghcr.io)"
docker compose -f docker-compose.prod.yml pull api worker

echo "==> docker compose up"
docker compose -f docker-compose.prod.yml up -d

# Уборка: старые версии образов копятся после каждого pull
echo "==> docker cleanup"
docker image prune -f

echo "==> done"
docker compose -f docker-compose.prod.yml ps
free -h | head -2
df -h / | tail -1
