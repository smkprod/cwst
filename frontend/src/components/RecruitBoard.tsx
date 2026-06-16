import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { RecruitmentCandidate } from '../types'
import { fmt } from '../lib/format'
import { haptic, openExternalLink } from '../lib/telegram'
import { useT } from '../lib/i18n'

type State = { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; candidates: RecruitmentCandidate[] }

export function RecruitBoard() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const { t } = useT()

  useEffect(() => {
    api.getRecruitmentCandidates()
      .then(r => setState({ kind: 'ready', candidates: r.candidates }))
      .catch(() => setState({ kind: 'error' }))
  }, [])

  const openTg = (candidate: RecruitmentCandidate) => {
    haptic('light')
    openExternalLink(`tg://user?id=${candidate.telegramUserId}`)
  }

  if (state.kind === 'loading') return <div className="center"><div className="spinner" /></div>
  if (state.kind === 'error') return <p className="center muted">{t.recruit.error}</p>

  return (
    <div>
      <h2 className="section-title">{t.recruit.boardTitle}</h2>
      <p className="muted small" style={{ marginBottom: 12 }}>{t.recruit.boardHint}</p>

      {state.candidates.length === 0 && (
        <p className="center muted">{t.recruit.empty}</p>
      )}

      <ul className="recruit-list">
        {state.candidates.map(c => (
          <li key={c.playerTag} className="card recruit-candidate" onClick={() => openTg(c)}>
            <div className="recruit-candidate-top">
              <div className="recruit-candidate-info">
                <span className="recruit-candidate-name">{c.name}</span>
                <span className="muted small">{c.playerTag}</span>
                {c.note && <span className="recruit-note-text">{c.note}</span>}
              </div>
              <button className="btn-mini recruit-tg-btn" onClick={e => { e.stopPropagation(); openTg(c) }}>
                {t.recruit.openTg}
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
