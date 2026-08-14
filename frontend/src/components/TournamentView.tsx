import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TournamentSummary, GameTournament } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { TournamentForm } from './TournamentForm'
import { TournamentDetail } from './TournamentDetail'
import { GameAddForm, GameTournamentDetail, gameStatusInfo } from './GameTournamentView'

type View =
  | { kind: 'list' }
  | { kind: 'chooseType' }   // выбор типа при создании
  | { kind: 'createClan' }
  | { kind: 'addGame' }
  | { kind: 'clanDetail'; id: number }
  | { kind: 'gameDetail'; g: GameTournament }

type ListState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; clan: TournamentSummary[]; game: GameTournament[] }

export function TournamentView() {
  const { t } = useT()
  const [view, setView] = useState<View>({ kind: 'list' })
  const [listState, setListState] = useState<ListState>({ kind: 'loading' })

  const loadList = () => {
    setListState({ kind: 'loading' })
    Promise.all([api.getTournaments(), api.getGameTournaments()])
      .then(([clan, game]) => setListState({ kind: 'ready', clan, game }))
      .catch(() => setListState({ kind: 'error' }))
  }

  useEffect(() => {
    if (view.kind === 'list') loadList()
  }, [view.kind])

  const go = (v: View) => { haptic('light'); setView(v) }
  const backToList = () => go({ kind: 'list' })

  const removeGame = async (id: number) => {
    haptic('medium')
    try { await api.removeGameTournament(id); hapticNotify('success'); backToList() }
    catch { hapticNotify('error') }
  }

  if (view.kind === 'chooseType') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}>← {t.tournament.back}</button>
        <h2 className="section-title">{t.tournament.chooseTypeTitle}</h2>
        <button className="card tt-choice" onClick={() => go({ kind: 'createClan' })}>
          <span className="tt-choice-emoji">🏆</span>
          <span className="tt-choice-text">
            <span className="tt-choice-name">{t.tournament.typeClan}</span>
            <span className="muted small">{t.tournament.typeClanDesc}</span>
          </span>
        </button>
        <button className="card tt-choice" onClick={() => go({ kind: 'addGame' })}>
          <span className="tt-choice-emoji">🎮</span>
          <span className="tt-choice-text">
            <span className="tt-choice-name">{t.tournament.typeGame}</span>
            <span className="muted small">{t.tournament.typeGameDesc}</span>
          </span>
        </button>
      </div>
    )
  }

  if (view.kind === 'createClan') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}>← {t.tournament.back}</button>
        <TournamentForm mode="create" onSaved={tr => go({ kind: 'clanDetail', id: tr.id })} onCancel={backToList} />
      </div>
    )
  }

  if (view.kind === 'addGame') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}>← {t.tournament.back}</button>
        <GameAddForm onAdded={backToList} onCancel={backToList} />
      </div>
    )
  }

  if (view.kind === 'clanDetail') {
    return <TournamentDetail tournamentId={view.id} onBack={backToList} onCancelled={backToList} />
  }

  if (view.kind === 'gameDetail') {
    return <GameTournamentDetail tournament={view.g} onBack={backToList} onRemove={removeGame} />
  }

  const clanStatusLabel = (s: string) => {
    switch (s) {
      case 'registrationOpen': return t.tournament.statusOpen
      case 'bracketReady': return t.tournament.statusBracketReady
      case 'inProgress': return t.tournament.statusInProgress
      case 'completed': return t.tournament.statusCompleted
      case 'cancelled': return t.tournament.statusCancelled
      default: return s
    }
  }
  const clanStatusClass = (s: string) => {
    switch (s) {
      case 'registrationOpen': return 'tournament-status-open'
      case 'bracketReady': return 'tournament-status-ready'
      case 'inProgress': return 'tournament-status-progress'
      case 'completed': return 'tournament-status-completed'
      case 'cancelled': return 'tournament-status-cancelled'
      default: return ''
    }
  }

  const isEmpty = listState.kind === 'ready' && listState.clan.length === 0 && listState.game.length === 0

  return (
    <div>
      <div className="card-title-row">
        <h2 className="section-title" style={{ margin: 0 }}>{t.tournament.listTitle}</h2>
      </div>

      <button className="btn tournament-create-btn" onClick={() => go({ kind: 'chooseType' })}>
        {t.tournament.createBtn}
      </button>

      {listState.kind === 'loading' && <div className="center"><div className="spinner" /></div>}
      {listState.kind === 'error' && <p className="center muted">{t.tournament.error}</p>}
      {isEmpty && <p className="center muted">{t.tournament.empty}</p>}

      {listState.kind === 'ready' && !isEmpty && (
        <ul className="tournament-list">
          {listState.clan.map(tr => (
            <li key={`c${tr.id}`} className="card tournament-list-item" onClick={() => go({ kind: 'clanDetail', id: tr.id })}>
              <div className="tournament-list-item-top">
                <span className="tournament-list-item-name">🏆 {tr.name}</span>
                <span className={`badge tournament-status-badge ${clanStatusClass(tr.status)}`}>{clanStatusLabel(tr.status)}</span>
              </div>
              <p className="muted small">
                {tr.creatorName} · {t.tournament.bestOfLabel} {tr.bestOf} · {tr.participantCount}/{tr.maxParticipants} {t.tournament.participantsCount}
              </p>
            </li>
          ))}

          {listState.game.map(g => {
            const si = g.live ? gameStatusInfo(g.live.status, t) : null
            return (
              <li key={`g${g.id}`} className="card tournament-list-item" onClick={() => go({ kind: 'gameDetail', g })}>
                <div className="tournament-list-item-top">
                  <span className="tournament-list-item-name">🎮 {g.live?.name ?? g.tournamentTag}</span>
                  {si && <span className={`badge tournament-status-badge ${si.cls}`}>{si.label}</span>}
                </div>
                <p className="muted small">
                  {g.live
                    ? `${g.live.capacity}/${g.live.maxCapacity} · ${t.gameT.levelCap} ${g.live.levelCap} · ${g.tournamentTag}`
                    : `${g.tournamentTag} · ${t.gameT.noLive}`}
                </p>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
