import { useEffect } from 'react'
import type { PlayerStatus, PlayStatus } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'

const STATUS_META: Record<PlayStatus, { icon: string; label: string; cls: string }> = {
  played: { icon: '✅', label: 'Сыграл все колоды', cls: 'status-played' },
  timeLeft: { icon: '⏳', label: 'Ещё есть время', cls: 'status-timeleft' },
  notPlayed: { icon: '❌', label: 'Не доиграл — дедлайн близко', cls: 'status-notplayed' },
}

interface Props {
  player: PlayerStatus
  isMe: boolean
  onClose: () => void
}

/** Инфо-окно игрока: вся статистика недели + прогнозы. Закрытие — тап по фону или ✕. */
export function PlayerInfoModal({ player: p, isMe, onClose }: Props) {
  const meta = STATUS_META[p.status]

  // Блокируем прокрутку фона, пока открыто окно
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

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
              {isMe && <span className="me-badge">ты</span>}
              {p.name}
            </h3>
            <span className="muted small">{p.playerTag} · #{p.rank} в клане{!p.isLinked && ' · нет TG'}</span>
          </div>
          <button className="modal-close" onClick={close} aria-label="Закрыть">✕</button>
        </div>

        <div className={`modal-status ${meta.cls}`}>
          {meta.icon} {meta.label} · колоды сегодня: <strong>{p.decksUsedToday}/4</strong>
        </div>

        <div className="modal-grid">
          <ModalStat value={fmt(p.fame)} label="🏅 слава за неделю" />
          <ModalStat value={p.avgFamePerAttack > 0 ? String(Math.round(p.avgFamePerAttack)) : '—'} label="⚡ слава / атака" />
          <ModalStat value={`${p.decksUsed}`} label="🃏 атак за неделю" />
          <ModalStat value={p.boatAttacks > 0 ? String(p.boatAttacks) : '—'} label="🚤 атаки лодки" />
          <ModalStat value={fmt(p.projectedDayFame)} label="🔮 прогноз на день" accent />
          <ModalStat value={fmt(p.projectedWeekFame)} label="🔮 прогноз недели" accent />
        </div>

        {p.repairPoints > 0 && (
          <p className="muted small modal-extra">🔧 Очки ремонта: {fmt(p.repairPoints)}</p>
        )}
      </div>
    </div>
  )
}

function ModalStat({ value, label, accent }: { value: string; label: string; accent?: boolean }) {
  return (
    <div className={`modal-stat ${accent ? 'modal-stat-accent' : ''}`}>
      <span className="modal-stat-value">{value}</span>
      <span className="modal-stat-label">{label}</span>
    </div>
  )
}
