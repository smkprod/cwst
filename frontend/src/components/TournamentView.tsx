import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TournamentSummary, GameTournament } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { TournamentForm } from './TournamentForm'
import { TournamentDetail } from './TournamentDetail'
import { GameAddForm, GameTournamentDetail, gameStatusInfo } from './GameTournamentView'
import { Icon } from './ui/Icon'
import { Chip, IconTile, SectionHead } from './ui/Section'

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
  | { kind: 'ready'; clan: TournamentSummary[]; game: GameTournament[]; past: TournamentSummary[] }

export function TournamentView() {
  const { t } = useT()
  const [view, setView] = useState<View>({ kind: 'list' })
  const [listState, setListState] = useState<ListState>({ kind: 'loading' })

  const loadList = () => {
    setListState({ kind: 'loading' })
    // История грузится вместе со списком, но её отказ не ломает экран: играют
    // в активных турнирах, а прошедшие — приятное дополнение.
    Promise.all([
      api.getTournaments(),
      api.getGameTournaments(),
      api.getTournamentHistory().catch(() => [] as TournamentSummary[]),
    ])
      .then(([clan, game, past]) => setListState({ kind: 'ready', clan, game, past }))
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
        <button className="btn-back" onClick={backToList}><Icon name="chevronLeft" size={15} /> {t.tournament.back}</button>
        <SectionHead icon="plus" tone="violet" title={t.tournament.chooseTypeTitle} className="mx-page-head" />
        <button className="card tt-choice" onClick={() => go({ kind: 'createClan' })}>
          <IconTile name="trophy" tone="gold" size={44} />
          <span className="tt-choice-text">
            <span className="tt-choice-name">{t.tournament.typeClan}</span>
            <span className="muted small">{t.tournament.typeClanDesc}</span>
          </span>
          <Icon name="chevronRight" size={18} className="mx-choice-chev" />
        </button>
        <button className="card tt-choice" onClick={() => go({ kind: 'addGame' })}>
          <IconTile name="dice" tone="blue" size={44} />
          <span className="tt-choice-text">
            <span className="tt-choice-name">{t.tournament.typeGame}</span>
            <span className="muted small">{t.tournament.typeGameDesc}</span>
          </span>
          <Icon name="chevronRight" size={18} className="mx-choice-chev" />
        </button>
      </div>
    )
  }

  if (view.kind === 'createClan') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}><Icon name="chevronLeft" size={15} /> {t.tournament.back}</button>
        <TournamentForm mode="create" onSaved={tr => go({ kind: 'clanDetail', id: tr.id })} onCancel={backToList} />
      </div>
    )
  }

  if (view.kind === 'addGame') {
    return (
      <div>
        <button className="btn-back" onClick={backToList}><Icon name="chevronLeft" size={15} /> {t.tournament.back}</button>
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
  const past = listState.kind === 'ready' ? listState.past : []

  return (
    <div>
      <SectionHead icon="trophy" tone="gold" title={t.tournament.listTitle} className="mx-page-head" />

      <button className="btn tournament-create-btn" onClick={() => go({ kind: 'chooseType' })}>
        <Icon name="plus" size={16} /> {t.tournament.createBtn}
      </button>

      {listState.kind === 'loading' && <div className="center"><div className="spinner" /></div>}
      {listState.kind === 'error' && <p className="center muted">{t.tournament.error}</p>}
      {isEmpty && <p className="center muted">{t.tournament.empty}</p>}

      {listState.kind === 'ready' && !isEmpty && (
        <ul className="tournament-list">
          {listState.clan.map(tr => (
            <li key={`c${tr.id}`} className="card tournament-list-item" onClick={() => go({ kind: 'clanDetail', id: tr.id })}>
              <div className="tournament-list-item-top">
                <span className="tournament-list-item-name mx-ti-name"><Icon name="trophy" size={16} className="mx-ic-gold" /> {tr.name}</span>
                <span className={`badge tournament-status-badge ${clanStatusClass(tr.status)}`}>{clanStatusLabel(tr.status)}</span>
              </div>
              <p className="muted small">
                {tr.mode === 'duo' && <>{t.tournament.formatDuo} · </>}
                {tr.creatorName} · {t.tournament.bestOfLabel} {tr.bestOf} · {tr.participantCount}/{tr.maxParticipants} {t.tournament.participantsCount}
                {tr.startsAtUtc !== null && <> · <Icon name="calendar" size={12} /> {new Date(tr.startsAtUtc).toLocaleString()}</>}
              </p>
              {/* Режим — условие участия: в драфт-турнир идут не с той колодой, что в обычный */}
              {(tr.streamer || tr.gameMode) && (
                <div className="st-mode-line">
                  {tr.streamer && <Chip icon="rocket" tone="orange">{t.tournament.streamerBadge}</Chip>}
                  {tr.gameMode && <Chip icon="swords" tone="violet">{t.duel.modes[tr.gameMode] ?? tr.gameMode}</Chip>}
                </div>
              )}
            </li>
          ))}

          {listState.game.map(g => {
            const si = g.live ? gameStatusInfo(g.live.status, t) : null
            return (
              <li key={`g${g.id}`} className="card tournament-list-item" onClick={() => go({ kind: 'gameDetail', g })}>
                <div className="tournament-list-item-top">
                  <span className="tournament-list-item-name mx-ti-name"><Icon name="dice" size={16} className="mx-ic-blue" /> {g.live?.name ?? g.tournamentTag}</span>
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

      {past.length > 0 && (
        <>
          <SectionHead icon="history" tone="gray" title={t.tournament.pastTitle} className="tournament-history-title mx-page-head" />
          <ul className="tournament-list">
            {past.map(tr => (
              <li
                key={`h${tr.id}`}
                className="card tournament-list-item tournament-past"
                onClick={() => go({ kind: 'clanDetail', id: tr.id })}
              >
                <div className="tournament-list-item-top">
                  <span className="tournament-list-item-name mx-ti-name"><Icon name="trophy" size={16} className="mx-ic-gold" /> {tr.name}</span>
                  {tr.completedAtUtc !== null && (
                    <span className="muted small tournament-past-date">
                      {new Date(tr.completedAtUtc).toLocaleDateString()}
                    </span>
                  )}
                </div>
                {tr.championName !== null && (
                  <p className="tournament-champion-line"><Icon name="crown" size={14} className="mx-ic-gold" /> {tr.championName}</p>
                )}
                <p className="muted small">
                  {tr.mode === 'duo' && <>{t.tournament.formatDuo} · </>}
                  {tr.participantCount} {t.tournament.participantsCount} · {t.tournament.bestOfLabel} {tr.bestOf}
                </p>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
