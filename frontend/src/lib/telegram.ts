// Минимальная типизация Telegram WebApp SDK (только то, что используем)
interface TelegramWebApp {
  initData: string
  initDataUnsafe: { user?: { id: number; first_name: string; username?: string } }
  ready(): void
  expand(): void
  colorScheme: 'light' | 'dark'
  HapticFeedback?: {
    impactOccurred(style: 'light' | 'medium' | 'heavy'): void
    notificationOccurred(type: 'error' | 'success' | 'warning'): void
  }
  openTelegramLink?(url: string): void
  openLink?(url: string): void
}

declare global {
  interface Window { Telegram?: { WebApp: TelegramWebApp } }
}

export const tg = window.Telegram?.WebApp

export function initTelegram() {
  tg?.ready()
  tg?.expand()
}

/** initData для заголовка авторизации. Пустая строка вне Telegram (dev-режим). */
export const initData = tg?.initData ?? ''
export const tgUser = tg?.initDataUnsafe?.user

/**
 * Username бота (без @). Значение из сборки — лишь стартовое: если переменную
 * VITE_BOT_USERNAME забыли задать в CI, юзернейм подтягивается с сервера
 * (см. lib/botUsername.ts). Поэтому не const — оно может уточниться позже.
 */
let botUsername = (import.meta.env.VITE_BOT_USERNAME ?? '').trim()

export function getBotUsername(): string { return botUsername }
export function setBotUsername(value: string) { botUsername = value.trim() }

/** Глубокая ссылка в чат с ботом с payload для /start (например, реферал ref_123). */
export function botStartLink(payload?: string): string {
  if (!botUsername) return 'https://t.me'
  return payload
    ? `https://t.me/${botUsername}?start=${encodeURIComponent(payload)}`
    : `https://t.me/${botUsername}`
}

/** Лёгкая вибрация при тапах (no-op вне Telegram). */
export function haptic(style: 'light' | 'medium' | 'heavy' = 'light') {
  tg?.HapticFeedback?.impactOccurred(style)
}

export function hapticNotify(type: 'error' | 'success' | 'warning') {
  tg?.HapticFeedback?.notificationOccurred(type)
}

/** Открыть внешнюю ссылку (браузер поверх Mini App). */
export function openExternalLink(url: string) {
  if (tg?.openLink) tg.openLink(url)
  else window.open(url, '_blank')
}

/**
 * Скопировать текст в буфер обмена. true — получилось.
 *
 * У Telegram нет своего метода записи в буфер (WebApp умеет только читать), поэтому
 * идём через обычный веб-API. Он есть не везде: в старых вебвью и без https объект
 * clipboard просто отсутствует — там остаётся приём со скрытым textarea, который
 * умеет ровно то же самое, только через устаревшую команду. Ошибку не глотаем молча:
 * вызывающий должен уметь показать, что не вышло.
 */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // Разрешение не дали или вебвью соврало о поддержке — пробуем запасной путь
  }

  try {
    const area = document.createElement('textarea')
    area.value = text
    // Вне экрана, но в документе: невидимый или display:none элемент не выделяется
    area.style.position = 'fixed'
    area.style.opacity = '0'
    area.style.pointerEvents = 'none'
    document.body.appendChild(area)
    area.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(area)
    return ok
  } catch {
    return false
  }
}

/** Поделиться текстом через нативный share-диалог Telegram.
 *  linkUrl — что прикладывается ссылкой (по умолчанию глубокая ссылка в бота, если известен username). */
export function shareToTelegram(text: string, linkUrl: string = botStartLink()) {
  // Юзернейм бота ещё не доехал — botStartLink отдал заглушку «https://t.me».
  // Отправлять её нельзя: в чате появится ссылка в никуда, и это хуже, чем её
  // отсутствие. Делимся одним текстом.
  const share = linkUrl === 'https://t.me' ? '' : linkUrl
  const url = `https://t.me/share/url?url=${encodeURIComponent(share)}&text=${encodeURIComponent(text)}`
  if (tg?.openTelegramLink) tg.openTelegramLink(url)
  else window.open(url, '_blank')
}
