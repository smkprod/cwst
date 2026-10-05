import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { WarJournal, WarBattleEntry } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

function Mark({ won, size = 14 }: { won: boolean; size?: number }) {
  return <Icon name={won ? 'check' : 'x'} size={size} className={won ? 'cl-ok' : 'cl-bad'} />
}

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
      <span className={`jr-icon cl-jr-mark ${b.won ? 'cl-ok' : 'cl-bad'}`}><Mark won={b.won} /></span>
      <div className="jr-info">
        <span className="jr-name">{b.playerName}</span>
        <span className="muted small">{battleTime(b.battleTimeUtc)}</span>
      </div>
      <span className="jr-crowns cl-ic">{b.crownsFor}:{b.crownsAgainst} <Icon name="crown" size={13} className="cl-fame" /></span>
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

  // Свёрнут по умолчанию: счёт побед/поражений виден сразу в шапке, сами бои — под тап,
  // чтобы не растягивать экран войны списком.
  return (
    <details className="card collapse-card">
      <summary className="card-title-row collapse-summary">
        <SectionHead
          icon="swords"
          tone="red"
          title={t.journal.title}
          aside={
            <span className="jr-score cl-score">
              <span className="jr-score-win cl-ic"><Mark won /> {fmt(j.won)}</span>
              <span className="jr-score-loss cl-ic"><Mark won={false} /> {fmt(j.lost)}</span>
            </span>
          }
        />
      </summary>

      <ul className="jr-list">
        {latest.map(b => <Row key={`${b.playerTag}-${b.battleTimeUtc}`} b={b} />)}
      </ul>

      {j.total > 5 && (
        <button className="btn-mini jr-more" onClick={() => { haptic('light'); setFull(true) }}>
          {t.journal.showAll} ({fmt(j.total)})
        </button>
      )}

      {full && <WarJournalModal journal={j} t={t} onClose={() => setFull(false)} />}
    </details>
  )
}

function WarJournalModal({ journal, t, onClose }: { journal: WarJournal; t: Translations; onClose: () => void }) {
  return (
    <div className="notif-overlay" role="dialog" aria-modal="true">
      <div className="notif-sheet fade-in">
        <div className="notif-head">
          <h2>{t.journal.title}</h2>
          <button className="notif-close" onClick={onClose} aria-label={t.warlog.close}><Icon name="x" size={16} /></button>
        </div>
        <p className="muted small cl-score" style={{ margin: '0 0 10px', flexWrap: 'wrap' }}>
          <span className="cl-ic"><Mark won size={13} /> {fmt(journal.won)} {t.journal.wonLabel}</span>
          <span className="cl-ic"><Mark won={false} size={13} /> {fmt(journal.lost)} {t.journal.lostLabel}</span>
          <span className="cl-ic">{fmt(journal.total)} {t.journal.totalLabel}</span>
        </p>
        <ul className="jr-list">
          {journal.battles.map(b => <Row key={`${b.playerTag}-${b.battleTimeUtc}`} b={b} />)}
        </ul>
      </div>
    </div>
  )
}
