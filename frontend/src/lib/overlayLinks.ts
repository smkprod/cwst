import type { IconName } from '../components/ui/Icon'

/**
 * Виджеты для OBS и ссылки на них.
 *
 * Ссылка ведёт на отдельную страницу overlay.html того же сайта: OBS открывает её
 * как обычный браузер, без Telegram. Поэтому всё, что нужно странице, — в самой
 * ссылке: какой виджет и секретный ключ.
 */
export type OverlayWidget = 'bracket' | 'match' | 'list' | 'league' | 'challenge'

export interface WidgetSpec {
  id: OverlayWidget
  icon: IconName
  /** Чей ключ подставлять: турнира или личный ключ блогера. */
  scope: 'tournament' | 'personal'
  /** Рекомендуемый размер источника «Браузер» в OBS: под него виджет и свёрстан. */
  width: number
  height: number
}

export const WIDGETS: WidgetSpec[] = [
  { id: 'bracket', icon: 'bracket', scope: 'tournament', width: 1280, height: 720 },
  { id: 'match', icon: 'swords', scope: 'tournament', width: 900, height: 220 },
  { id: 'list', icon: 'users', scope: 'tournament', width: 520, height: 720 },
  { id: 'league', icon: 'shield', scope: 'personal', width: 520, height: 720 },
  { id: 'challenge', icon: 'ticket', scope: 'personal', width: 520, height: 720 },
]

export const TOURNAMENT_WIDGETS = WIDGETS.filter(w => w.scope === 'tournament')
export const PERSONAL_WIDGETS = WIDGETS.filter(w => w.scope === 'personal')

/**
 * Язык в ссылку кладём, только если он не русский: русский — язык виджета по
 * умолчанию, и короткую ссылку проще проверить глазами в настройках OBS.
 */
export function overlayUrl(widget: OverlayWidget, key: string, lang?: string): string {
  const q = new URLSearchParams({ w: widget, k: key })
  if (lang && lang !== 'ru') q.set('lang', lang)
  return `${window.location.origin}/overlay.html?${q}`
}
