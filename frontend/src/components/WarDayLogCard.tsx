import type { WarDayLog } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

/**
 * Официальный по-дневный лог гонки (periodLogs из CR API): место клана на конец
 * каждого военного дня + остаток защит. Никаких расчётов — всё прямо из API.
 */
export function WarDayLogCard({ dayLogs }: { dayLogs: WarDayLog[] }) {
  const { t } = useT()
  // Только военные дни (dayIndex 3..6 => Д1..Д4); тренировка очков не даёт.
  const warDays = dayLogs.filter(d => d.dayIndex >= 3).sort((a, b) => a.dayIndex - b.dayIndex)
  if (warDays.length === 0) return null

  const latest = warDays[warDays.length - 1]
  const defenses = latest.numOfDefensesRemaining

  const rankClass = (r: number) => (r === 1 ? 'wd-rank-1' : r === 2 ? 'wd-rank-2' : r === 3 ? 'wd-rank-3' : '')

  return (
    <section className="card">
      <div className="card-title" style={{ marginBottom: 10 }}>{t.warDays.title}</div>
      <div className="wd-row">
        {warDays.map(d => (
          <div key={d.dayIndex} className="wd-day">
            <span className={`wd-rank ${rankClass(d.endOfDayRank)}`}>#{d.endOfDayRank}</span>
            <span className="wd-points">{fmt(d.pointsEarned)}</span>
            <span className="wd-label">{t.warDays.dayShort}{d.dayIndex - 2}</span>
          </div>
        ))}
      </div>
      {defenses > 0 && (
        <p className="muted small wd-defenses">🛡 {t.warDays.defensesLeft} {defenses}</p>
      )}
    </section>
  )
}
