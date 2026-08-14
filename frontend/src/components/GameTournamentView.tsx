import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { GameTournament } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

export function gameStatusInfo(status: string, t: Translations): { label: string; cls: string } {
  switch (status) {
    case 'IN_PREPARATION': return { label: t.gameT.statusPrep, cls: 'tournament-status-open' }
    case 'IN_PROGRESS': return { label: t.gameT.statusProgress, cls: 'tournament-status-progress' }
    case 'ENDED': return { label: t.gameT.statusEnded, cls: 'tournament-status-completed' }
    default: return { label: t.gameT.statusUnknown, cls: '' }
  }
}

function fmtCountdown(sec: number): string {
  if (sec <= 0) return '0м'
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  return h > 0 ? `${h}ч ${m}м` : `${m}м`
}

/** Форма добавления игрового турнира по тегу. */
export function GameAddForm({ onAdded, onCancel }: { onAdded: () => void; onCancel: () => void }) {
  const { t } = useT()
  const [tag, setTag] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.addGameTournament(tag.trim(), password.trim() || undefined)
      hapticNotify('success')
      onAdded()
    } catch (e) {
      hapticNotify('error')
      setError(
        e instanceof ApiError && e.code === 'tournament_not_found' ? t.gameT.errNotFound
          : e instanceof ApiError && e.code === 'already_tracked' ? t.gameT.errTracked
          : e instanceof ApiError && (e.code === 'cr_api_unavailable' || e.code === 'cr_api_token_invalid') ? t.gameT.errApi
          : t.gameT.errGeneric,
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card" style={{ marginTop: 10 }}>
      <div className="cards-section-title">{t.gameT.addTitle}</div>
      <input className="owner-bc-text" style={{ marginBottom: 8 }} placeholder={t.gameT.tagPlaceholder}
        value={tag} onChange={e => { setTag(e.target.value); setError(null) }} />
      <input className="owner-bc-text" style={{ marginBottom: 8 }} placeholder={t.gameT.passwordPlaceholder}
        value={password} onChange={e => setPassword(e.target.value)} />
      <p className="muted small" style={{ margin: '0 0 10px' }}>{t.gameT.tagHint}</p>
      <div style={{ display: 'flex', gap: 6 }}>
        <button className="btn btn-nudge" style={{ flex: 1 }} disabled={busy || tag.trim().length === 0} onClick={submit}>
          {busy ? t.gameT.adding : t.gameT.add}
        </button>
        <button className="btn-mini" onClick={() => { haptic('light'); onCancel() }}>{t.tournament.back}</button>
      </div>
      {error && <p className="muted small" style={{ marginTop: 8, textAlign: 'center' }}>{error}</p>}
    </div>
  )
}

/** Детальная карточка игрового турнира с живой таблицей. */
export function GameTournamentDetail(
  { tournament, onBack, onRemove }:
  { tournament: GameTournament; onBack: () => void; onRemove: (id: number) => void },
) {
  const { t } = useT()
  const [confirmRemove, setConfirmRemove] = useState(false)
  const g = tournament
  const live = g.live
  const si = live ? gameStatusInfo(live.status, t) : null

  return (
    <div>
      <button className="btn-back" onClick={onBack}>← {t.tournament.back}</button>

      <section className="card" style={{ marginTop: 10 }}>
        <div className="tournament-list-item-top">
          <h2 className="section-title" style={{ margin: 0 }}>{live?.name ?? g.tournamentTag}</h2>
          {si && <span className={`badge tournament-status-badge ${si.cls}`}>{si.label}</span>}
        </div>
        {live?.description && <p className="muted small" style={{ marginTop: 6 }}>{live.description}</p>}

        <div className="gt-join">
          <div><span className="muted small">{t.gameT.tag}</span><span className="gt-code">{g.tournamentTag}</span></div>
          {g.password && <div><span className="muted small">{t.gameT.password}</span><span className="gt-code">{g.password}</span></div>}
        </div>

        {live && (
          <div className="profile-stats" style={{ gridTemplateColumns: 'repeat(2, 1fr)', marginTop: 12 }}>
            <div className="profile-stat">
              <div className="profile-stat-value">🎮 {live.capacity}/{live.maxCapacity}</div>
              <div className="profile-stat-label">{t.gameT.fill}</div>
            </div>
            <div className="profile-stat">
              <div className="profile-stat-value">🃏 {live.levelCap}</div>
              <div className="profile-stat-label">{t.gameT.levelCap}</div>
            </div>
            {live.firstPlaceCardPrize > 0 && (
              <div className="profile-stat">
                <div className="profile-stat-value">🏆 {live.firstPlaceCardPrize}</div>
                <div className="profile-stat-label">{t.gameT.prize}</div>
              </div>
            )}
            {live.startsInSeconds != null && live.startsInSeconds > 0 && (
              <div className="profile-stat">
                <div className="profile-stat-value">⏳ {fmtCountdown(live.startsInSeconds)}</div>
                <div className="profile-stat-label">{t.gameT.startsIn}</div>
              </div>
            )}
            {live.endsInSeconds != null && live.endsInSeconds > 0 && (
              <div className="profile-stat">
                <div className="profile-stat-value">⏳ {fmtCountdown(live.endsInSeconds)}</div>
                <div className="profile-stat-label">{t.gameT.endsIn}</div>
              </div>
            )}
          </div>
        )}
      </section>

      {live && live.members.length > 0 && (
        <section className="card" style={{ marginTop: 10 }}>
          <div className="cards-section-title">{t.gameT.standings} ({live.members.length})</div>
          <ul className="gt-members">
            {live.members.slice(0, 50).map(m => (
              <li key={`${m.rank}-${m.name}`} className="gt-member">
                <span className="gt-member-rank">#{m.rank > 0 ? m.rank : '—'}</span>
                <div className="gt-member-info">
                  <span className="gt-member-name">{m.name}</span>
                  {m.clanName && <span className="muted small">{m.clanName}</span>}
                </div>
                <span className="gt-member-score">{m.score} 🏅</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {!live && <p className="center muted">{t.gameT.noLiveDetail}</p>}

      {g.isCreator && (
        <button
          className="btn-mini btn-mini-danger"
          style={{ marginTop: 12 }}
          onClick={() => { haptic('medium'); confirmRemove ? onRemove(g.id) : setConfirmRemove(true) }}
          onBlur={() => setConfirmRemove(false)}
        >
          {confirmRemove ? t.gameT.confirmRemove : t.gameT.remove}
        </button>
      )}
    </div>
  )
}
