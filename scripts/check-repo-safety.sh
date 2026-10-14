#!/usr/bin/env bash
# Проверка, что в репозиторий не попало то, чему там не место: базы с данными
# пользователей, .env с паролями, ключи и токены. Однажды в публичный репозиторий
# уже уехала clanwar.db - её пришлось вычищать переписыванием истории.
# Запускается в CI на каждый PR и пуш; локально: ./scripts/check-repo-safety.sh
set -euo pipefail
cd "$(dirname "$0")/.."

fail=0

# 1. Файлы, которые не должны быть в git никогда
bad_files=$(git ls-files | grep -iE '(\.db|\.db-shm|\.db-wal|\.sqlite3?|\.dump|\.bak|\.pem|\.pfx|\.p12)$|(^|/)\.env(\.[^/]*)?$' \
  | grep -vE '(^|/)\.env\.example$' || true)
if [ -n "$bad_files" ]; then
  echo "::error::В репозитории файлы с данными или секретами:"
  echo "$bad_files"
  fail=1
fi

# 2. Похожее на настоящие ключи прямо в коде
#    sk-...         - DeepSeek / OpenAI / Anthropic
#    123456789:AA.. - токен Telegram-бота
#    eyJ...eyJ...   - JWT (так выглядит ключ Clash Royale API)
#    ghp_/github_pat_ - токены GitHub
secrets=$(git grep -nIE 'sk-[A-Za-z0-9_-]{24,}|[0-9]{8,10}:AA[A-Za-z0-9_-]{30,}|eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}' \
  -- . ':!*package-lock.json' ':!scripts/check-repo-safety.sh' || true)
if [ -n "$secrets" ]; then
  echo "::error::Похоже на ключи или токены в коде (покажу только место, без значения):"
  echo "$secrets" | cut -d: -f1,2
  fail=1
fi

if [ "$fail" -ne 0 ]; then
  echo "Уберите эти файлы/строки из коммита. Секреты живут только в .env на сервере."
  exit 1
fi
echo "OK: баз, .env и ключей в репозитории нет"
