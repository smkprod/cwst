import { useRef } from 'react'
import type { Translations } from '../lib/i18n'
import type { OverlayLeague } from '../types'
import { Icon } from '../components/ui/Icon'
import { RankEmblem } from '../components/duel/RankEmblem'
import { rankName } from '../lib/duelRank'
import { useFlip } from './data'

/** Топ-10 лиги дуэлей, 520×720: эмблема ранга, кубки, победы–поражения. */
export function LeagueWidget({ data, t }: { data: OverlayLeague; t: Translations }) {
  const rows = data.top.slice(0, 10)
  const ref = useRef<HTMLDivElement>(null)
  useFlip(ref, rows.map(r => `${r.tag}:${r.rank}`).join(','))

  return (
    <div className="ov-panel ov-board">
      <div className="ov-board-head">
        <span className="ov-head-tile ov-tile-gold"><Icon name="swords" size={26} /></span>
        <div className="ov-head-text">
          <div className="ov-head-title">{t.duel.title}</div>
          <div className="ov-board-meta">{t.duel.players.replace('{n}', String(data.players))}</div>
        </div>
      </div>

      {rows.length === 0
        ? <div className="ov-list-empty"><Icon name="swords" size={26} /> {t.duel.emptyTop}</div>
        : (
          <div className="ov-board-rows" ref={ref}>
            {rows.map(r => (
              <div key={r.tag} data-flip={r.tag} className={`ov-brow ov-brow-${r.rank <= 3 ? r.rank : 'n'}`}>
                <span className="ov-brow-rank">{r.rank}</span>
                {/* Анимация эмблемы только у тройки: десять крутящихся колец рябят в глазах */}
                <RankEmblem league={r.league} division={r.division} size={44} animated={r.rank <= 3} />
                <span className="ov-brow-text">
                  <span className="ov-brow-name">{r.name}</span>
                  <span className="ov-brow-sub">{rankName(r.league, r.division, t)}</span>
                </span>
                <span className="ov-brow-stat">
                  <span key={r.rating} className="ov-brow-big ov-pop"><Icon name="trophy" size={17} /> {r.rating}</span>
                  <span className="ov-brow-wl">{r.wins}–{r.losses}</span>
                </span>
              </div>
            ))}
          </div>
        )}
    </div>
  )
}
