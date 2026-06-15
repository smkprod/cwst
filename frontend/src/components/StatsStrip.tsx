import type { ClanStats } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

export function StatsStrip({ stats }: { stats: ClanStats }) {
  const { t } = useT()
  const chips = [
    { icon: '🏅', value: fmt(stats.totalFame), label: t.stats.weekMedals },
    { icon: '🃏', value: `${stats.totalDecksUsedToday}/${stats.maxDecksToday}`, label: t.stats.decksToday },
    { icon: '⚡', value: stats.avgFamePerAttack.toFixed(1), label: t.stats.medalsPerBattle },
    { icon: '👥', value: String(stats.activePlayers), label: t.stats.active },
  ]

  return (
    <section className="stats-strip">
      {chips.map(c => (
        <div key={c.label} className="stat-chip">
          <span className="stat-chip-icon">{c.icon}</span>
          <span className="stat-chip-value">{c.value}</span>
          <span className="stat-chip-label">{c.label}</span>
        </div>
      ))}
    </section>
  )
}
