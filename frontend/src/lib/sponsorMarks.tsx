import type { BackgroundKey } from '../types'

/**
 * Ярлыки спонсора и значок витрины — одни и те же во всех списках.
 *
 * Вынесено из Аллеи в общий модуль ровно потому, что смысл у них появляется
 * только от повторения: значок, который видно на одном экране раз в неделю,
 * статусом не работает. В рейтинге и составе на него смотрят каждый день.
 */
export const BADGE_ICONS: Record<string, string> = {
  streak: '🔥',
  dailyStreak: '📆',
  perfectDays: '💯',
  mvpWeeks: '👑',
  totalFame: '🏅',
  warsPlayed: '⚔️',
  perfectWeeks: '💎',
  perfectSeasons: '🏆',
  boatAttacks: '🚤',
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
        <span className={`hall-badge ${LEVEL_CLS[level] ?? ''}`}>
          {BADGE_ICONS[of.badgeKey] ?? '🏅'}
        </span>
      )}
      {of.isSponsor && <span className="hall-sponsor-tag">★</span>}
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
