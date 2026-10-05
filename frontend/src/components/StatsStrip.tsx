import type { ClanStats } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { Icon, type IconName } from './ui/Icon'

export function StatsStrip({ stats }: { stats: ClanStats }) {
  const { t } = useT()
  const chips: { icon: IconName; tone: string; value: string; label: string }[] = [
    { icon: 'medal', tone: 'gold', value: fmt(stats.totalFame), label: t.stats.weekMedals },
    { icon: 'cards', tone: 'blue', value: `${stats.totalDecksUsedToday}/${stats.maxDecksToday}`, label: t.stats.decksToday },
    { icon: 'bolt', tone: 'orange', value: stats.avgFamePerAttack.toFixed(1), label: t.stats.medalsPerBattle },
    { icon: 'users', tone: 'green', value: String(stats.activePlayers), label: t.stats.active },
  ]

  return (
    <section className="stats-strip">
      {chips.map(c => (
        <div key={c.label} className="stat-chip">
          <span className={`stat-chip-icon cl-stat-icon cl-stat-${c.tone}`}><Icon name={c.icon} size={16} /></span>
          <span className="stat-chip-value">{c.value}</span>
          <span className="stat-chip-label">{c.label}</span>
        </div>
      ))}
    </section>
  )
}
