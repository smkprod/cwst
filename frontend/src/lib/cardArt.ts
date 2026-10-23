import { useEffect, useState } from 'react'
import { api } from './api'

/**
 * Арт карт по английскому имени из каталога игры - для жетонов юнитов на схемах.
 * Каталог один на приложение: грузим раз и держим, он меняется раз в сезон.
 */
let cache: Promise<Record<string, string>> | null = null

function load(): Promise<Record<string, string>> {
  cache ??= api.builderCards()
    .then(cards => Object.fromEntries(cards.map(c => [c.name, c.baseIconUrl || c.iconUrl])))
    .catch(() => { cache = null; return {} })
  return cache
}

export function useCardArt(): Record<string, string> {
  const [art, setArt] = useState<Record<string, string>>({})
  useEffect(() => {
    let alive = true
    load().then(a => { if (alive) setArt(a) })
    return () => { alive = false }
  }, [])
  return art
}
