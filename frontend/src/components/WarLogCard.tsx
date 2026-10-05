import { useState } from 'react'
import type { WarLogWeek } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, formatPlace } from '../lib/i18n'
import { useOpenClan } from '../lib/clanModal'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

/** Номер места в кружке: 1–3 — золото, серебро, бронза (вместо медалей-эмодзи). */
export function PlaceBadge({ rank, large = false }: { rank: number; large?: boolean }) {
  return (
    <span className={`cl-place ${rank >= 1 && rank <= 3 ? `cl-place-${rank}` : ''} ${large ? 'cl-place-lg' : ''}`}>
      {rank}
    </span>
  )
}

interface WeeksProps {
  weeks: WarLogWeek[]
  meLabel?: string
}

export function WarLogWeeks({ weeks, meLabel }: WeeksProps) {
  const [openKey, setOpenKey] = useState<string | null>(null)
  const [openClan, setOpenClan] = useState<string | null>(null)
  const showClanPage = useOpenClan()
  const { t } = useT()

  const toggleWeek = (key: string) => {
    haptic('light')
    setOpenKey(k => (k === key ? null : key))
    setOpenClan(null)
  }

  const toggleClan = (clanKey: string) => {
    haptic('light')
    setOpenClan(k => (k === clanKey ? null : clanKey))
  }

  return (
    <ul className="warlog-list">
      {weeks.map(w => {
        const key = `${w.seasonId}-${w.sectionIndex}`
        const ours = w.standings.find(s => s.isOurClan)
        const isOpen = openKey === key
        const placeText = !ours ? '—'
          : ours.rank === 1 ? <span className="cl-ic"><Icon name="trophy" size={14} className="cl-fame" />{t.warlog.victory}</span>
          : ours.rank <= 3 ? <span className="cl-ic"><PlaceBadge rank={ours.rank} />{formatPlace(ours.rank, t)}</span>
          : formatPlace(ours.rank, t)

        return (
          <li key={key} className="warlog-week">
            <button className="warlog-row" onClick={() => toggleWeek(key)}>
              <span className="history-week-badge">
                {w.isColosseum ? <Icon name="columns" size={14} /> : `W${w.sectionIndex + 1}`}
              </span>
              <div className="warlog-info">
                <span className="warlog-place">{placeText}</span>
                <span className="muted small">{t.warlog.season} {w.seasonId}</span>
              </div>
              <div className="warlog-numbers">
                {ours && <span className="race-fame cl-ic">{fmt(ours.fame)} <Icon name="medal" size={13} className="cl-fame" /></span>}
                {ours && ours.trophyChange !== 0 && (
                  <span className={`warlog-trophy cl-ic ${ours.trophyChange > 0 ? 'trophy-up' : 'trophy-down'}`}>
                    {ours.trophyChange > 0 ? '+' : ''}{ours.trophyChange} <Icon name="trophy" size={12} />
                  </span>
                )}
              </div>
              <span className={`warlog-chevron ${isOpen ? 'warlog-chevron-open' : ''}`}><Icon name="chevronRight" size={16} /></span>
            </button>

            {isOpen && (
              <ul className="warlog-standings fade-in">
                {w.standings.map(s => {
                  const clanKey = `${key}-${s.rank}`
                  const hasPlayers = s.players && s.players.length > 0
                  const isExpanded = openClan === clanKey
                  return (
                    <li key={clanKey} className={`warlog-standing ${s.isOurClan ? 'race-ours' : ''}`}>
                      <button
                        className={`warlog-clan-row ${hasPlayers ? '' : 'warlog-clan-row-plain'}`}
                        onClick={() => hasPlayers && toggleClan(clanKey)}
                        disabled={!hasPlayers}
                      >
                        <span className={`race-pos ${s.rank === 1 ? 'race-pos-gold' : ''}`}>{s.rank}</span>
                        {/* Тап по строке раскрывает состав, тап по имени открывает клан:
                            вкладывать кнопку в кнопку нельзя, поэтому имя — span,
                            гасящий всплытие. */}
                        <span className="warlog-standing-name">
                          <span
                            className="clan-name-link"
                            role="button"
                            tabIndex={0}
                            onClick={e => { e.stopPropagation(); showClanPage(s.tag, s.name) }}
                          >
                            {s.name} <Icon name="external" size={11} />
                          </span>
                          {s.isOurClan && meLabel && <span className="me-badge">{meLabel}</span>}
                        </span>
                        <span className="race-fame">{fmt(s.fame)}</span>
                        <span className={`warlog-trophy ${s.trophyChange > 0 ? 'trophy-up' : s.trophyChange < 0 ? 'trophy-down' : 'muted'}`}>
                          {s.trophyChange > 0 ? '+' : ''}{s.trophyChange}
                        </span>
                        {hasPlayers && (
                          <span className={`warlog-chevron ${isExpanded ? 'warlog-chevron-open' : ''}`} style={{ fontSize: 12 }}><Icon name="chevronRight" size={13} /></span>
                        )}
                      </button>

                      {isExpanded && hasPlayers && (
                        <ul className="warlog-players fade-in">
                          {s.players!.map((p, i) => (
                            <li key={p.name} className="warlog-player-row">
                              <span className="warlog-player-rank">#{i + 1}</span>
                              <span className="warlog-player-name">{p.name}</span>
                              <span className="muted small cl-ic">{p.decksUsed > 0 ? <>{p.decksUsed}<Icon name="cards" size={11} /></> : ''}</span>
                              <span className="race-fame cl-ic">{fmt(p.fame)} <Icon name="medal" size={12} className="cl-fame" /></span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </li>
        )
      })}
    </ul>
  )
}

interface Props {
  log: WarLogWeek[]
}

export function WarLogCard({ log }: Props) {
  const { t } = useT()
  if (!log || log.length === 0) return null

  return (
    <section className="card warlog-card">
      <SectionHead icon="history" tone="blue" title={t.warlog.title} info={<p>{t.warlog.hint}</p>} />
      <WarLogWeeks weeks={log} meLabel={t.warlog.ours} />
    </section>
  )
}
