import { useEffect, useState } from 'react'
import { api } from './api'

/**
 * Арт карт по английскому имени из каталога игры - для жетонов юнитов на схемах.
 * Каталог один на приложение: грузим раз и держим, он меняется раз в сезон.
 */
let cache: Promise<Record<string, string>> | null = null

function load(): Promise<Record<string, string>> {
  cache ??= api.builderCards()
    // Герой и эволюция лежат под именами «Hero …» и «Evo …» - как карты в тренажёре
    .then(cards => Object.fromEntries(cards.flatMap(c => [
      [c.name, c.baseIconUrl || c.iconUrl] as [string, string],
      ...(c.heroIconUrl ? [[`Hero ${c.name}`, c.heroIconUrl] as [string, string]] : []),
      ...(c.evoIconUrl ? [[`Evo ${c.name}`, c.evoIconUrl] as [string, string]] : []),
    ])))
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
