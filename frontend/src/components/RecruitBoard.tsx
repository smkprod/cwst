import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { RecruitmentCandidate } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { usePlayerSheet } from '../lib/playerSheet'

type BoardState = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; candidates: RecruitmentCandidate[] }

export function RecruitBoard() {
  const [boardState, setBoardState] = useState<BoardState>({ kind: 'loading' })
  const { t } = useT()

  useEffect(() => {
    api.getRecruitmentCandidates()
      .then(r => setBoardState({ kind: 'ready', candidates: r.candidates }))
      .catch(() => setBoardState({ kind: 'error' }))
  }, [])

  const openSheet = usePlayerSheet()
  const openProfile = (candidate: RecruitmentCandidate) => openSheet(candidate.playerTag)

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
