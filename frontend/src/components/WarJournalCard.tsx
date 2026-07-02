import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { WarJournal, WarBattleEntry } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

function battleTime(iso: string): string {
  const d = new Date(iso)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const mon = String(d.getMonth() + 1).padStart(2, '0')
  return `${day}.${mon} ${hh}:${mm}`
}

function Row({ b }: { b: WarBattleEntry }) {
  return (
    <li className={`jr-row ${b.won ? 'jr-win' : 'jr-loss'}`}>
      <span className="jr-icon">{b.won ? '✅' : '❌'}</span>
      <div className="jr-info">
        <span className="jr-name">{b.playerName}</span>
        <span className="muted small">{battleTime(b.battleTimeUtc)}</span>
      </div>
      <span className="jr-crowns">{b.crownsFor}:{b.crownsAgainst} 👑</span>
    </li>
  )
}

/**
 * Журнал военных боёв: кто и когда отыграл КВ + исход. Сверху счёт побед/поражений,
 * ниже 5 последних боёв; «Показать все» открывает полный список на весь экран.
 */
export function WarJournalCard() {
  const { t } = useT()
  const [j, setJournal] = useState<WarJournal | null>(null)
  const [full, setFull] = useState(false)

  useEffect(() => {
    api.getWarJournal().then(setJournal).catch(() => setJournal(null))
  }, [])

  if (!j || j.total === 0) return null

  const latest = j.battles.slice(0, 5)

  return (
    <section className="card">
      <div className="card-title-row">
        <div className="card-title">{t.journal.title}</div>
        <span className="jr-score">
          <span className="jr-score-win">✅ {fmt(j.won)}</span>
          {' · '}
          <span className="jr-score-loss">❌ {fmt(j.lost)}</span>
        </span>
      </div>

      <ul className="jr-list">
        {latest.map(b => <Row key={`${b.playerTag}-${b.battleTimeUtc}`} b={b} />)}
      </ul>

      {j.total > 5 && (
        <button className="btn-mini jr-more" onClick={() => { haptic('light'); setFull(true) }}>
          {t.journal.showAll} ({fmt(j.total)})
        </button>
      )}

      {full && <WarJournalModal journal={j} t={t} onClose={() => setFull(false)} />}
    </section>
  )
}

function WarJournalModal({ journal, t, onClose }: { journal: WarJournal; t: Translations; onClose: () => void }) {
  return (
    <div className="notif-overlay" role="dialog" aria-modal="true">
      <div className="notif-sheet fade-in">
        <div className="notif-head">
          <h2>{t.journal.title}</h2>
          <button className="notif-close" onClick={onClose} aria-label="✕">✕</button>
        </div>
        <p className="muted small" style={{ margin: '0 0 10px' }}>
          ✅ {fmt(journal.won)} {t.journal.wonLabel} · ❌ {fmt(journal.lost)} {t.journal.lostLabel} · {fmt(journal.total)} {t.journal.totalLabel}
        </p>
        <ul className="jr-list">
          {journal.battles.map(b => <Row key={`${b.playerTag}-${b.battleTimeUtc}`} b={b} />)}
        </ul>
      </div>
    </div>
  )
}
