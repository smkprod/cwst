import { useEffect, useState } from 'react'
import type { PlayerHistory, PlayerStatus, PlayStatus } from '../types'
import { api } from '../lib/api'
import { fmt } from '../lib/format'
import { copyText, haptic, hapticNotify, openExternalLink } from '../lib/telegram'
import { useT, roleLabel } from '../lib/i18n'

const ROLE_ICON: Record<string, string> = {
  leader: '👑',
  coLeader: '⚜️',
  elder: '⭐',
}

interface Props {
  player: PlayerStatus
  isMe: boolean
  onClose: () => void
}

export function PlayerInfoModal({ player: p, isMe, onClose }: Props) {
  const [history, setHistory] = useState<PlayerHistory | null>(null)
  const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')
  // Респект 👏: 'idle' — можно дать, 'sent' — только что дал, 'used' — лимит на сегодня исчерпан
  const [respect, setRespect] = useState<'loading' | 'idle' | 'sent' | 'used'>('loading')
  // Тег скопирован: короткая подсветка вместо тоста — иначе непонятно, сработало ли
  const [tagCopied, setTagCopied] = useState<'idle' | 'done' | 'fail'>('idle')
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

  useEffect(() => {
    if (isMe) return                       // себе респект не дают
    let alive = true
    api.getRespectStatus()
      .then(s => { if (alive) setRespect(s.givenToday ? 'used' : 'idle') })
      .catch(() => { if (alive) setRespect('idle') })
    return () => { alive = false }
  }, [isMe])

  const sendRespect = async () => {
    haptic('medium')
    setRespect('sent')                     // оптимистично: ощущение мгновенной награды
    try { await api.giveRespect(p.playerTag) } catch { setRespect('used') }
  }

  const close = () => {
    haptic('light')
    onClose()
  }

  // Тег нужен постоянно: вбить в игре, отправить лидеру, привязать командой /bind.
  // Раньше его переписывали с экрана руками — а в теге легко перепутать O и 0.
  const copyTag = async () => {
    haptic('light')
    const ok = await copyText(p.playerTag)
    hapticNotify(ok ? 'success' : 'error')
    setTagCopied(ok ? 'done' : 'fail')
    setTimeout(() => setTagCopied('idle'), 1600)
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
              <button
                type="button"
                className={`tag-copy ${tagCopied !== 'idle' ? 'tag-copy-done' : ''}`}
                onClick={copyTag}
                title={t.playerModal.copyTag}
                aria-label={t.playerModal.copyTag}
              >
                {p.playerTag}
                <span className="tag-copy-icon">
                  {tagCopied === 'idle' ? '⧉' : tagCopied === 'done' ? '✓' : '✕'}
                </span>
              </button>
              {' · '}#{p.rank} {t.playerModal.rankInClan}
              {!p.isLinked && ` · ${t.playerModal.noTg}`}
            </span>
            {tagCopied !== 'idle' && (
              <span className="tag-copy-hint">
                {tagCopied === 'done' ? t.playerModal.tagCopied : t.playerModal.tagCopyFailed}
              </span>
            )}
            {roleName && (
              <span className={`role-badge role-${p.role}`}>
                {ROLE_ICON[p.role!]} {roleName}
              </span>
            )}
          </div>
          <button className="modal-close" onClick={close} aria-label={t.playerModal.close}>✕</button>
        </div>

        <div className={`modal-status ${meta.cls}`}>
          {meta.icon} {meta.label} · {t.playerModal.decksToday} <strong>{p.decksUsedToday}/4</strong>
        </div>

        {!isMe && respect !== 'loading' && (
          <button
            className={`btn-respect ${respect === 'idle' ? '' : 'btn-respect-done'}`}
            onClick={sendRespect}
            disabled={respect !== 'idle'}
          >
            {respect === 'idle' && <>👏 {t.respect.give}</>}
            {respect === 'sent' && <>✅ {t.respect.sent}</>}
            {respect === 'used' && <>👏 {t.respect.usedToday}</>}
          </button>
        )}

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
