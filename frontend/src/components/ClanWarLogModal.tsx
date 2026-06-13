import { useEffect, useState } from 'react'
import type { RaceClan, WarLogWeek } from '../types'
import { api } from '../lib/api'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { WarLogWeeks } from './WarLogCard'

interface Props {
  clan: RaceClan
  onClose: () => void
}

/** Инфо-окно клана из гонки: история его прошлых войн (официальный riverracelog). */
export function ClanWarLogModal({ clan, onClose }: Props) {
  const [weeks, setWeeks] = useState<WarLogWeek[]>([])
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  // Блокируем прокрутку фона, пока открыто окно
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    api.getClanWarLog(clan.tag)
      .then(log => { if (alive) { setWeeks(log.weeks); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [clan.tag])

  const close = () => {
    haptic('light')
    onClose()
  }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />

        <div className="modal-head">
          <div className="modal-title-wrap">
            <h3 className="modal-name">
              {clan.name}
              {clan.isOurClan && <span className="me-badge">мы</span>}
            </h3>
            <span className="muted small">
              {clan.tag}{clan.warTrophies > 0 && ` · 🏆 ${fmt(clan.warTrophies)}`}
            </span>
          </div>
          <button className="modal-close" onClick={close} aria-label="Закрыть">✕</button>
        </div>

        <div className="card-title history-title">📜 Прошлые войны</div>

        {state === 'loading' && <p className="muted small">Загружаю журнал…</p>}
        {state === 'error' && <p className="muted small">Журнал недоступен.</p>}
        {state === 'ready' && weeks.length === 0 && (
          <p className="muted small">У клана пока нет завершённых войн в журнале.</p>
        )}
        {state === 'ready' && weeks.length > 0 && <WarLogWeeks weeks={weeks} />}
      </div>
    </div>
  )
}
