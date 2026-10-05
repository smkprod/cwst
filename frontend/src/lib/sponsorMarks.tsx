import type { ReactNode } from 'react'
import type { BackgroundKey } from '../types'
import { Icon, type IconName } from '../components/ui/Icon'

/**
 * Ярлыки спонсора и значок витрины — одни и те же во всех списках.
 *
 * Вынесено из Аллеи в общий модуль ровно потому, что смысл у них появляется
 * только от повторения: значок, который видно на одном экране раз в неделю,
 * статусом не работает. В рейтинге и составе на него смотрят каждый день.
 */
export const BADGE_ICON_NAMES: Record<string, IconName> = {
  streak: 'flame',
  dailyStreak: 'calendar',
  perfectDays: 'checkCircle',
  mvpWeeks: 'crown',
  totalFame: 'medal',
  warsPlayed: 'swords',
  perfectWeeks: 'gem',
  perfectSeasons: 'trophy',
  boatAttacks: 'anchor',
}

/** Иконка значка по ключу; неизвестный ключ — медаль. */
export function BadgeIcon({ badge, size = 16 }: { badge: string | null | undefined; size?: number }) {
  return <Icon name={(badge && BADGE_ICON_NAMES[badge]) || 'medal'} size={size} />
}

/** Готовые иконки значков (раньше здесь были эмодзи-строки). */
export const BADGE_ICONS: Record<string, ReactNode> = Object.fromEntries(
  Object.keys(BADGE_ICON_NAMES).map(k => [k, <BadgeIcon key={k} badge={k} />]),
)

/** Место на пьедестале: золотой/серебряный/бронзовый кружок с цифрой вместо медалей-эмодзи. */
export function PlaceBadge({ place, size = 26 }: { place: number; size?: number }) {
  return (
    <span className={`pl-place pl-place-${place <= 3 ? place : 'n'}`} style={{ width: size, height: size, fontSize: Math.round(size * 0.48) }}>
      {place}
    </span>
  )
}

const LEVEL_CLS = ['', 'badge-bronze', 'badge-silver', 'badge-gold']

export interface Marked {
  isSponsor?: boolean
  badgeKey?: string | null
  badgeLevel?: number
  backgroundKey?: BackgroundKey | null
}

/** Значок витрины и звезда спонсора рядом с именем. */
export function SponsorMarks({ of }: { of: Marked }) {
  const level = of.badgeLevel ?? 0
  return (
    <>
      {of.badgeKey && level > 0 && (
        <span className={`hall-badge pl-badge-icon ${LEVEL_CLS[level] ?? ''}`}>
          <BadgeIcon badge={of.badgeKey} size={13} />
        </span>
      )}
      {of.isSponsor && <span className="hall-sponsor-tag pl-sponsor-star"><Icon name="star" size={12} fill="currentColor" /></span>}
    </>
  )
}

/**
 * Стиль строки со своим фоном.
 *
 * Возвращает и класс, и стиль: без класса вуаль не включится, и белое имя ляжет
 * прямо на светлые облака.
 */
export function rowBackground(of: Marked): { className: string; style?: React.CSSProperties } {
  return of.backgroundKey
    ? { className: 'sponsor-bg', style: { backgroundImage: `url(/bg/${of.backgroundKey}.webp)` } }
    : { className: '' }
}
