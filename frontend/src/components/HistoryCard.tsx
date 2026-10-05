import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { ClanHistory } from '../types'
import { fmt, fmtShort } from '../lib/format'
import { useT } from '../lib/i18n'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

type State =
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'error' }
  | { kind: 'ready'; data: ClanHistory }

export function HistoryCard() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const { t } = useT()

  useEffect(() => {
    api.getMyClanHistory()
      .then(data => setState(data.weeks.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setState({ kind: 'error' }))
  }, [])

  if (state.kind === 'loading') return null
  if (state.kind === 'error') return null

  if (state.kind === 'empty') {
    return (
      <section className="card">
        <SectionHead icon="calendar" tone="green" title={t.history.title} />
        <p className="muted small">{t.history.emptyNote}</p>
      </section>
    )
  }

  const weeks = state.data.weeks
  const maxWeekFame = Math.max(...weeks.map(w => w.finalFame), 1)

  return (
    <section className="card">
      <SectionHead icon="calendar" tone="green" title={t.history.title} />

      <div className="history-weeks">
        {[...weeks].reverse().map(w => (
          <div key={`${w.seasonId}-${w.sectionIndex}`} className="history-week">
            <div className="history-bar-wrap">
              <div
                className={`history-bar ${w.isColosseum ? 'history-bar-colosseum' : ''}`}
                style={{ height: `${Math.max(8, Math.round((w.finalFame / maxWeekFame) * 100))}%` }}
                title={`${fmt(w.finalFame)} ${t.leaderboard.medals}`}
              />
            </div>
            <span className="history-fame">{fmtShort(w.finalFame)}</span>
            <span className="history-label">{w.isColosseum ? <Icon name="columns" size={12} /> : `н.${w.sectionIndex + 1}`}</span>
          </div>
        ))}
      </div>

      {weeks[0] && (
        <div className="history-latest">
          <div className="history-latest-title">
            {t.history.currentWeek} {weeks[0].isColosseum && t.history.colosseum}
          </div>
          <div className="history-days">
            {weeks[0].days.map(d => (
              <div key={d.periodIndex} className="history-day">
                <span className="history-day-num">{t.history.day} {d.dayNumber}</span>
                <span className="history-day-fame">+{fmt(d.dayFame)}</span>
              </div>
            ))}
          </div>
          {weeks[0].topPlayers.length > 0 && (
            <p className="muted small history-top">
              {t.history.leaders} {weeks[0].topPlayers.map(p => p.name).join(', ')}
            </p>
          )}
          {weeks[0].myFame !== null && (
            <p className="muted small cl-ic">{t.history.myContrib} <strong>{fmt(weeks[0].myFame)}</strong> <Icon name="medal" size={12} className="cl-fame" /></p>
          )}
        </div>
      )}
    </section>
  )
}
