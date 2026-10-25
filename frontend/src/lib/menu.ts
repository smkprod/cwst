import type { AppConfig, MenuTab } from '../types'
import type { MoreSection } from '../components/MoreView'

/**
 * Нижнее меню по настройкам владельца: что он спрятал, тому место в «Ещё». «Донат»
 * ставим в середину меню - так он сразу на глазах, а не в углу среди прочих.
 */

export function hiddenTabs(config: AppConfig | null | undefined): Set<MenuTab> {
  return new Set(config?.hiddenTabs ?? [])
}

/**
 * Разделы «Ещё», которые уже стоят в меню режима игрока. Вкладку спрятали - её
 * разделы снова видны в «Ещё», иначе до них было бы не дойти.
 */
export function moreInBar(hidden: Set<MenuTab>): MoreSection[] {
  return [
    ...(hidden.has('trainer') ? [] : ['trainer', 'tips'] as MoreSection[]),
    ...(hidden.has('decks') ? [] : ['builder', 'worldTop'] as MoreSection[]),
    ...(hidden.has('play') ? [] : ['challenge', 'duel', 'tournament'] as MoreSection[]),
  ]
}

/** «Донат» в середину набора вкладок режима (до «Ещё» и панели, они всегда в конце). */
export function withShop<T>(tabs: T[], shop: T | null): T[] {
  if (!shop) return tabs
  const at = Math.ceil(tabs.length / 2)
  return [...tabs.slice(0, at), shop, ...tabs.slice(at)]
}
