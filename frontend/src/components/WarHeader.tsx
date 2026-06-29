import type { ClanStatus } from '../types'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'

function warDayNumber(periodIndex: number): number | null {
  if (periodIndex < 3) return null
  return Math.min(4, periodIndex - 2)
}

export function WarHeader(
  { status, canManage = false, onOpenSettings }:
  { status: ClanStatus; canManage?: boolean; onOpenSettings?: () => void },
) {
  const { t } = useT()
  const played = status.stats.playersPlayed
  const total = status.players.length
  const pct = total > 0 ? Math.round((played / total) * 100) : 0
  const isWar = status.periodType !== 'training'
  const day = warDayNumber(status.periodIndex)

  return (
    <header className="war-header">
      <div className="war-header-top">
        <h1>{status.clanName}</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span className={`badge ${isWar ? 'badge-war' : 'badge-training'}`}>
            {t.period[status.periodType]}
            {isWar && day !== null && ` ${t.header.dayOf4} ${day}/4`}
          </span>
          {canManage && onOpenSettings && (
            <button className="war-settings-btn" onClick={onOpenSettings} aria-label={t.notif.title}>⚙️</button>
          )}
          <LangSwitcher />
        </div>
      </div>

      {isWar && (
        <p className="deadline">
          ⏰ {t.header.untilEnd} <strong>~{status.hoursLeft} {t.header.h}</strong>
        </p>
      )}

      <div className="progress-row">
        <div className="progress-track" role="progressbar" aria-valuenow={pct}>
          <div className="progress-fill" style={{ width: `${pct}%` }} />
        </div>
        <span className="progress-label">{played}/{total} {t.header.played}</span>
      </div>
    </header>
  )
}
