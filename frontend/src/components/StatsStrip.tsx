import type { ClanStats } from '../types'
import { fmt, fmtShort } from '../lib/format'

export function StatsStrip({ stats }: { stats: ClanStats }) {
  const chips = [
    { icon: '🏅', value: fmt(stats.totalFame), label: 'медали недели' },
    { icon: '🃏', value: `${stats.totalDecksUsedToday}/${stats.maxDecksToday}`, label: 'колод сегодня' },
    { icon: '⚡', value: fmtShort(Math.round(stats.avgFamePerAttack)), label: 'медалей/бой' },
    { icon: '👥', value: String(stats.activePlayers), label: 'активных' },
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
