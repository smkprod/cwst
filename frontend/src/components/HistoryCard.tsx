import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ClanHistory, Plan } from '../types'
import { fmt, fmtShort } from '../lib/format'

type State =
  | { kind: 'loading' }
  | { kind: 'locked' }
  | { kind: 'empty' }
  | { kind: 'error' }
  | { kind: 'ready'; data: ClanHistory }

export function HistoryCard({ plan }: { plan: Plan }) {
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    if (plan !== 'pro') {
      setState({ kind: 'locked' })
      return
    }
    api.getMyClanHistory()
      .then(data => setState(data.weeks.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(e => setState(e instanceof ApiError && e.code === 'pro_required' ? { kind: 'locked' } : { kind: 'error' }))
  }, [plan])

  if (state.kind === 'loading') return null
  if (state.kind === 'error') return null

  if (state.kind === 'locked') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">📈 История войн</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">Динамика клана по неделям и дням войны — доступно на тарифе Pro. 🔒</p>
      </section>
    )
  }

  if (state.kind === 'empty') {
    return (
      <section className="card">
        <div className="card-title">📈 История войн</div>
        <p className="muted small">
          История копится автоматически с каждым днём войны — загляни сюда после первого военного дня.
        </p>
      </section>
    )
  }

  const weeks = state.data.weeks
  const maxWeekFame = Math.max(...weeks.map(w => w.finalFame), 1)

  return (
    <section className="card">
      <div className="card-title" style={{ marginBottom: 10 }}>📈 История войн</div>

      <div className="history-weeks">
        {[...weeks].reverse().map(w => (
          <div key={`${w.seasonId}-${w.sectionIndex}`} className="history-week">
            <div className="history-bar-wrap">
              <div
                className={`history-bar ${w.isColosseum ? 'history-bar-colosseum' : ''}`}
                style={{ height: `${Math.max(8, Math.round((w.finalFame / maxWeekFame) * 100))}%` }}
                title={`${fmt(w.finalFame)} славы`}
              />
            </div>
            <span className="history-fame">{fmtShort(w.finalFame)}</span>
            <span className="history-label">{w.isColosseum ? '🏟' : `н.${w.sectionIndex + 1}`}</span>
          </div>
        ))}
      </div>

      {/* Детали последней недели */}
      {weeks[0] && (
        <div className="history-latest">
          <div className="history-latest-title">
            Текущая неделя {weeks[0].isColosseum && '· Колизей'}
          </div>
          <div className="history-days">
            {weeks[0].days.map(d => (
              <div key={d.periodIndex} className="history-day">
                <span className="history-day-num">День {d.dayNumber}</span>
                <span className="history-day-fame">+{fmt(d.dayFame)}</span>
              </div>
            ))}
          </div>
          {weeks[0].topPlayers.length > 0 && (
            <p className="muted small history-top">
              👑 Лидеры: {weeks[0].topPlayers.map(p => p.name).join(', ')}
            </p>
          )}
          {weeks[0].myFame !== null && (
            <p className="muted small">Твой вклад на этой неделе: <strong>{fmt(weeks[0].myFame)}</strong> 🏅</p>
          )}
        </div>
      )}
    </section>
  )
}
