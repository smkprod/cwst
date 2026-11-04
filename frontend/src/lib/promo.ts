/**
 * Связь окна-анонса челленджа с остальным приложением без общего состояния:
 * окно живёт рядом с App, а переключает вкладку и сдерживает окно «разрешить
 * писать в личку» через события.
 */
export const OPEN_CHALLENGE = 'cwst:open-challenge'
const PROMO_DONE = 'cwst:promo-done'

let pending = true

/** Окно-анонс решило, показываться ли, и (если показалось) закрыто. */
export function promoDone() {
  pending = false
  window.dispatchEvent(new Event(PROMO_DONE))
}

/** Ждём анонс: два окна подряд поверх друг друга выглядят как спам. */
export function afterPromo(): Promise<void> {
  if (!pending) return Promise.resolve()
  return new Promise(resolve => {
    const done = () => { window.removeEventListener(PROMO_DONE, done); resolve() }
    window.addEventListener(PROMO_DONE, done)
    // Страховка: сеть зависла — не держим второе окно вечно
    window.setTimeout(done, 15_000)
  })
}

export function openChallenge() {
  window.dispatchEvent(new Event(OPEN_CHALLENGE))
}
