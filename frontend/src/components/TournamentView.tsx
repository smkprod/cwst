import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TournamentSummary } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { TournamentForm } from './TournamentForm'
import { TournamentDetail } from './TournamentDetail'
import { GameTournamentView } from './GameTournamentView'

type View =
  | { kind: 'list' }
  | { kind: 'create' }
  | { kind: 'detail'; id: number }

type ListState = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; tournaments: TournamentSummary[] }

export function TournamentView() {
  const { t } = useT()
  const [subTab, setSubTab] = useState<'clan' | 'game'>('clan')
  const [view, setView] = useState<View>({ kind: 'list' })
  const [listState, setListState] = useState<ListState>({ kind: 'loading' })

  const loadList = () => {
    setListState({ kind: 'loading' })
    api.getTournaments()
      .then(tournaments => setListState({ kind: 'ready', tournaments }))
      .catch(() => setListState({ kind: 'error' }))
  }

  useEffect(() => {
    if (view.kind === 'list') loadList()
  }, [view.kind])

  const openCreate = () => {
    haptic('light')
    setView({ kind: 'create' })
  }

  const openDetail = (id: number) => {
    haptic('light')
    setView({ kind: 'detail', id })
  }

  const backToList = () => {
    haptic('light')
    setView({ kind: 'list' })
  }

  const toggle = (
    <div className="gt-toggle">
      <button className={`gt-toggle-btn ${subTab === 'clan' ? 'gt-toggle-on' : ''}`}
        onClick={() => { haptic('light'); setSubTab('clan'); setView({ kind: 'list' }) }}>
        {t.tournament.tabClan}
      </button>
      <button className={`gt-toggle-btn ${subTab === 'game' ? 'gt-toggle-on' : ''}`}
        onClick={() => { haptic('light'); setSubTab('game'); setView({ kind: 'list' }) }}>
        {t.tournament.tabGame}
      </button>
    </div>
  )

  if (subTab === 'game') {
    return <div>{toggle}<GameTournamentView /></div>
  }

  if (view.kind === 'create') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}>← {t.tournament.back}</button>
        <TournamentForm mode="create" onSaved={t2 => openDetail(t2.id)} onCancel={backToList} />
      </div>
    )
  }

  if (view.kind === 'detail') {
    return <TournamentDetail tournamentId={view.id} onBack={backToList} onCancelled={backToList} />
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
  const statusClass = (s: string) => {
    switch (s) {
      case 'registrationOpen': return 'tournament-status-open'
      case 'bracketReady': return 'tournament-status-ready'
      case 'inProgress': return 'tournament-status-progress'
      case 'completed': return 'tournament-status-completed'
      case 'cancelled': return 'tournament-status-cancelled'
      default: return ''
    }
  }

  return (
    <div>
      {toggle}
      <div className="card-title-row">
        <h2 className="section-title" style={{ margin: 0 }}>{t.tournament.listTitle}</h2>
      </div>

      <button className="btn tournament-create-btn" onClick={openCreate}>{t.tournament.createBtn}</button>

      {listState.kind === 'loading' && <div className="center"><div className="spinner" /></div>}
      {listState.kind === 'error' && <p className="center muted">{t.tournament.error}</p>}
      {listState.kind === 'ready' && listState.tournaments.length === 0 && (
        <p className="center muted">{t.tournament.empty}</p>
      )}

      {listState.kind === 'ready' && listState.tournaments.length > 0 && (
        <ul className="tournament-list">
          {listState.tournaments.map(tr => (
            <li key={tr.id} className="card tournament-list-item" onClick={() => openDetail(tr.id)}>
              <div className="tournament-list-item-top">
                <span className="tournament-list-item-name">{tr.name}</span>
                <span className={`badge tournament-status-badge ${statusClass(tr.status)}`}>{statusLabel(tr.status)}</span>
              </div>
              <p className="muted small">
                {tr.creatorName} · {t.tournament.bestOfLabel} {tr.bestOf} · {tr.participantCount}/{tr.maxParticipants} {t.tournament.participantsCount}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
