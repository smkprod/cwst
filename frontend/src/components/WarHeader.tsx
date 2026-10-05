import type { ClanStatus } from '../types'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'
import { Icon } from './ui/Icon'

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
      {/* Название — отдельной строкой с кнопками справа; статус дня и дедлайн — ниже:
          в одну строку на 390px бейдж, шестерёнка и RU/UA/EN не помещались */}
      <div className="war-header-top">
        <h1>{status.clanName}</h1>
        <div className="war-header-actions">
          {canManage && onOpenSettings && (
            <button className="cl-icon-btn" onClick={onOpenSettings} aria-label={t.notif.title}>
              <Icon name="gear" size={18} />
            </button>
          )}
          <LangSwitcher />
        </div>
      </div>

      <div className="war-header-meta">
        <span className={`badge ${isWar ? 'badge-war' : 'badge-training'}`}>
          {t.period[status.periodType]}
          {isWar && day !== null && ` ${t.header.dayOf4} ${day}/4`}
        </span>
        {isWar && (
          <p className="deadline cl-deadline">
            <Icon name="clock" size={15} /> {t.header.untilEnd} <strong>~{status.hoursLeft} {t.header.h}</strong>
          </p>
        )}
      </div>

      <div className="progress-row">
        <div className="progress-track" role="progressbar" aria-valuenow={pct}>
          <div className="progress-fill" style={{ width: `${pct}%` }} />
        </div>
        <span className="progress-label">{played}/{total} {t.header.played}</span>
      </div>
    </header>
  )
}
