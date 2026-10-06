import type { Translations } from '../lib/i18n'
import type { Tournament, TournamentMatch, TournamentParticipant } from '../types'
import { Icon } from '../components/ui/Icon'
import { boLabel, champion, isLive, roundName, sideName } from './data'
import { TourHead } from './TourHead'

/*
 * Сетка в пикселях сцены 1280×720. Приложенческую TournamentBracket не берём: она
 * меряет карточки в DOM, потому что их высота там гуляет (кнопки организатора,
 * ввод счёта). Здесь карточки одинаковые, и раскладку можно посчитать заранее —
 * без замеров, без кадра с линиями не на своих местах.
 */
// Панель отступает от краёв сцены на 6px (см. .ov-panel), раскладка — внутри неё
const W = 1280 - 12
const H = 720 - 12
const PAD = 26
const TOP = 112          // шапка с названием турнира
const LABEL_H = 34       // подписи раундов
const BOTTOM = 22
const BODY_H = H - TOP - LABEL_H - BOTTOM
const STUB = 10

export function BracketWidget({ data, t }: { data: Tournament; t: Translations }) {
  const rounds = [...new Set(data.matches.map(m => m.round))].sort((a, b) => a - b)
  const champ = champion(data)

  if (rounds.length === 0) {
    return (
      <div className="ov-panel ov-bracket">
        <TourHead data={data} t={t} />
        <div className="ov-br-empty"><Icon name="hourglass" size={30} /> {t.overlay.registration}</div>
      </div>
    )
  }

  const maxRound = rounds[rounds.length - 1]
  const cols = rounds.length + 1  // последняя колонка — чемпион
  const gap = rounds.length >= 5 ? 24 : 40
  const colW = (W - PAD * 2 - gap * (cols - 1)) / cols

  // Сколько мест в раунде. Считаем от последнего раунда вверх, а не по числу
  // матчей: так пара следующего раунда встаёт ровно посередине своих двух.
  const slotsOf = (ri: number) => Math.max(2 ** (rounds.length - 1 - ri),
    data.matches.filter(m => m.round === rounds[ri]).length)
  const firstSlots = slotsOf(0)
  const cardH = Math.max(18, Math.min(92, BODY_H / firstSlots - (firstSlots > 8 ? 4 : 14)))

  const pos = new Map<number, { x: number; y: number }>()
  rounds.forEach((r, ri) => {
    const list = data.matches.filter(m => m.round === r).sort((a, b) => a.slotIndex - b.slotIndex)
    const slots = slotsOf(ri)
    list.forEach((m, i) => {
      const slot = m.slotIndex < slots ? m.slotIndex : i
      pos.set(m.id, { x: PAD + ri * (colW + gap), y: TOP + LABEL_H + (slot + 0.5) * BODY_H / slots })
    })
  })

  const final = data.matches.find(m => m.round === maxRound) ?? null
  const finalPos = final ? pos.get(final.id) : undefined
  const champX = PAD + rounds.length * (colW + gap)

  const links = data.matches.flatMap(m => {
    const a = pos.get(m.id)
    const b = m.nextMatchId != null ? pos.get(m.nextMatchId) : undefined
    if (!a || !b) return []
    const x1 = a.x + colW, x2 = b.x, mid = (x1 + x2) / 2
    return [{
      id: m.id,
      d: `M ${x1} ${a.y} H ${x1 + STUB} C ${mid} ${a.y}, ${mid} ${b.y}, ${x2 - STUB} ${b.y} H ${x2}`,
      done: m.status === 'completed',
    }]
  })

  return (
    <div className="ov-panel ov-bracket" style={{ ['--card-h' as string]: `${cardH}px` }}>
      <TourHead data={data} t={t} />

      <svg className="ov-br-links" width={W} height={H} aria-hidden="true">
        <defs>
          <linearGradient id="ov-link-done" x1="0" x2="1">
            <stop offset="0" stopColor="#9c6bff" />
            <stop offset="1" stopColor="#3fc6f2" />
          </linearGradient>
        </defs>
        {links.map(l => (
          <path key={l.id} d={l.d} className={`ov-br-link ${l.done ? 'ov-br-link-done' : ''}`} />
        ))}
        {finalPos && (
          <path d={`M ${finalPos.x + colW} ${finalPos.y} H ${champX}`}
            className={`ov-br-link ${champ ? 'ov-br-link-champ' : ''}`} />
        )}
      </svg>

      {rounds.map((r, ri) => (
        <div key={r} className={`ov-br-label ${r === maxRound ? 'ov-br-label-final' : ''}`}
          style={{ left: PAD + ri * (colW + gap), top: TOP, width: colW }}>
          {roundName(r, maxRound, t)}
          {r === maxRound && data.finalBestOf != null && <span className="ov-br-label-bo">{boLabel(data.finalBestOf)}</span>}
        </div>
      ))}

      {data.matches.map(m => {
        const p = pos.get(m.id)
        if (!p) return null
        // Готовые к игре пары подсвечены все: зритель видит, какие матчи сейчас
        // в работе, а начатые (со счётом) горят отдельно — пульсом
        return <MatchCard key={m.id} m={m} x={p.x} y={p.y} w={colW} final={m.round === maxRound}
          next={m.status === 'ready' && !!m.participantA && !!m.participantB && !isLive(m)} />
      })}

      {finalPos && (
        <div className={`ov-br-champ ${champ ? 'ov-br-champ-on' : ''}`}
          style={{ left: champX, top: finalPos.y, width: colW }}>
          <span className="ov-br-champ-ic"><Icon name="trophy" size={34} /></span>
          <span className="ov-br-champ-label">{t.overlay.champion}</span>
          <span className="ov-br-champ-name">{champ ? sideName(champ) : '?'}</span>
        </div>
      )}
    </div>
  )
}

function MatchCard({ m, x, y, w, final, next }: {
  m: TournamentMatch; x: number; y: number; w: number; final: boolean; next: boolean
}) {
  const live = isLive(m)
  const showScore = m.status === 'completed' || live
  const cls = [
    'ov-bm',
    m.status === 'completed' && 'ov-bm-done',
    m.status === 'bye' && 'ov-bm-bye',
    live && 'ov-bm-live',
    next && 'ov-bm-next',
    final && 'ov-bm-final',
  ].filter(Boolean).join(' ')
  return (
    <div className={cls} style={{ left: x, top: y, width: w }}>
      <Side p={m.participantA} score={m.scoreA} show={showScore} win={!!m.winner && m.winner.id === m.participantA?.id}
        lose={!!m.winner && !!m.participantA && m.winner.id !== m.participantA.id} />
      <Side p={m.participantB} score={m.scoreB} show={showScore} win={!!m.winner && m.winner.id === m.participantB?.id}
        lose={!!m.winner && !!m.participantB && m.winner.id !== m.participantB.id} />
      {live && <span className="ov-bm-live-dot" />}
    </div>
  )
}

function Side({ p, score, show, win, lose }: {
  p: TournamentParticipant | null; score: number; show: boolean; win: boolean; lose: boolean
}) {
  if (!p) return <div className="ov-bs ov-bs-empty"><span className="ov-bs-slot" /></div>
  return (
    <div className={`ov-bs ${win ? 'ov-bs-win' : ''} ${lose ? 'ov-bs-lose' : ''}`}>
      <span className="ov-bs-seed">{p.seed}</span>
      <span className="ov-bs-name">{sideName(p)}</span>
      {/* key по счёту: новое число монтируется заново и «выстреливает» анимацией */}
      {show && <span key={score} className="ov-bs-score ov-pop">{score}</span>}
    </div>
  )
}
