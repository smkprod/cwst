import { useId, type ReactElement } from 'react'
import type { DuelLeagueKey } from '../../types'
import { LEAGUE_COLORS } from '../../lib/duelRank'

/**
 * Эмблема ранга: граненый щит в металле лиги, знак лиги в центре и дивизион
 * ромбами снизу. Чем выше лига, тем богаче: с Алмаза — вращающееся кольцо,
 * с Мастера — крылья, у Легенды — пламя по краю.
 *
 * Всё рисуется SVG: эмодзи на разных телефонах выглядят по-разному, а ранг —
 * это то, чем хвастаются, и он должен выглядеть одинаково дорого везде.
 */
export function RankEmblem({ league, division, size = 64, animated = true, dim = false }: {
  league: DuelLeagueKey
  division: number
  size?: number
  animated?: boolean
  /** Ещё не достигнутый ранг на лестнице — приглушённый. */
  dim?: boolean
}) {
  const raw = useId().replace(/:/g, '')
  const id = (k: string) => `${raw}-${k}`
  const c = LEAGUE_COLORS[league]
  const tier = ['bronze', 'silver', 'gold', 'diamond', 'master', 'legend'].indexOf(league)
  const pips = division > 0 ? 4 - division : 0

  return (
    <svg
      className={`rk ${animated ? 'rk-anim' : ''} ${dim ? 'rk-dim' : ''} rk-${league}`}
      width={size} height={size} viewBox="0 0 120 120" aria-hidden="true"
    >
      <defs>
        <linearGradient id={id('metal')} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={c.hi} />
          <stop offset="0.45" stopColor={c.mid} />
          <stop offset="1" stopColor={c.lo} />
        </linearGradient>
        <linearGradient id={id('core')} x1="0" y1="1" x2="1" y2="0">
          <stop offset="0" stopColor={c.lo} />
          <stop offset="1" stopColor={c.mid} stopOpacity="0.85" />
        </linearGradient>
        <linearGradient id={id('sym')} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="1" stopColor={c.hi} />
        </linearGradient>
        <radialGradient id={id('glow')}>
          <stop offset="0" stopColor={c.glow} />
          <stop offset="1" stopColor={c.glow} stopOpacity="0" />
        </radialGradient>
        <linearGradient id={id('shine')} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#fff" stopOpacity="0" />
          <stop offset="0.5" stopColor="#fff" stopOpacity="0.75" />
          <stop offset="1" stopColor="#fff" stopOpacity="0" />
        </linearGradient>
        <clipPath id={id('clip')}>
          <path d={SHIELD} />
        </clipPath>
      </defs>

      <circle cx="60" cy="58" r="56" fill={`url(#${id('glow')})`} className="rk-glow" />

      {tier >= 3 && (
        <circle cx="60" cy="58" r="52" fill="none" stroke={c.hi} strokeOpacity="0.55" strokeWidth="1.5"
          strokeDasharray="3 7" className="rk-ring" />
      )}

      {tier >= 4 && (
        <g fill={`url(#${id('metal')})`} stroke={c.hi} strokeOpacity="0.6" strokeWidth="1">
          <path d="M18 40 L2 30 L8 48 L0 52 L10 62 L4 68 L18 70 Z" />
          <path d="M102 40 L118 30 L112 48 L120 52 L110 62 L116 68 L102 70 Z" />
        </g>
      )}

      {tier === 5 && (
        <g className="rk-flame">
          <path d="M60 0 C66 8 72 10 70 18 C76 14 78 8 80 4 C84 14 82 22 74 26 L46 26 C38 22 36 14 40 4 C42 8 44 14 50 18 C48 10 54 8 60 0 Z"
            fill={`url(#${id('metal')})`} />
        </g>
      )}

      <path d={SHIELD} fill={`url(#${id('metal')})`} stroke={c.hi} strokeWidth="2.5" strokeLinejoin="round" />
      <path d={INNER} fill={`url(#${id('core')})`} stroke="rgba(0,0,0,0.35)" strokeWidth="1.5" strokeLinejoin="round" />
      <path d={INNER} fill="none" stroke={c.hi} strokeOpacity="0.35" strokeWidth="1" transform="translate(0 -1)" />

      <g fill={`url(#${id('sym')})`} stroke="rgba(0,0,0,0.25)" strokeWidth="1">
        {SYMBOLS[league]}
      </g>

      <g clipPath={`url(#${id('clip')})`}>
        <rect x="-60" y="0" width="40" height="120" fill={`url(#${id('shine')})`} transform="skewX(-20)" className="rk-shine" />
      </g>

      {pips > 0 && (
        <g>
          {[0, 1, 2].map(i => (
            <path key={i} d={`M${46 + i * 14} 106 l5 -5 l5 5 l-5 5 z`}
              fill={i < pips ? c.hi : 'rgba(0,0,0,0.45)'} stroke={c.lo} strokeWidth="1" />
          ))}
        </g>
      )}
    </svg>
  )
}

const SHIELD = 'M60 12 L98 30 L98 70 Q98 86 60 104 Q22 86 22 70 L22 30 Z'
const INNER = 'M60 22 L89 36 L89 68 Q89 80 60 94 Q31 80 31 68 L31 36 Z'

const SYMBOLS: Record<DuelLeagueKey, ReactElement> = {
  bronze: <path d="M44 58 L60 46 L76 58 L76 66 L60 54 L44 66 Z" />,
  silver: (
    <>
      <path d="M44 52 L60 40 L76 52 L76 59 L60 47 L44 59 Z" />
      <path d="M44 66 L60 54 L76 66 L76 73 L60 61 L44 73 Z" />
    </>
  ),
  gold: <path d="M60 38 L65.5 51.5 L80 52.5 L69 62 L72.5 76 L60 68.5 L47.5 76 L51 62 L40 52.5 L54.5 51.5 Z" />,
  diamond: (
    <>
      <path d="M46 50 L52 42 L68 42 L74 50 L60 76 Z" />
      <path d="M46 50 L74 50 M52 42 L57 50 L60 76 M68 42 L63 50 L60 76" fill="none" stroke="rgba(0,0,0,0.3)" />
    </>
  ),
  master: <path d="M42 72 L40 46 L51 56 L60 40 L69 56 L80 46 L78 72 Z M42 76 L78 76 L78 80 L42 80 Z" />,
  legend: (
    <path d="M60 36 C64 46 74 50 72 62 C71 70 66 76 60 78 C54 76 49 70 48 62 C47 55 51 51 54 47 C54 53 56 56 59 57 C57 50 58 42 60 36 Z" />
  ),
}
