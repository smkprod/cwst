import { useEffect, useState } from 'react'
import type { PlayerHistory, PlayerStatus, PlayStatus } from '../types'
import { api } from '../lib/api'
import { fmt } from '../lib/format'
import { haptic, openExternalLink } from '../lib/telegram'
import { useT, roleLabel } from '../lib/i18n'

interface Props {
  player: PlayerStatus
  isMe: boolean
  onClose: () => void
}

export function PlayerInfoModal({ player: p, isMe, onClose }: Props) {
  const [history, setHistory] = useState<PlayerHistory | null>(null)
  const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')
  const { t } = useT()

  const STATUS_META: Record<PlayStatus, { icon: string; label: string; cls: string }> = {
    played: { icon: '✅', label: t.playerModal.played, cls: 'status-played' },
    timeLeft: { icon: '⏳', label: t.playerModal.timeLeft, cls: 'status-timeleft' },
    notPlayed: { icon: '❌', label: t.playerModal.notPlayed, cls: 'status-notplayed' },
  }

  const meta = STATUS_META[p.status]

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

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

  const roleName = roleLabel(p.role, t)

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />

        <div className="modal-head">
          <div className="modal-title-wrap">
            <h3 className="modal-name">
              {isMe && <span className="me-badge">{t.leaderboard.you}</span>}
              {p.name}
            </h3>
            <span className="muted small">
              {p.playerTag} · #{p.rank} {t.playerModal.rankInClan}
              {roleName && ` · ${roleName}`}
              {!p.isLinked && ` · ${t.playerModal.noTg}`}
            </span>
          </div>
          <button className="modal-close" onClick={close} aria-label={t.playerModal.close}>✕</button>
        </div>

        <div className={`modal-status ${meta.cls}`}>
          {meta.icon} {meta.label} · {t.playerModal.decksToday} <strong>{p.decksUsedToday}/4</strong>
        </div>

        {p.dnaLabel && (
          <div className="dna-row">
            <span className="dna-chip">{p.dnaLabel}</span>
            {p.reliabilityScore > 0 && (
              <span className="dna-reliability">
                {t.playerModal.reliability}
                <span className="dna-track">
                  <span
                    className={`dna-fill ${p.reliabilityScore >= 70 ? 'fill-good' : p.reliabilityScore >= 45 ? 'fill-mid' : 'fill-bad'}`}
                    style={{ width: `${p.reliabilityScore}%` }}
                  />
                </span>
                <strong>{p.reliabilityScore}</strong>
              </span>
            )}
          </div>
        )}

        <div className="modal-grid">
          <ModalStat value={fmt(p.fame)} label={t.playerModal.weekMedals} />
          <ModalStat
            value={p.avgFamePerAttack > 0 ? String(Math.round(p.avgFamePerAttack)) : '—'}
            label={t.playerModal.avgAttack}
          />
          <ModalStat value={String(p.warDecksUsed)} label={t.playerModal.totalAttacks} />
          <ModalStat value={p.boatAttacks > 0 ? String(p.boatAttacks) : '—'} label={t.playerModal.boatAttacks} />
          <ModalStat value={fmt(p.projectedDayFame)} label={t.playerModal.dayForecast} accent />
          <ModalStat value={fmt(p.projectedWeekFame)} label={t.playerModal.weekForecast} accent />
        </div>

        {p.repairPoints > 0 && (
          <p className="muted small modal-extra">{t.playerModal.repairPoints} {fmt(p.repairPoints)}</p>
        )}

        <p className="muted small modal-extra">{t.playerModal.forecastNote}</p>

        <div className="history-block">
          <div className="card-title history-title">{t.playerModal.pastWars}</div>

          {historyState === 'loading' && <p className="muted small">{t.playerModal.loadingHistory}</p>}
          {historyState === 'error' && <p className="muted small">{t.playerModal.historyError}</p>}

          {historyState === 'ready' && history && history.weeks.length === 0 && (
            <p className="muted small">{t.playerModal.noHistory}</p>
          )}

          {historyState === 'ready' && history && history.weeks.length > 0 && (
            <ul className="history-week-list">
              {history.weeks.map(w => (
                <li key={`${w.seasonId}-${w.sectionIndex}-${w.clanTag}`} className="history-week-row">
                  <span className="history-week-badge">
                    {w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}
                  </span>
                  <div className="history-week-info">
                    <span className="history-week-clan">{w.clanName}</span>
                    <span className="muted small">{t.playerModal.season} {w.seasonId} · ⚡ {w.avgFamePerAttack > 0 ? Math.round(w.avgFamePerAttack) : '—'}</span>
                  </div>
                  <span className="history-week-fame">{fmt(w.fame)} 🏅</span>
                </li>
              ))}
            </ul>
          )}

          <button className="btn-mini history-royaleapi" onClick={() => openExternalLink(royaleApiUrl)}>
            {t.playerModal.royaleApi}
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
