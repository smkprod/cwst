import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Tournament } from '../types'
import { haptic, hapticNotify, openExternalLink } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { TournamentForm } from './TournamentForm'
import { TournamentBracket } from './TournamentBracket'

interface Props {
  tournamentId: number
  onBack: () => void
  onCancelled: () => void
}

type LoadState = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; data: Tournament }

export function TournamentDetail({ tournamentId, onBack, onCancelled }: Props) {
  const { t } = useT()
  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    api.getTournament(tournamentId)
      .then(data => setState({ kind: 'ready', data }))
      .catch(() => setState({ kind: 'error' }))
  }, [tournamentId])

  const update = (data: Tournament) => {
    setState({ kind: 'ready', data })
    setActionError(null)
    setConfirmCancel(false)
  }

  const mapError = (code: string) => {
    switch (code) {
      case 'not_open': return t.tournament.notOpen
      case 'full': return t.tournament.full
      case 'already_joined': return t.tournament.alreadyJoined
      case 'not_joined': return t.tournament.notJoined
      case 'already_started': return t.tournament.alreadyStarted
      case 'not_enough_participants': return t.tournament.notEnoughParticipants
      case 'already_playing': return t.tournament.alreadyPlaying
      case 'not_creator': return t.tournament.notCreator
      case 'tournament_not_found': return t.tournament.notFound
      case 'player_not_linked': return t.tournament.notLinked
      default: return t.tournament.error
    }
  }

  const runAction = async (fn: () => Promise<Tournament>) => {
    haptic('medium')
    setBusy(true)
    setActionError(null)
    try {
      const result = await fn()
      hapticNotify('success')
      update(result)
    } catch (e) {
      hapticNotify('error')
      setActionError(e instanceof ApiError ? mapError(e.code) : t.tournament.error)
    } finally {
      setBusy(false)
    }
  }

  const join = () => runAction(() => api.joinTournament(tournamentId))
  const leave = () => runAction(() => api.leaveTournament(tournamentId))
  const generateBracket = () => runAction(() => api.generateTournamentBracket(tournamentId))

  const cancelTournament = async () => {
    if (!confirmCancel) {
      haptic('medium')
      setConfirmCancel(true)
      return
    }
    haptic('medium')
    setBusy(true)
    try {
      await api.cancelTournament(tournamentId)
      hapticNotify('success')
      onCancelled()
    } catch {
      hapticNotify('error')
      setActionError(t.tournament.error)
    } finally {
      setBusy(false)
    }
  }

  if (state.kind === 'loading') {
    return (
      <div>
        <button className="btn-back" onClick={onBack}>← {t.tournament.back}</button>
        <div className="center"><div className="spinner" /></div>
      </div>
    )
  }
  if (state.kind === 'error') {
    return (
      <div>
        <button className="btn-back" onClick={onBack}>← {t.tournament.back}</button>
        <p className="center muted">{t.tournament.notFound}</p>
      </div>
    )
  }

  const d = state.data

  if (editing) {
    return (
      <div>
        <button className="btn-back" onClick={() => setEditing(false)}>← {t.tournament.back}</button>
        <TournamentForm
          mode="edit"
          initial={d}
          onSaved={updated => { update(updated); setEditing(false) }}
          onCancel={() => setEditing(false)}
        />
      </div>
    )
  }

  const statusLabel = (s: string) => {
    switch (s) {
      case 'registrationOpen': return t.tournament.statusOpen
      case 'bracketReady': return t.tournament.statusBracketReady
      case 'inProgress': return t.tournament.statusInProgress
      case 'completed': return t.tournament.statusCompleted
      case 'cancelled': return t.tournament.statusCancelled
      default: return s
    }
  }

  const canEdit = d.isCreator && d.status !== 'cancelled' && d.status !== 'completed'
  const canManageBracket = d.isCreator && (d.status === 'registrationOpen' || d.status === 'bracketReady') && d.participants.length >= 2
  const canLeave = d.isParticipant && !d.isCreator && d.status === 'registrationOpen'

  return (
    <div>
      <button className="btn-back" onClick={onBack}>← {t.tournament.back}</button>

      <div className="card tournament-detail-header">
        <div className="card-title-row">
          <div className="card-title tournament-detail-name">{d.name}</div>
          <span className="badge tournament-status-badge">{statusLabel(d.status)}</span>
        </div>
        <p className="muted small">
          {t.tournament.bestOfLabel} {d.bestOf} · {d.participants.length}/{d.maxParticipants} {t.tournament.participantsCount}
        </p>
        <p className="muted small">{d.creatorName} · {t.tournament.creatorBadge}</p>

        {d.description && (
          <div className="tournament-detail-section">
            <div className="tournament-detail-section-title">{t.tournament.descTitle}</div>
            <p className="small tournament-detail-text">{d.description}</p>
          </div>
        )}
        {d.prizeInfo && (
          <div className="tournament-detail-section">
            <div className="tournament-detail-section-title">{t.tournament.prizeTitle}</div>
            <p className="small tournament-detail-text">{d.prizeInfo}</p>
          </div>
        )}

        {actionError && <p className="form-error small">{actionError}</p>}

        <div className="tournament-detail-actions">
          {d.isParticipant && (
            <button className="btn" disabled={busy} onClick={() => openExternalLink(d.clanInviteLink)}>
              {t.tournament.joinClanBtn}
            </button>
          )}
          {d.canJoin && (
            <button className="btn" disabled={busy} onClick={join}>{t.tournament.joinBtn}</button>
          )}
          {canLeave && (
            <button className="btn-mini btn-mini-danger" disabled={busy} onClick={leave}>{t.tournament.leaveBtn}</button>
          )}
          {canEdit && (
            <button className="btn-mini" disabled={busy} onClick={() => setEditing(true)}>{t.tournament.editBtn}</button>
          )}
          {canManageBracket && (
            <button className="btn-mini" disabled={busy} onClick={generateBracket}>
              {d.matches.length > 0 ? t.tournament.regenerateBracketBtn : t.tournament.generateBracketBtn}
            </button>
          )}
          {canEdit && (
            <button className="btn-mini btn-mini-danger" disabled={busy} onClick={cancelTournament}>
              {confirmCancel ? t.tournament.confirmCancel : t.tournament.cancelTournamentBtn}
            </button>
          )}
        </div>
      </div>

      <div className="tournament-detail-section">
        <div className="tournament-detail-section-title">{t.tournament.participantsTitle}</div>
        <ul className="tournament-participants-list">
          {d.participants.map(p => (
            <li key={p.id} className="tournament-participant-row">
              <span className="tournament-participant-name">{p.playerName}</span>
              {p.finalPlacement && <span className="muted small">#{p.finalPlacement}</span>}
            </li>
          ))}
        </ul>
      </div>

      <TournamentBracket tournament={d} onUpdated={update} />
    </div>
  )
}
