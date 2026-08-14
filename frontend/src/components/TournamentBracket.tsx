import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Tournament, TournamentMatch, TournamentParticipant } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

interface Props {
  tournament: Tournament
  onUpdated: (t: Tournament) => void
}

export function TournamentBracket({ tournament, onUpdated }: Props) {
  const { t } = useT()
  const [editingMatch, setEditingMatch] = useState<number | null>(null)
  const [scoreA, setScoreA] = useState(0)
  const [scoreB, setScoreB] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (tournament.matches.length === 0) return null

  const rounds = new Map<number, TournamentMatch[]>()
  for (const m of tournament.matches) {
    if (!rounds.has(m.round)) rounds.set(m.round, [])
    rounds.get(m.round)!.push(m)
  }
  const roundNumbers = [...rounds.keys()].sort((a, b) => a - b)
  const maxRound = roundNumbers[roundNumbers.length - 1]

  const startEdit = (m: TournamentMatch) => {
    haptic('light')
    setEditingMatch(m.id)
    setScoreA(m.scoreA)
    setScoreB(m.scoreB)
    setError(null)
  }

  const cancelEdit = () => {
    haptic('light')
    setEditingMatch(null)
    setError(null)
  }

  const saveResult = async (matchId: number) => {
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      const result = await api.setTournamentMatchResult(tournament.id, matchId, scoreA, scoreB)
      hapticNotify('success')
      setEditingMatch(null)
      onUpdated(result)
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError && e.code === 'bad_score' ? t.tournament.badScore : t.tournament.error)
    } finally {
      setBusy(false)
    }
  }

  const champion = tournament.status === 'completed'
    ? tournament.participants.find(p => p.finalPlacement === 1)
    : null

  return (
    <div className="tournament-bracket-wrap">
      <h3 className="section-title">{t.tournament.bracketTitle}</h3>

      {champion && (
        <div className="card tournament-champion-card">
          <span className="tournament-champion-title">{t.tournament.champion}</span>
          <span className="tournament-champion-name">🏆 {champion.playerName}</span>
        </div>
      )}

      <div className="tournament-bracket">
        {roundNumbers.map(round => (
          <div key={round} className="tournament-round">
            <div className="tournament-round-label">
              {round === maxRound ? t.tournament.finalLabel : `${t.tournament.roundLabel} ${round}`}
            </div>
            {rounds.get(round)!.map(m => (
              <div key={m.id} className="card tournament-match">
                <MatchSide participant={m.participantA} isWinner={m.winner?.id === m.participantA?.id} score={m.scoreA} status={m.status} />
                <div className="tournament-match-vs">{t.tournament.vs}</div>
                <MatchSide participant={m.participantB} isWinner={m.winner?.id === m.participantB?.id} score={m.scoreB} status={m.status} />

                {m.status === 'bye' && <p className="muted small tournament-bye">{t.tournament.bye}</p>}
                {m.status === 'pending' && <p className="muted small tournament-bye">{t.tournament.pendingStatus}</p>}

                {tournament.isCreator && (m.status === 'ready' || m.status === 'completed') && editingMatch !== m.id && (
                  <button className="btn-mini tournament-result-btn" onClick={() => startEdit(m)}>
                    {m.status === 'completed' ? t.tournament.editResultBtn : t.tournament.setResultBtn}
                  </button>
                )}

                {editingMatch === m.id && (
                  <div className="tournament-score-edit">
                    <label className="muted small">{t.tournament.scoreLabel}</label>
                    <div className="tournament-score-inputs">
                      <input
                        className="search-input tournament-score-input"
                        type="number"
                        min={0}
                        max={tournament.bestOf}
                        value={scoreA}
                        onChange={e => setScoreA(Number(e.target.value))}
                      />
                      <span>:</span>
                      <input
                        className="search-input tournament-score-input"
                        type="number"
                        min={0}
                        max={tournament.bestOf}
                        value={scoreB}
                        onChange={e => setScoreB(Number(e.target.value))}
                      />
                    </div>
                    {error && <p className="form-error small">{error}</p>}
                    <div className="recruit-actions">
                      <button className="btn-mini" disabled={busy} onClick={() => saveResult(m.id)}>
                        {busy ? t.tournament.saving : t.tournament.update}
                      </button>
                      <button className="btn-mini" disabled={busy} onClick={cancelEdit}>{t.tournament.cancelForm}</button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

function MatchSide({ participant, isWinner, score, status }: {
  participant: TournamentParticipant | null
  isWinner: boolean
  score: number
  status: string
}) {
  return (
    <div className={`tournament-match-side ${isWinner ? 'tournament-match-winner' : ''}`}>
      <span className="tournament-match-name">{participant ? participant.playerName : '—'}</span>
      {status === 'completed' && <span className="tournament-match-score">{score}</span>}
    </div>
  )
}
