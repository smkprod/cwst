import { useEffect, useState } from 'react'
import type { PlayerHistory, PlayerStatus, PlayStatus } from '../types'
import { api } from '../lib/api'
import { fmt } from '../lib/format'
import { haptic, openExternalLink } from '../lib/telegram'

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

/** Инфо-окно игрока: статистика недели, прогнозы и история прошлых войн. */
export function PlayerInfoModal({ player: p, isMe, onClose }: Props) {
  const meta = STATUS_META[p.status]
  const [history, setHistory] = useState<PlayerHistory | null>(null)
  const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')

  // Блокируем прокрутку фона, пока открыто окно
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  // История прошлых войн (по данным сервиса)
  useEffect(() => {
    let alive = true
    api.getPlayerHistory(p.playerTag)
      .then(h => { if (alive) { setHistory(h); setHistoryState('ready') } })
      .catch(() => { if (alive) setHistoryState('error') })
    return () => { alive = false }
  }, [p.playerTag])

  const close = () => {
    haptic('light')
    onClose()
  }

  const royaleApiUrl = history?.royaleApiUrl
    ?? `https://royaleapi.com/player/${encodeURIComponent(p.playerTag.replace('#', ''))}`

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
            <span className="muted small">{p.playerTag} · #{p.rank} в клане{p.role && ` · ${p.role}`}{!p.isLinked && ' · нет TG'}</span>
          </div>
          <button className="modal-close" onClick={close} aria-label="Закрыть">✕</button>
        </div>

        <div className={`modal-status ${meta.cls}`}>
          {meta.icon} {meta.label} · колоды сегодня: <strong>{p.decksUsedToday}/4</strong>
        </div>

        <div className="modal-grid">
          <ModalStat value={fmt(p.fame)} label="🏅 медали за неделю" />
          <ModalStat
            value={p.avgFamePerAttack > 0 ? String(Math.round(p.avgFamePerAttack)) : '—'}
            label="⚡ медалей за бой"
          />
          <ModalStat value={String(p.warDecksUsed)} label="⚔️ атак всего" />
          <ModalStat value={p.boatAttacks > 0 ? String(p.boatAttacks) : '—'} label="🚤 атаки лодки" />
          <ModalStat value={fmt(p.projectedDayFame)} label="🔮 прогноз на день" accent />
          <ModalStat value={fmt(p.projectedWeekFame)} label="🔮 прогноз недели" accent />
        </div>

        {p.repairPoints > 0 && (
          <p className="muted small modal-extra">🔧 Очки ремонта: {fmt(p.repairPoints)}</p>
        )}

        {/* История прошлых войн — как Race Log на cwstats */}
        <div className="history-block">
          <div className="card-title history-title">📜 Прошлые войны</div>

          {historyState === 'loading' && <p className="muted small">Загружаю историю…</p>}
          {historyState === 'error' && <p className="muted small">История недоступна.</p>}

          {historyState === 'ready' && history && history.weeks.length === 0 && (
            <p className="muted small">
              Пока нет данных — история копится с момента подключения клана к боту.
            </p>
          )}

          {historyState === 'ready' && history && history.weeks.length > 0 && (
            <ul className="history-weeks">
              {history.weeks.map(w => (
                <li key={`${w.seasonId}-${w.sectionIndex}-${w.clanTag}`} className="history-week-row">
                  <span className="history-week-badge">
                    {w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}
                  </span>
                  <div className="history-week-info">
                    <span className="history-week-clan">{w.clanName}</span>
                    <span className="muted small">сезон {w.seasonId} · ⚡ {w.avgFamePerAttack > 0 ? Math.round(w.avgFamePerAttack) : '—'}</span>
                  </div>
                  <span className="history-week-fame">{fmt(w.fame)} 🏅</span>
                </li>
              ))}
            </ul>
          )}

          <button className="btn-mini history-royaleapi" onClick={() => openExternalLink(royaleApiUrl)}>
            Полный профиль на RoyaleAPI ↗
          </button>
        </div>
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
