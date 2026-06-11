import type { ClanStatus } from '../types'

const PERIOD_LABEL: Record<ClanStatus['periodType'], string> = {
  training: 'Тренировочные дни',
  warDay: 'День войны',
  colosseum: 'Колизей',
}

/** Номер дня войны: periodIndex 3..6 -> день 1..4 */
function warDayNumber(periodIndex: number): number | null {
  if (periodIndex < 3) return null
  return Math.min(4, periodIndex - 2)
}

export function WarHeader({ status }: { status: ClanStatus }) {
  const played = status.stats.playersPlayed
  const total = status.players.length
  const pct = total > 0 ? Math.round((played / total) * 100) : 0
  const isWar = status.periodType !== 'training'
  const day = warDayNumber(status.periodIndex)

  return (
    <header className="war-header">
      <div className="war-header-top">
        <h1>{status.clanName}</h1>
        <span className={`badge ${isWar ? 'badge-war' : 'badge-training'}`}>
          {PERIOD_LABEL[status.periodType]}
          {isWar && day !== null && ` · день ${day}/4`}
        </span>
      </div>

      {isWar && (
        <p className="deadline">
          ⏰ До конца дня: <strong>~{status.hoursLeft} ч</strong>
        </p>
      )}

      <div className="progress-row">
        <div className="progress-track" role="progressbar" aria-valuenow={pct}>
          <div className="progress-fill" style={{ width: `${pct}%` }} />
        </div>
        <span className="progress-label">{played}/{total} сыграли</span>
      </div>
    </header>
  )
}
