// Минимальная типизация Telegram WebApp SDK (только то, что используем)
interface TelegramWebApp {
  initData: string
  initDataUnsafe: { user?: { id: number; first_name: string; username?: string }; start_param?: string }
  ready(): void
  expand(): void
  colorScheme: 'light' | 'dark'
  HapticFeedback?: {
    impactOccurred(style: 'light' | 'medium' | 'heavy'): void
    notificationOccurred(type: 'error' | 'success' | 'warning'): void
  }
  openTelegramLink?(url: string): void
  openLink?(url: string): void
  openInvoice?(url: string, callback?: (status: InvoiceStatus) => void): void
  requestWriteAccess?(callback?: (allowed: boolean) => void): void
  switchInlineQuery?(query: string, chooseChatTypes?: ('users' | 'bots' | 'groups' | 'channels')[]): void
  readTextFromClipboard?(callback?: (text: string | null) => void): void
}

export type InvoiceStatus = 'paid' | 'cancelled' | 'failed' | 'pending'

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
 * С чем открыли приложение: ссылка t.me/бот?startapp=review|plus|meta. Бот ставит
 * её под своими сообщениями, чтобы кнопка «Открыть разбор» вела в разбор, а не
 * на первый экран. Запасной путь — параметр адреса: так его передают старые клиенты.
 */
export const startParam: string =
  tg?.initDataUnsafe?.start_param
  ?? new URLSearchParams(window.location.search).get('tgWebAppStartParam')
  ?? ''

/** Бой из карточки трекера: startapp=m_123 открывает отчёт об этом бое. */
export const startMatchId: number | null = (() => {
  const m = /^m_(\d+)$/.exec(startParam)
  return m ? Number(m[1]) : null
})()

/** Параметр ведёт в историю боёв: «📜 Все бои», «📖 Разбор» из карточки трекера. */
export const startToMatches = startParam === 'matches' || startMatchId !== null

/**
 * Попросить разрешение писать в личку — для «Стоп-тильта». Без него бот не может
 * написать тому, кто открыл приложение, но ни разу не нажал «Старт» в чате с ним.
 * Старый Telegram метода не знает — тогда просто пробуем писать.
 */
export function requestWriteAccess(): Promise<boolean> {
  return new Promise(resolve => {
    if (!tg?.requestWriteAccess) { resolve(true); return }
    try { tg.requestWriteAccess(allowed => resolve(Boolean(allowed))) }
    catch { resolve(true) }
  })
}

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

/**
 * Открыть счёт Telegram и дождаться, чем кончилось.
 *
 * 'paid' значит только «Telegram списал звёзды». Выдачу делает бот, получив
 * подтверждение отдельным сообщением, поэтому после 'paid' спонсорство может
 * появиться не мгновенно — вызывающий обязан переспросить сервер, а не рисовать
 * звезду по одному лишь ответу этого окна.
 */
export function openInvoice(url: string): Promise<InvoiceStatus> {
  return new Promise(resolve => {
    if (!tg?.openInvoice) { resolve('failed'); return }
    tg.openInvoice(url, status => resolve(status))
  })
}

const DM_ASK_KEY = 'cwst_dm_ask'
const DM_ASK_AGAIN_MS = 7 * 24 * 3600 * 1000

/**
 * Один раз попросить разрешение писать в личку — родным окном Telegram.
 *
 * Бот может написать только тому, кто хоть раз нажал «Старт» в личке с ним. Многие
 * привязывают тег в группе клана или в приложении и в личку не заходят: из 131
 * человека рассылка дошла до 70. Разрешение из этого окна работает так же, как «Старт».
 *
 * Кто разрешил — больше не спрашиваем; кто отказал — не раньше чем через неделю.
 * @returns true — разрешил (сейчас или раньше).
 */
export async function askDmOnce(): Promise<boolean> {
  if (!tg?.requestWriteAccess || !initData) return false
  let saved: string | null = null
  try { saved = localStorage.getItem(DM_ASK_KEY) } catch { /* приватный режим — просто спросим */ }
  if (saved === 'granted') return false
  if (saved && Date.now() - Number(saved) < DM_ASK_AGAIN_MS) return false

  const allowed = await requestWriteAccess()
  try { localStorage.setItem(DM_ASK_KEY, allowed ? 'granted' : String(Date.now())) } catch { /* не страшно */ }
  return allowed
}

/** Открыть ссылку t.me внутри Telegram (профиль, канал) — без выхода в браузер. */
export function openTelegramLink(url: string) {
  if (tg?.openTelegramLink) tg.openTelegramLink(url)
  else window.open(url, '_blank')
}

/**
 * Вставить «@бот запрос» в выбранный чат - так вызов на дуэль уезжает в любой чат
 * в два тапа. Нет метода (старый клиент) или инлайн выключен - false, вызывающий
 * ведёт в личку бота.
 */
export function switchInline(query: string): boolean {
  try {
    if (!tg?.switchInlineQuery) return false
    tg.switchInlineQuery(query, ['users', 'groups'])
    return true
  } catch {
    return false
  }
}
