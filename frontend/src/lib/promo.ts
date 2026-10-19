/**
 * Связь окна-анонса челленджа с остальным приложением без общего состояния:
 * окно живёт рядом с App, а переключает вкладку и сдерживает окно «разрешить
 * писать в личку» через события.
 */
export const OPEN_CHALLENGE = 'cwst:open-challenge'
const PROMO_DONE = 'cwst:promo-done'
const CHALLENGE_DONE = 'cwst:challenge-promo-done'

let pending = true

/** null - окно челленджа ещё решает; true - показалось; false - нет. */
let challengeShown: boolean | null = null

/**
 * Окно челленджа решило. Следом может идти заставка ивента: если челлендж уже
 * показался, она ждёт следующего захода - два окна подряд выглядят как спам.
 */
export function challengePromoDone(shown: boolean) {
  challengeShown = shown
  window.dispatchEvent(new Event(CHALLENGE_DONE))
}

/** Показалось ли окно челленджа. Сеть зависла - считаем, что показалось, и не лезем вторым. */
export function afterChallengePromo(): Promise<boolean> {
  if (challengeShown !== null) return Promise.resolve(challengeShown)
  return new Promise(resolve => {
    const done = () => { window.removeEventListener(CHALLENGE_DONE, done); window.clearTimeout(timer); resolve(challengeShown ?? true) }
    window.addEventListener(CHALLENGE_DONE, done)
    const timer = window.setTimeout(done, 12_000)
  })
}

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
