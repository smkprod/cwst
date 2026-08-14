import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { PlayerProfile, RecruitmentCandidate } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { PlayerProfileCard } from './PlayerProfileCard'

type BoardState = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; candidates: RecruitmentCandidate[] }
type View =
  | { kind: 'board' }
  | { kind: 'profile_loading'; playerTag: string }
  | { kind: 'profile'; profile: PlayerProfile }
  | { kind: 'profile_error'; playerTag: string }

export function RecruitBoard() {
  const [boardState, setBoardState] = useState<BoardState>({ kind: 'loading' })
  const [view, setView] = useState<View>({ kind: 'board' })
  const { t } = useT()

  useEffect(() => {
    api.getRecruitmentCandidates()
      .then(r => setBoardState({ kind: 'ready', candidates: r.candidates }))
      .catch(() => setBoardState({ kind: 'error' }))
  }, [])

  const openProfile = async (candidate: RecruitmentCandidate) => {
    haptic('light')
    setView({ kind: 'profile_loading', playerTag: candidate.playerTag })
    try {
      const profile = await api.getPlayerProfile(candidate.playerTag)
      setView({ kind: 'profile', profile })
    } catch (e) {
      if (e instanceof ApiError && e.code === 'player_not_found') {
        setView({ kind: 'profile_error', playerTag: candidate.playerTag })
      } else {
        setView({ kind: 'profile_error', playerTag: candidate.playerTag })
      }
    }
  }

  const backToBoard = () => {
    haptic('light')
    setView({ kind: 'board' })
  }

  if (view.kind === 'profile_loading') {
    return (
      <div>
        <button className="btn-back" onClick={backToBoard}>← {t.recruit.back}</button>
        <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
      </div>
    )
  }

  if (view.kind === 'profile_error') {
    return (
      <div>
        <button className="btn-back" onClick={backToBoard}>← {t.recruit.back}</button>
        <p className="center muted" style={{ marginTop: 24 }}>{t.search.notFound}</p>
      </div>
    )
  }

  if (view.kind === 'profile') {
    return (
      <div>
        <button className="btn-back" onClick={backToBoard}>← {t.recruit.back}</button>
        <PlayerProfileCard profile={view.profile} />
      </div>
    )
  }

  // Board view
  if (boardState.kind === 'loading') return <div className="center"><div className="spinner" /></div>
  if (boardState.kind === 'error') return <p className="center muted">{t.recruit.error}</p>

  return (
    <div>
      <h2 className="section-title">{t.recruit.boardTitle}</h2>
      <p className="muted small" style={{ marginBottom: 12 }}>{t.recruit.boardHint}</p>

      {boardState.candidates.length === 0 && (
        <p className="center muted">{t.recruit.empty}</p>
      )}

      <ul className="recruit-list">
        {boardState.candidates.map(c => (
          <li key={c.playerTag} className="card recruit-candidate" onClick={() => openProfile(c)}>
            <div className="recruit-candidate-top">
              <div className="recruit-candidate-info">
                <span className="recruit-candidate-name">{c.name}</span>
                <span className="muted small">{c.playerTag}</span>
                {c.note && <span className="recruit-note-text">{c.note}</span>}
              </div>
              <button
                className="btn-mini"
                onClick={e => { e.stopPropagation(); openProfile(c) }}
              >
                {t.recruit.viewProfile}
              </button>
            </div>
            <div className="recruit-stats">
              <span className="recruit-stat">
                <strong>{c.weeksPlayed}</strong> <span className="muted small">{t.recruit.weeks}</span>
              </span>
              <span className="recruit-stat">
                <strong>{fmt(c.totalFame)}</strong> <span className="muted small">{t.recruit.medalsTotal}</span>
              </span>
              <span className="recruit-stat">
                <strong>{c.avgFamePerAttack > 0 ? c.avgFamePerAttack.toFixed(0) : '—'}</strong> <span className="muted small">{t.recruit.perAttack}</span>
              </span>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
