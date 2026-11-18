import { useEffect } from 'react'

/**
 * Временная тема «Vice City» к выходу GTA VI: закатный фон и неон на всё приложение.
 * Держится неделю после релиза и выключается сама - убирать руками не нужно.
 */
export const VICE_END_UTC = Date.UTC(2026, 10, 26, 22, 0)

export const viceActive = (now = Date.now()) => now < VICE_END_UTC

/** Включить тему, пока идёт неделя релиза. */
export function useViceTheme() {
  useEffect(() => {
    if (!viceActive()) return
    document.documentElement.classList.add('vice')
    return () => document.documentElement.classList.remove('vice')
  }, [])
}
