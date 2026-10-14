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

docker compose -f docker-compose.prod.yml ps
free -h | head -2
df -h / | tail -1

# --- Проверка после деплоя ---
# Однажды воркер после деплоя падал по памяти каждые полторы минуты сутками, а
# узнали об этом по графикам хостинга. Теперь деплой сам ждёт и смотрит: живы ли
# контейнеры, не перезапускаются ли, отвечает ли API. SKIP_CHECK=1 - пропустить.
if [ "${SKIP_CHECK:-0}" = "1" ]; then
  echo "==> проверка пропущена (SKIP_CHECK=1)"
  exit 0
fi

WAIT="${CHECK_WAIT:-120}"
restarts() { docker inspect -f '{{.RestartCount}}' "$1" 2>/dev/null || echo "?"; }
running() { docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null || echo "false"; }
API_C=$(docker compose -f docker-compose.prod.yml ps -q api)
WORKER_C=$(docker compose -f docker-compose.prod.yml ps -q worker)
API_R0=$(restarts "$API_C"); WORKER_R0=$(restarts "$WORKER_C")

echo "==> проверка: жду ${WAIT} с, смотрю на перезапуски и падения…"
sleep "$WAIT"

problems=0
for svc in api worker; do
  c=$(docker compose -f docker-compose.prod.yml ps -q "$svc")
  r0=$([ "$svc" = api ] && echo "$API_R0" || echo "$WORKER_R0")
  r1=$(restarts "$c")
  if [ "$(running "$c")" != "true" ]; then
    echo "❌ $svc не запущен"; problems=1
  elif [ "$r1" != "$r0" ]; then
    echo "❌ $svc перезапускался за время проверки ($r0 → $r1)"; problems=1
  else
    echo "✅ $svc работает, перезапусков нет"
  fi
done

if docker compose -f docker-compose.prod.yml exec -T caddy wget -qO- -T 10 http://api:10000/health >/dev/null 2>&1; then
  echo "✅ API отвечает на /health"
else
  echo "❌ API не отвечает на /health"; problems=1
fi

crashes=$(docker compose -f docker-compose.prod.yml logs --since "$((WAIT + 60))s" api worker 2>&1 \
  | grep -cE "Out of memory|OutOfMemoryException|Unhandled exception" || true)
if [ "$crashes" -gt 0 ]; then
  echo "❌ в логах падения ($crashes): docker compose -f docker-compose.prod.yml logs --tail 100 worker"
  problems=1
fi

docker stats --no-stream --format "   {{.Name}}: CPU {{.CPUPerc}}, память {{.MemUsage}}" \
  $(docker compose -f docker-compose.prod.yml ps -q)

if [ "$problems" -ne 0 ]; then
  echo "==> ❌ ДЕПЛОЙ С ПРОБЛЕМАМИ — смотри строки выше и логи:"
  echo "    docker compose -f docker-compose.prod.yml logs --tail 100 worker api"
  exit 1
fi
echo "==> ✅ done: всё работает"
