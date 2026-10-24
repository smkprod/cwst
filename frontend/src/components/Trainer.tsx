import { useEffect, useMemo, useRef, useState } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { haptic, hapticNotify } from '../lib/telegram'
import { openTip } from '../lib/tipSheet'
import { useCardArt } from '../lib/cardArt'
import {
  CARD_NAMES, FREE_PUZZLES, HINTS, PUZZLES, PUZZLE_TEXT, REASONS, RESULT_TEXT, cardInfo, dailyIndex, explain, loadStars,
  loadStreak, markDailySolved, saveStars, snap, takePendingPuzzle, type Puzzle, type Verdict,
} from '../lib/trainer'
import { canDeploy, sideOf, type Entity, type Sim } from '../lib/sim/engine'
import { emojiOf } from '../lib/sim/emoji'
import { Watch, atPause, baseline, introSim, judge, pauseTime, playMine, type Outcome } from '../lib/sim/scenario'
import { ArenaSim, type SimGhost } from './ArenaSim'
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
  return <span className="tr-art">{art[card] ? <img src={art[card]} alt="" /> : <span className="tr-emoji">{emojiOf(card)}</span>}</span>
}

function Stars({ n }: { n: number }) {
  return (
    <span className="tr-stars">
      {[0, 1, 2].map(i => <span key={i} className={i < n ? 'tr-star-on' : ''}>★</span>)}
    </span>
  )
}

type Phase = 'intro' | 'choose' | 'battle' | 'result'

const FAST_KEY = 'cwst_trainer_fast'

function loadFast(): boolean {
  try { return localStorage.getItem(FAST_KEY) === '1' } catch { return false }
}

function TrainerGame({ puzzle, daily, onExit, onNext, onStars }: {
  puzzle: Puzzle; daily: boolean; onExit: () => void; onNext: () => void; onStars: (n: number) => void
}) {
  const { t, lang } = useT()
  const s = t.trainer
  const n = (card: string) => CARD_NAMES[lang][card] ?? card
  const text = PUZZLE_TEXT[lang][puzzle.id]
  const art = useCardArt()
  const sc = puzzle.sc
  const at = useMemo(() => pauseTime(sc), [sc])
  // Тот же бой без защиты - с ним сравниваем урон по башне
  const base = useMemo(() => (puzzle.custom ? 0 : baseline(sc)), [puzzle, sc])
  const [round, setRound] = useState(0)
  const sim = useMemo(() => introSim(sc, at), [sc, at, round])
  const [phase, setPhase] = useState<Phase>('intro')
  const [card, setCard] = useState<string | null>(null)
  const [ghost, setGhost] = useState<SimGhost | null>(null)
  const [hint, setHint] = useState<string | null>(null)
  const [fast, setFast] = useState(loadFast)
  const [outcome, setOutcome] = useState<Outcome | null>(null)
  const [verdict, setVerdict] = useState<Verdict | null>(null)
  const [lines, setLines] = useState<string[]>([])
  const battle = useRef<{ watch: Watch; mine: Entity[]; card: string; custom: Verdict | null } | null>(null)

  const reset = () => {
    haptic('light')
    battle.current = null
    setPhase('intro'); setCard(null); setGhost(null); setHint(null); setOutcome(null); setVerdict(null); setLines([])
    setRound(r => r + 1)
  }

  const finish = (sim: Sim) => {
    const b = battle.current
    if (!b) return
    let grade: Verdict['grade']
    if (b.custom) {
      setVerdict(b.custom)
      grade = b.custom.grade
    } else {
      const o = judge(sc, sim, b.watch, b.card, base, b.mine)
      setOutcome(o)
      setLines(explain(puzzle, o, b.card, sim.dealt[`me:${b.card}`] ?? 0, lang))
      grade = o.grade
    }
    const stars = grade === 'perfect' ? 3 : grade === 'ok' ? 2 : 0
    setPhase('result')
    onStars(stars)
    hapticNotify(stars > 0 ? 'success' : 'error')
  }

  const onTick = (sim: Sim): boolean => {
    if (phase === 'intro') {
      if (!atPause(sc, sim, at)) return false
      setPhase('choose')
      haptic('medium')
      return true
    }
    if (phase === 'battle' && battle.current?.watch.done(sim)) {
      finish(sim)
      return true
    }
    return false
  }

  const place = (c: string, x: number, y: number) => {
    const mine = playMine(sim, c, x, y)
    if (!mine) return
    battle.current = { watch: new Watch(sc, sim), mine, card: c, custom: puzzle.custom?.(c, x, y) ?? null }
    setGhost(null)
    setPhase('battle')
    haptic('heavy')
  }

  const tap = (x: number, y: number) => {
    if (phase !== 'choose') return
    if (!card) { setHint(s.pickCard); haptic('light'); return }
    const p = snap(card, x, y)
    const ok = canDeploy('me', card, p.x, p.y)
    // Второй тап по той же клетке - поставить
    if (ok && ghost?.ok && ghost.card === card && ghost.x === p.x && ghost.y === p.y) { place(card, p.x, p.y); return }
    setGhost({ card, ...p, ok })
    if (!ok) { setHint(card === 'Miner' ? s.noMiner : sideOf(p.y) === 'me' ? s.onTower : s.ownSide); hapticNotify('error'); return }
    setHint(null)
    haptic('light')
  }

  const toggleFast = () => {
    const next = !fast
    setFast(next)
    try { localStorage.setItem(FAST_KEY, next ? '1' : '0') } catch { /* пусть так */ }
    haptic('light')
  }

  const kind = card ? cardInfo(card).kind : null
  const zone = phase === 'choose' && card ? (kind === 'spell' ? 'spell' : card === 'Miner' ? 'miner' : 'troop') : null
  const grade = verdict?.grade ?? outcome?.grade ?? 'fail'
  const stars = grade === 'perfect' ? 3 : grade === 'ok' ? 2 : 0
  const rt = RESULT_TEXT[lang]

  return (
    <div className="tr-game fade-in">
      <div className="tr-top">
        <button className="btn-mini" onClick={() => { haptic('light'); onExit() }}><Icon name="chevronLeft" size={15} /> {s.back}</button>
        {daily && <span className="tr-badge">{s.daily}</span>}
      </div>
      <h2 className="tr-title">{text.title}</h2>
      <p className="tr-task">{text.task}</p>

      <div className={`tr-board ${phase === 'choose' ? 'tr-board-live' : ''}`}>
        <ArenaSim key={round} sim={sim} running={phase === 'intro' || phase === 'battle'} speed={fast ? 2.5 : 1}
          onTick={onTick} onTap={phase === 'choose' ? tap : undefined} ghost={ghost} zone={zone} />
        {phase === 'choose' && !ghost && <div className="tr-turn">{s.yourTurn}</div>}
        {(phase === 'intro' || phase === 'battle') && (
          <button className={`tr-speed ${fast ? 'tr-speed-on' : ''}`} onClick={toggleFast}>⏩ {fast ? '×2.5' : '×1'}</button>
        )}
        {phase === 'choose' && ghost?.ok && card && (
          <button className="tr-place" onClick={() => place(card, ghost.x, ghost.y)}>
            {emojiOf(card)} {s.place}
          </button>
        )}
      </div>

      <ElixirBar sim={sim} />

      <div className="tr-hand">
        {puzzle.hand.map(c => {
          const info = cardInfo(c)
          const tooDear = info.cost > sc.elixir
          return (
            <button key={c} className={`tr-card ${card === c ? 'tr-card-on' : ''}`} disabled={phase !== 'choose' || tooDear}
              onClick={() => {
                haptic('light')
                setCard(c)
                setGhost(g => (g ? { ...g, card: c, ...snap(c, g.x, g.y), ok: canDeploy('me', c, snap(c, g.x, g.y).x, snap(c, g.x, g.y).y) } : null))
                setHint(info.kind === 'spell' ? s.pickSpell : s.pickTile)
              }}>
              <span className="tr-card-art">{art[c] ? <img src={art[c]} alt="" /> : <span className="tr-emoji">{emojiOf(c)}</span>}</span>
              {art[c] && <span className="tr-card-emoji">{emojiOf(c)}</span>}
              <span className="tr-cost">{info.cost}</span>
              <small>{n(c)}</small>
            </button>
          )
        })}
      </div>

      {phase === 'choose' && <p className="tr-hint">{hint ?? (card ? (kind === 'spell' ? s.pickSpell : s.pickTile) : s.pickCard)}</p>}

      {phase === 'result' && (
        <div className={`tr-result tr-result-${grade}`}>
          <div className="tr-result-head">
            <Stars n={stars} />
            <b>{grade === 'perfect' ? s.perfect : grade === 'ok' ? s.ok : s.fail}</b>
          </div>
          {outcome && (
            <div className="tr-facts">
              <span className={outcome.dmg === 0 ? 'tr-fact-good' : 'tr-fact-bad'}>
                🏰 {outcome.dmg === 0 ? rt.tower0 : rt.tower.replace('{n}', String(outcome.dmg))}
                {outcome.base > 0 && <small>{rt.base.replace('{n}', String(outcome.base))}</small>}
              </span>
              <span className={outcome.killed ? 'tr-fact-good' : 'tr-fact-bad'}>
                ⏱ {outcome.killed && outcome.killIn !== null ? rt.stopped.replace('{s}', outcome.killIn.toFixed(1)) : rt.notStopped}
              </span>
              <span className={outcome.myCost <= outcome.enemyCost ? 'tr-fact-good' : 'tr-fact-bad'}>
                💧 {rt.trade.replace('{me}', String(outcome.myCost)).replace('{en}', String(outcome.enemyCost))}
                <small>{outcome.enemyCost - outcome.myCost > 0 ? '+' : ''}{outcome.enemyCost - outcome.myCost}</small>
              </span>
            </div>
          )}
          {verdict && <p>{REASONS[lang][verdict.reason] ?? ''}</p>}
          {lines.map((l, i) => <p key={i}>{l}</p>)}
          {HINTS[lang][puzzle.id] && <p className="tr-trick"><b>💡 {s.trick}:</b> {HINTS[lang][puzzle.id]}</p>}
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

/** Эликсир из боя: копится по 1 за 2,8 секунды, как в игре. */
function ElixirBar({ sim }: { sim: Sim }) {
  const [v, setV] = useState(sim.elixir.me)
  useEffect(() => {
    let raf = 0
    const loop = () => { setV(sim.elixir.me); raf = requestAnimationFrame(loop) }
    raf = requestAnimationFrame(loop)
    return () => cancelAnimationFrame(raf)
  }, [sim])
  const full = Math.floor(v)
  return (
    <div className="tr-elixir">
      <GameIcon name="drop" size={18} />
      <div className="tr-elixir-bar">
        {Array.from({ length: 10 }, (_, i) => (
          <i key={i} className={i < full ? 'on' : ''}>
            {i === full && <span style={{ width: `${(v - full) * 100}%` }} />}
          </i>
        ))}
      </div>
      <b>{full}</b>
    </div>
  )
}
