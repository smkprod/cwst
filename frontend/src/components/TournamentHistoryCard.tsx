import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { PlayerTournamentHistory } from '../types'
import { useT } from '../lib/i18n'
import { SectionHead } from './ui/Section'

interface Props {
  playerTag: string
}

type State = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; items: PlayerTournamentHistory[] }

export function TournamentHistoryCard({ playerTag }: Props) {
  const { t } = useT()
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    api.getPlayerTournamentHistory(playerTag)
      .then(items => setState({ kind: 'ready', items }))
      .catch(() => setState({ kind: 'error' }))
  }, [playerTag])

  if (state.kind === 'loading') {
    return (
      <div className="card">
        <SectionHead icon="trophy" tone="gold" title={t.tournament.historyTitle} />
        <div className="center" style={{ padding: 16 }}><div className="spinner" /></div>
      </div>
    )
  }
  if (state.kind === 'error' || state.items.length === 0) {
    return (
      <div className="card">
        <SectionHead icon="trophy" tone="gold" title={t.tournament.historyTitle} />
        <p className="muted small" style={{ margin: 0 }}>{t.tournament.historyEmpty}</p>
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

  return (
    <div className="card">
      <SectionHead icon="trophy" tone="gold" title={t.tournament.historyTitle} />
      <ul className="tournament-history-list">
        {state.items.map(h => (
          <li key={h.tournamentId} className="tournament-history-row">
            <div className="tournament-history-info">
              <span className="tournament-history-name">{h.tournamentName}</span>
              <span className="muted small">{statusLabel(h.status)} · {h.participantCount} {t.tournament.participantsCount}</span>
            </div>
            {h.finalPlacement && (
              h.finalPlacement <= 3
                ? <span className={`mx-medal mx-medal-${h.finalPlacement}`} title={`${t.tournament.place} ${h.finalPlacement}`}>{h.finalPlacement}</span>
                : <span className="tournament-history-place">{t.tournament.place} {h.finalPlacement}</span>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
