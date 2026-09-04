import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { DailyPuzzle } from '../types'
import { haptic, hapticNotify, shareToTelegram } from '../lib/telegram'
import { useT } from '../lib/i18n'

/**
 * «Карта дня»: угадай карту Clash Royale по фрагменту арта.
 *
 * Загадка одна на всех и меняется раз в сутки. Общая карта — не деталь, а смысл всей
 * затеи: разные загадки у разных людей не дают повода поговорить, а одна на всех
 * превращает игру в разговор в клановом чате.
 *
 * Картинка грузится по подписанному адресу, где нет уровня приближения — иначе
 * достаточно было бы запросить сразу третий и ответить с первой попытки.
 */
export function DailyPuzzleCard() {
  const [state, setState] = useState<'loading' | 'hidden' | 'ready'>('loading')
  const [game, setGame] = useState<DailyPuzzle | null>(null)
  const [sending, setSending] = useState(false)
  const [wrong, setWrong] = useState<number[]>([])
  const { t } = useT()

  useEffect(() => {
    let alive = true
    api.getDailyPuzzle()
      .then(g => { if (alive) { setGame(g); setState('ready') } })
      // Игра — развлечение: если она недоступна, экран не должен показывать ошибку
      .catch(() => { if (alive) setState('hidden') })
    return () => { alive = false }
  }, [])

  if (state !== 'ready' || game === null) return null

  const answer = async (cardId: number) => {
    if (sending || game.finished) return
    haptic('medium')
    setSending(true)
    try {
      const next = await api.guessPuzzle(cardId)
      // Промах помечаем локально: сервер знает только счётчик попыток, а показать
      // надо именно те кнопки, по которым человек уже стучал.
      if (!next.solved) setWrong(w => [...w, cardId])
      hapticNotify(next.solved ? 'success' : next.finished ? 'error' : 'warning')
      setGame(next)
    } catch {
      hapticNotify('error')
    } finally {
      setSending(false)
    }
  }

  const share = () => {
    haptic('light')
    // Результат без ответа: спойлер убил бы всю затею — остальным должно быть
    // интересно попробовать самим. Квадратики показывают, с какой попытки вышло,
    // и ничего не говорят о самой карте.
    const squares = game.solved
      ? '⬛'.repeat(game.attempt - 1) + '🟩'
      : '⬛'.repeat(game.maxAttempts)
    shareToTelegram(
      `🔍 ${t.puzzle.title} #${game.day}\n` +
      (game.solved
        ? `${squares} ${t.puzzle.shareSolved.replace('{n}', String(game.attempt))}`
        : `${squares} ${t.puzzle.shareFailed}`) +
      (game.streak > 1 ? `\n🔥 ${t.puzzle.shareStreak.replace('{n}', String(game.streak))}` : ''))
  }

  return (
    <div className="card puzzle-card">
      <div className="puzzle-head">
        <span className="adm-block-title">🔍 {t.puzzle.title} #{game.day}</span>
        {game.streak > 0 && <span className="puzzle-streak">🔥 {game.streak}</span>}
      </div>

      {/* Номер попытки в адресе — чтобы после промаха браузер перекачал картинку.
          Уровень приближения сервер всё равно берёт из базы, так что подставить
          сюда тройку и увидеть весь арт нельзя: параметр влияет только на кэш. */}
      <img
        className="puzzle-image"
        src={`/api/img/puzzle/${game.imageToken}.jpg?a=${game.attempt}`}
        alt={t.puzzle.title}
      />

      <p className="puzzle-status">
        {game.finished
          ? game.solved
            ? `✅ ${t.puzzle.solved.replace('{n}', String(game.attempt))} · +${game.points}`
            : `❌ ${t.puzzle.failed}`
          : t.puzzle.attempt
              .replace('{n}', String(game.attempt))
              .replace('{max}', String(game.maxAttempts))}
      </p>

      {game.finished ? (
        <>
          <div className="puzzle-answer">
            {game.answerIconUrl ? <img src={game.answerIconUrl} alt="" /> : null}
            <span>{game.answerName}</span>
          </div>
          <button className="btn-invite" onClick={share}>➤ {t.puzzle.share}</button>
          <p className="invite-note">{t.puzzle.comeBack}</p>
        </>
      ) : (
        <div className="puzzle-options">
          {game.options.map(o => (
            <button
              key={o.cardId}
              className={`puzzle-opt ${wrong.includes(o.cardId) ? 'puzzle-opt-wrong' : ''}`}
              disabled={sending || wrong.includes(o.cardId)}
              onClick={() => answer(o.cardId)}
            >
              {o.name}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
