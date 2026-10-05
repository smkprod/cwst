import type { CSSProperties } from 'react'
import type { DuelSheetRank } from '../../types'
import type { Translations } from '../../lib/i18n'
import { LEAGUE_COLORS, rankName } from '../../lib/duelRank'
import { RankEmblem } from './RankEmblem'
import { IconTrophy } from './DuelIcons'

/** Ранг лиги дуэлей в карточке игрока: эмблема, ранг, кубки и место. */
export function DuelSheetBadge({ rank, t }: { rank: DuelSheetRank; t: Translations }) {
  const s = t.duel
  const c = LEAGUE_COLORS[rank.league]
  const vars = { '--lg-hi': c.hi, '--lg-mid': c.mid, '--lg-lo': c.lo, '--lg-glow': c.glow } as CSSProperties
  return (
    <section className="dl-sheet" style={vars}>
      <RankEmblem league={rank.league} division={rank.division} size={54} />
      <span className="dl-sheet-info">
        <span className="dl-sheet-label">{s.sheetTitle}</span>
        <span className="dl-sheet-rank">{rankName(rank.league, rank.division, t)}</span>
        <span className="dl-sheet-sub">#{rank.place} · {rank.wins}–{rank.losses}</span>
      </span>
      <span className="dl-sheet-pts">
        <b><IconTrophy size={18} /> {rank.trophies}</b>
        <small>{s.peak} {rank.peak}</small>
      </span>
    </section>
  )
}
