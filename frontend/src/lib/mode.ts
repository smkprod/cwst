/**
 * Два режима приложения: «Клан» - война, штаб, состав, рейтинг; «Игрок» - свой
 * профиль, колоды, тренажёр, турниры. Переключатель - первым пунктом нижнего меню.
 *
 * Последний режим запоминаем: кто живёт в колодах, должен сразу попадать туда, а не
 * каждый раз проходить через экран войны.
 */
export type AppMode = 'clan' | 'player'

const MODE_KEY = 'cwst_mode'
const TRAINER_DAY_KEY = 'cwst_trainer_seen_day'

export function loadMode(): AppMode | null {
  try {
    const v = localStorage.getItem(MODE_KEY)
    return v === 'clan' || v === 'player' ? v : null
  } catch { return null }
}

export function saveMode(mode: AppMode) {
  try { localStorage.setItem(MODE_KEY, mode) } catch { /* без памяти - откроется режим по умолчанию */ }
}

const today = () => new Date().toISOString().slice(0, 10)

/** Тренажёр открывали сегодня - точка «загляни в режим игрока» больше не нужна. */
export function markTrainerSeen() {
  try { localStorage.setItem(TRAINER_DAY_KEY, today()) } catch { /* точка просто останется */ }
}

export function trainerSeenToday(): boolean {
  try { return localStorage.getItem(TRAINER_DAY_KEY) === today() } catch { return true }
}
