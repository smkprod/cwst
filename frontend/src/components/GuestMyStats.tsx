import type { ClanStatus } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { TournamentHistoryCard } from './TournamentHistoryCard'

interface Props {
  data: ClanStatus
  myPlayerTag: string
}

export function GuestMyStats({ data, myPlayerTag }: Props) {
  const { t } = useT()
  const me = data.players.find(p =>
    p.playerTag.toUpperCase() === myPlayerTag.toUpperCase()
  )

  if (!me) {
    return (
      <div className="card" style={{ textAlign: 'center' }}>
        <p className="muted small" style={{ margin: 0 }}>{t.guest.notInWarRoster}</p>
      </div>
    )
  }

  const contrib = data.stats.totalFame > 0
    ? Math.round(me.fame * 100 / data.stats.totalFame)
    : 0

  return (
    <div>
      <div className="me-header">
        <div className="me-title-row">
          <h2 className="me-name">{me.name}</h2>
        </div>
        <p className="muted small">
          {data.clanName} · {t.me.rankOf} #{me.rank} {t.me.of} {data.players.length}
        </p>
      </div>

      <div className="me-grid">
        <div className="card me-stat">
          <span className="me-stat-value">{fmt(me.fame)}</span>
          <span className="me-stat-label">{t.me.weekMedals}</span>
        </div>
        <div className="card me-stat">
          <span className="me-stat-value">{contrib}%</span>
          <span className="me-stat-label">{t.me.contrib}</span>
        </div>
        <div className="card me-stat">
          <span className="me-stat-value">{me.decksUsedToday}/4</span>
          <span className="me-stat-label">{t.me.decksToday}</span>
        </div>
        <div className="card me-stat">
          <span className="me-stat-value">
            {me.avgFamePerAttack > 0 ? Math.round(me.avgFamePerAttack) : '—'}
          </span>
          <span className="me-stat-label">{t.me.medalsPerAttack}</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(me.projectedDayFame)}</span>
          <span className="me-stat-label">{t.me.dayForecast}</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(me.projectedWeekFame)}</span>
          <span className="me-stat-label">{t.me.weekForecast}</span>
        </div>
      </div>

      <TournamentHistoryCard playerTag={myPlayerTag} />

      <div className="card guest-link-card">
        <p className="muted small" style={{ margin: 0, textAlign: 'center' }}>
          {t.guest.linkForStats}
        </p>
      </div>
    </div>
  )
}
