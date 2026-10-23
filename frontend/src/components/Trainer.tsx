import { useEffect, useMemo, useState } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { haptic, hapticNotify } from '../lib/telegram'
import { openTip } from '../lib/tipSheet'
import { useCardArt } from '../lib/cardArt'
import {
  CARDS, CARD_NAMES, FREE_PUZZLES, PUZZLES, PUZZLE_TEXT, REASONS, canPlace, dailyIndex, loadStars, loadStreak,
  markDailySolved, saveStars, snap, takePendingPuzzle, type Puzzle, type Verdict,
} from '../lib/trainer'
import { ArenaAnim, type Ghost, type Scene } from './ArenaAnim'
import { GameIcon } from './GameIcon'
import { Icon } from './ui/Icon'

/**
 * Тренажёр «Задефай атаку». Список задач со звёздами, задача дня с серией и сама
 * игра. Задача дня и первые три - бесплатно, остальные - в Плюсе.
 */
export function TrainerView() {
  const { t, lang } = useT()
  const s = t.trainer
  const [unlocked, setUnlocked] = useState(false)
  const [stars, setStars] = useState(loadStars)
  const daily = dailyIndex()
  // Открыли из вывода профиля - сразу на задаче той же темы (если она доступна)
  const [current, setCurrent] = useState<number | null>(() => {
    const id = takePendingPuzzle()
    const i = id ? PUZZLES.findIndex(p => p.id === id) : -1
    return i >= 0 && (i < FREE_PUZZLES || i === daily) ? i : null
  })
  const streak = loadStreak()
  const openPlus = usePlusSheet()

  useEffect(() => {
    const load = () => api.getPlus().then(p => setUnlocked(p.unlocked)).catch(() => { /* без статуса - как без Плюса */ })
    load()
    window.addEventListener(PLUS_CHANGED, load)
    return () => window.removeEventListener(PLUS_CHANGED, load)
  }, [])

  const isOpen = (i: number) => unlocked || i < FREE_PUZZLES || i === daily

  if (current !== null) {
    const next = () => {
      const after = PUZZLES.findIndex((_, i) => i > current && isOpen(i))
      setCurrent(after >= 0 ? after : null)
    }
    return (
      <TrainerGame key={current} puzzle={PUZZLES[current]} daily={current === daily}
        onExit={() => setCurrent(null)} onNext={next}
        onStars={n => {
          setStars(saveStars(PUZZLES[current].id, n))
          if (current === daily && n > 0) markDailySolved()
        }} />
    )
  }

  const lockedCount = PUZZLES.filter((_, i) => !isOpen(i)).length
  const totalStars = Object.values(stars).reduce((a, b) => a + b, 0)

  return (
    <div className="tr fade-in">
      <div className="tr-hero">
        <div>
          <h2>{s.title}</h2>
          <p>{s.lead}</p>
        </div>
        <div className="tr-hero-stats">
          <span><GameIcon name="crown" size={20} /> {totalStars}/{PUZZLES.length * 3}</span>
          {streak.days > 0 && <span><GameIcon name="flame" size={20} /> {s.streak.replace('{n}', String(streak.days))}</span>}
        </div>
      </div>

      <button className="tr-daily" onClick={() => { haptic('medium'); setCurrent(daily) }}>
        <TargetArt card={PUZZLES[daily].enemyCard} />
        <div>
          <small>{s.daily}</small>
          <b>{PUZZLE_TEXT[lang][PUZZLES[daily].id]?.title ?? ''}</b>
          <Stars n={stars[PUZZLES[daily].id] ?? 0} />
        </div>
        <span className="tr-play"><GameIcon name="play" size={34} /></span>
      </button>

      <div className="tr-grid">
        {PUZZLES.map((p, i) => {
          const ok = isOpen(i)
          return (
            <button key={p.id} className={`tr-tile ${ok ? '' : 'tr-locked'}`}
              onClick={() => { haptic('light'); if (ok) setCurrent(i); else openPlus('plus') }}>
              <TargetArt card={p.enemyCard} />
              <b>{PUZZLE_TEXT[lang][p.id]?.title}</b>
              {ok ? <Stars n={stars[p.id] ?? 0} /> : <span className="tr-lock"><GameIcon name="lock" size={16} /> {s.locked}</span>}
            </button>
          )
        })}
      </div>
      {lockedCount > 0 && (
        <button className="pd-cta" onClick={() => { haptic('medium'); openPlus('plus') }}>
          <GameIcon name="gem" size={20} /> {s.unlock.replace('{n}', String(lockedCount))}
        </button>
      )}
    </div>
  )
}

function TargetArt({ card }: { card: string }) {
  const art = useCardArt()
  return <span className="tr-art">{art[card] ? <img src={art[card]} alt="" /> : <GameIcon name="swords" size={26} />}</span>
}

function Stars({ n }: { n: number }) {
  return (
    <span className="tr-stars">
      {[0, 1, 2].map(i => <span key={i} className={i < n ? 'tr-star-on' : ''}>★</span>)}
    </span>
  )
}

type Phase = 'intro' | 'choose' | 'outcome' | 'result'

function TrainerGame({ puzzle, daily, onExit, onNext, onStars }: {
  puzzle: Puzzle; daily: boolean; onExit: () => void; onNext: () => void; onStars: (n: number) => void
}) {
  const { t, lang } = useT()
  const s = t.trainer
  const n = (card: string) => CARD_NAMES[lang][card] ?? card
  const text = PUZZLE_TEXT[lang][puzzle.id]
  const art = useCardArt()
  const [phase, setPhase] = useState<Phase>('intro')
  const [round, setRound] = useState(0)
  const [card, setCard] = useState<string | null>(null)
  const [ghost, setGhost] = useState<Ghost | null>(null)
  const [hint, setHint] = useState<string | null>(null)
  const [verdict, setVerdict] = useState<Verdict | null>(null)
  const [outcome, setOutcome] = useState<Scene | null>(null)

  const intro: Scene = useMemo(() => ({ duration: puzzle.decision, actors: puzzle.intro(n) }),
    [puzzle, lang, round])

  const reset = () => {
    haptic('light')
    setPhase('intro'); setCard(null); setGhost(null); setHint(null); setVerdict(null); setOutcome(null)
    setRound(r => r + 1)
  }

  const tap = (x: number, y: number) => {
    if (phase !== 'choose') return
    if (!card) { setHint(s.pickCard); haptic('light'); return }
    if (!canPlace(card, y)) { setHint(s.ownSide); hapticNotify('error'); setGhost({ ...snap(card, x, y), card, kind: CARDS[card].kind, r: CARDS[card].r, ok: false }); return }
    const p = snap(card, x, y)
    const v = puzzle.grade(card, p.x, p.y)
    const o = puzzle.outcome(card, p.x, p.y, v, n)
    setGhost(null)
    setVerdict(v)
    setOutcome({ duration: o.duration, actors: o.actors })
    setPhase('outcome')
    haptic('medium')
  }

  const finish = () => {
    setPhase('result')
    const stars = verdict?.grade === 'perfect' ? 3 : verdict?.grade === 'ok' ? 2 : 0
    onStars(stars)
    hapticNotify(stars > 0 ? 'success' : 'error')
  }

  const stars = verdict?.grade === 'perfect' ? 3 : verdict?.grade === 'ok' ? 2 : 0

  return (
    <div className="tr-game fade-in">
      <div className="tr-top">
        <button className="btn-mini" onClick={() => { haptic('light'); onExit() }}><Icon name="chevronLeft" size={15} /> {s.back}</button>
        {daily && <span className="tr-badge">{s.daily}</span>}
      </div>
      <h2 className="tr-title">{text.title}</h2>
      <p className="tr-task">{text.task}</p>

      <div className={`tr-board ${phase === 'choose' ? 'tr-board-live' : ''}`}>
        {phase === 'intro' && <ArenaAnim key={`i${round}`} scene={intro} loop={false} onEnd={() => setPhase('choose')} />}
        {phase === 'choose' && <ArenaAnim scene={intro} at={puzzle.decision} onTap={tap} ghost={ghost} />}
        {(phase === 'outcome' || phase === 'result') && outcome && (
          <ArenaAnim key={`o${round}`} scene={outcome} loop={false} at={phase === 'result' ? outcome.duration : undefined} onEnd={finish} />
        )}
        {phase === 'choose' && <div className="tr-turn">{s.yourTurn}</div>}
      </div>

      <div className="tr-elixir">
        <GameIcon name="drop" size={18} />
        <div className="tr-elixir-bar">
          {Array.from({ length: 10 }, (_, i) => <i key={i} className={i < puzzle.elixir ? 'on' : ''} />)}
        </div>
        <b>{puzzle.elixir}</b>
      </div>

      <div className="tr-hand">
        {puzzle.hand.map(c => {
          const info = CARDS[c]
          const tooDear = info.cost > puzzle.elixir
          return (
            <button key={c} className={`tr-card ${card === c ? 'tr-card-on' : ''}`} disabled={phase !== 'choose' || tooDear}
              onClick={() => { haptic('light'); setCard(c); setHint(info.spell ? s.pickSpell : s.pickTile) }}>
              <span className="tr-card-art">{art[c] ? <img src={art[c]} alt="" /> : <GameIcon name="cards" size={28} />}</span>
              <span className="tr-cost">{info.cost}</span>
              <small>{n(c)}</small>
            </button>
          )
        })}
      </div>

      {phase === 'choose' && <p className="tr-hint">{hint ?? s.pickCard}</p>}

      {phase === 'result' && verdict && (
        <div className={`tr-result tr-result-${verdict.grade}`}>
          <div className="tr-result-head">
            <Stars n={stars} />
            <b>{verdict.grade === 'perfect' ? s.perfect : verdict.grade === 'ok' ? s.ok : s.fail}</b>
          </div>
          <p>{REASONS[lang][verdict.reason] ?? ''}</p>
          <div className="tr-result-btns">
            <button className="btn btn-ghost" onClick={reset}><Icon name="refresh" size={15} /> {s.retry}</button>
            <button className="btn btn-ghost" onClick={() => openTip(puzzle.tip)}><GameIcon name="play" size={16} /> {s.how}</button>
            <button className="btn" onClick={() => { haptic('medium'); onNext() }}>{s.next} <Icon name="chevronRight" size={15} /></button>
          </div>
        </div>
      )}
    </div>
  )
}
