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

/** Username бота (без @) — задаётся при сборке через VITE_BOT_USERNAME. Пусто — реф-ссылки скрыты. */
export const BOT_USERNAME = (import.meta.env.VITE_BOT_USERNAME ?? '').trim()

/** Глубокая ссылка в чат с ботом с payload для /start (например, реферал ref_123). */
export function botStartLink(payload?: string): string {
  if (!BOT_USERNAME) return 'https://t.me'
  return payload
    ? `https://t.me/${BOT_USERNAME}?start=${encodeURIComponent(payload)}`
    : `https://t.me/${BOT_USERNAME}`
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

/** Поделиться текстом через нативный share-диалог Telegram.
 *  linkUrl — что прикладывается ссылкой (по умолчанию глубокая ссылка в бота, если известен username). */
export function shareToTelegram(text: string, linkUrl: string = botStartLink()) {
  const url = `https://t.me/share/url?url=${encodeURIComponent(linkUrl)}&text=${encodeURIComponent(text)}`
  if (tg?.openTelegramLink) tg.openTelegramLink(url)
  else window.open(url, '_blank')
}
