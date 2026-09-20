import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Tournament, TournamentMatch, TournamentParticipant } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

interface Props {
  tournament: Tournament
  onUpdated: (t: Tournament) => void
}

/** Соединитель между парой и следующей: путь в пикселях холста. */
interface Link {
  id: number
  d: string
  /** Матч сыгран — по этой линии уже кто-то прошёл дальше. */
  done: boolean
}

/** Длина прямого «носика» у карточки перед тем, как линия уходит в изгиб. */
const STUB = 10

export function TournamentBracket({ tournament, onUpdated }: Props) {
  const { t } = useT()
  const [editingMatch, setEditingMatch] = useState<number | null>(null)
  const [scoreA, setScoreA] = useState(0)
  const [scoreB, setScoreB] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const canvasRef = useRef<HTMLDivElement>(null)
  const cards = useRef(new Map<number, HTMLDivElement>())
  const [links, setLinks] = useState<Link[]>([])

  /**
   * Линии считаем по реальным координатам карточек, а не рисуем псевдоэлементами.
   *
   * Высота карточки непостоянна: у организатора есть кнопка результата, у несыгранной
   * пары строка «ожидание», а при вводе счёта карточка раздувается вдвое. Любая
   * вёрстка, где линия нарисована относительно самой карточки, на этом разъезжается.
   * Замер по DOM попадает точно при любой высоте и любом числе раундов.
   */
  const measure = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const base = canvas.getBoundingClientRect()
    const next: Link[] = []

    for (const m of tournament.matches) {
      if (m.nextMatchId == null) continue
      const from = cards.current.get(m.id)
      const to = cards.current.get(m.nextMatchId)
      if (!from || !to) continue

      const a = from.getBoundingClientRect()
      const b = to.getBoundingClientRect()

      // Холст не прокручивается сам — прокручивается обёртка вокруг него, поэтому
      // его rect едет вместе с содержимым и вычитания достаточно: scrollLeft уже учтён.
      const x1 = a.right - base.left
      const y1 = a.top + a.height / 2 - base.top
      const x2 = b.left - base.left
      const y2 = b.top + b.height / 2 - base.top
      const mid = (x1 + x2) / 2

      next.push({
        id: m.id,
        d: `M ${x1} ${y1} H ${x1 + STUB} C ${mid} ${y1}, ${mid} ${y2}, ${x2 - STUB} ${y2} H ${x2}`,
        done: m.status === 'completed',
      })
    }

    setLinks(next)
  }, [tournament.matches])

  // Пересчитываем сразу после раскладки: между отрисовкой и замером не должно быть
  // кадра, иначе линии моргнут на старых местах.
  useLayoutEffect(measure, [measure, editingMatch])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    // Меняется всё: ширина экрана, высота карточки при вводе счёта, шрифт после
    // подгрузки. Наблюдаем за холстом целиком, а не гадаем, что именно поехало.
    const ro = new ResizeObserver(measure)
    ro.observe(canvas)
    for (const card of cards.current.values()) ro.observe(card)
    return () => ro.disconnect()
  }, [measure, tournament.matches])

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
      {roundNumbers.length > 2 && <p className="muted small bracket-hint">{t.tournament.bracketScrollHint}</p>}

      {champion && (
        <div className="card tournament-champion-card">
          <span className="tournament-champion-title">{t.tournament.champion}</span>
          <span className="tournament-champion-name">🏆 {champion.teamName ?? champion.playerName}</span>
        </div>
      )}

      <div className="bracket-scroll">
        <div className="bracket-canvas" ref={canvasRef}>
          {/* Линии лежат под карточками: пересечение с углом карточки не должно
              перечёркивать название. */}
          <svg className="bracket-links" aria-hidden="true">
            {links.map(l => (
              <path key={l.id} d={l.d} className={`bracket-link ${l.done ? 'bracket-link-done' : ''}`} />
            ))}
          </svg>

          {roundNumbers.map(round => (
            <div key={round} className="bracket-round">
              <div className="bracket-round-label">
                {round === maxRound ? t.tournament.finalLabel : `${t.tournament.roundLabel} ${round}`}
              </div>
              {/* space-around разносит пары так, что следующий раунд встаёт ровно
                  между своими двумя — это и даёт форму сетки, а не столбиков. */}
              <div className="bracket-round-body">
                {rounds.get(round)!.map(m => (
                  <div
                    key={m.id}
                    className={`bmatch ${m.status === 'completed' ? 'bmatch-done' : ''}`
                      + `${round === maxRound ? ' bmatch-final' : ''}`
                      + `${m.participantA?.isMe || m.participantB?.isMe ? ' bmatch-mine' : ''}`}
                    ref={el => { if (el) cards.current.set(m.id, el); else cards.current.delete(m.id) }}
                  >
                    <Side
                      participant={m.participantA}
                      isWinner={!!m.winner && m.winner.id === m.participantA?.id}
                      isLoser={!!m.winner && !!m.participantA && m.winner.id !== m.participantA.id}
                      score={m.scoreA}
                      showScore={m.status === 'completed'}
                    />
                    <Side
                      participant={m.participantB}
                      isWinner={!!m.winner && m.winner.id === m.participantB?.id}
                      isLoser={!!m.winner && !!m.participantB && m.winner.id !== m.participantB.id}
                      score={m.scoreB}
                      showScore={m.status === 'completed'}
                    />

                    {m.status === 'bye' && <p className="bmatch-note">{t.tournament.bye}</p>}
                    {m.status === 'pending' && <p className="bmatch-note">{t.tournament.pendingStatus}</p>}
                    {/* Отметка «засчитано ботом» нужна не ради красоты: организатор должен
                        видеть, что счёт взялся из лога, и при желании его поправить. */}
                    {m.autoResolved && (
                      <p className="bmatch-note bmatch-auto" title={t.tournament.autoResolvedHint}>
                        🤖 {t.tournament.autoResolved}
                      </p>
                    )}

                    {tournament.isCreator && (m.status === 'ready' || m.status === 'completed') && editingMatch !== m.id && (
                      <button className="bmatch-btn" onClick={() => startEdit(m)}>
                        {m.status === 'completed' ? t.tournament.editResultBtn : t.tournament.setResultBtn}
                      </button>
                    )}

                    {editingMatch === m.id && (
                      <div className="bmatch-edit">
                        <div className="bmatch-score-inputs">
                          <input
                            className="bmatch-score-input"
                            type="number"
                            min={0}
                            max={tournament.bestOf}
                            value={scoreA}
                            onChange={e => setScoreA(Number(e.target.value))}
                          />
                          <span className="bmatch-score-colon">:</span>
                          <input
                            className="bmatch-score-input"
                            type="number"
                            min={0}
                            max={tournament.bestOf}
                            value={scoreB}
                            onChange={e => setScoreB(Number(e.target.value))}
                          />
                        </div>
                        {error && <p className="form-error small">{error}</p>}
                        <div className="bmatch-edit-actions">
                          <button className="bmatch-btn bmatch-btn-primary" disabled={busy} onClick={() => saveResult(m.id)}>
                            {busy ? t.tournament.saving : t.tournament.update}
                          </button>
                          <button className="bmatch-btn" disabled={busy} onClick={cancelEdit}>
                            {t.tournament.cancelForm}
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

/**
 * Одна сторона пары. Пустой слот рисуется пунктиром, а не прочерком-текстом:
 * так сразу видно, что место ещё ждёт победителя, а не что участник выбыл.
 */
function Side({ participant, isWinner, isLoser, score, showScore }: {
  participant: TournamentParticipant | null
  isWinner: boolean
  isLoser: boolean
  score: number
  showScore: boolean
}) {
  if (!participant) {
    return (
      <div className="bside bside-empty">
        <span className="bside-slot" />
      </div>
    )
  }

  return (
    <div className={`bside ${isWinner ? 'bside-win' : ''} ${isLoser ? 'bside-lost' : ''}`
      + `${participant.isMe ? ' bside-mine' : ''}`}>
      <span className="bside-seed">{participant.seed}</span>
      {/* В парном турнире в сетке стоит команда: её имя короче двух ников и не рвёт вёрстку */}
      <span className="bside-name">{participant.teamName ?? participant.playerName}</span>
      {showScore && <span className="bside-score">{score}</span>}
    </div>
  )
}
