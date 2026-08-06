import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { Achievement, Achievements } from '../types'
import { fmt } from '../lib/format'
import { useT, type Translations } from '../lib/i18n'

const BADGE_ICONS: Record<Achievement['key'], string> = {
  streak: '🔥',
  perfectDays: '💯',
  mvpWeeks: '👑',
  totalFame: '🏅',
  warsPlayed: '⚔️',
}

const LEVEL_CLS = ['ach-none', 'ach-bronze', 'ach-silver', 'ach-gold']

/**
 * Витрина наград: личная коллекция значков (эффект владения) с открытым прогрессом
 * до следующего уровня (эффект Зейгарник — незакрытая шкала тянет доиграть).
 */
export function AchievementsCard() {
  const { t } = useT()
  const [data, setData] = useState<Achievements | null>(null)

  useEffect(() => {
    api.getMyAchievements().then(setData).catch(() => setData(null))
  }, [])

  if (!data || data.badges.length === 0) return null

  const goldCount = data.badges.filter(b => b.level === 3).length

  return (
    <div className="card ach-card">
      <div className="card-title-row">
        <div className="card-title">{t.ach.title}</div>
        <span className="muted small">{goldCount > 0 ? `🥇 ${goldCount}/${data.badges.length}` : ''}</span>
      </div>
      <div className="ach-grid">
        {data.badges.map(b => <Badge key={b.key} b={b} t={t} />)}
      </div>
    </div>
  )
}

function Badge({ b, t }: { b: Achievement; t: Translations }) {
  const label = t.ach.badges[b.key]
  // Прогресс шкалы: до следующего порога (или полная при золоте)
  const target = b.nextAt ?? b.thresholds[b.thresholds.length - 1]
  const pct = b.nextAt === null ? 100 : Math.min(100, Math.round((b.value / target) * 100))
  const levelName = [t.ach.lvlNone, t.ach.lvlBronze, t.ach.lvlSilver, t.ach.lvlGold][b.level]

  return (
    <div className={`ach-badge ${LEVEL_CLS[b.level]}`}>
      <span className="ach-icon">{BADGE_ICONS[b.key]}</span>
      <div className="ach-info">
        <span className="ach-name">{label} <span className="ach-lvl">{levelName}</span></span>
        <div className="ach-track">
          <div className="ach-fill" style={{ width: `${Math.max(3, pct)}%` }} />
        </div>
        <span className="muted small">
          {b.nextAt === null
            ? `${fmt(b.value)} — ${t.ach.maxed}`
            : `${fmt(b.value)} / ${fmt(b.nextAt)} ${t.ach.toNext}`}
        </span>
      </div>
    </div>
  )
}
