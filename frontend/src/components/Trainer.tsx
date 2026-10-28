import { useEffect, useMemo, useRef, useState } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { haptic, hapticNotify } from '../lib/telegram'
import { openTip } from '../lib/tipSheet'
import { questDone } from '../lib/quests'
import { useCardArt } from '../lib/cardArt'
import {
  CARD_NAMES, FREE_PUZZLES, HINTS, PUZZLES, PUZZLE_TEXT, RESULT_TEXT, cardInfo, dailyIndex, explain, loadStars,
  loadStreak, markDailySolved, markSandboxUsed, sandboxUsedToday, saveStars, snap, takePendingPuzzle, type Puzzle, type PuzzleCat,
} from '../lib/trainer'
import { canDeploy, sideOf, type Entity, type Sim } from '../lib/sim/engine'
import { emojiOf } from '../lib/sim/emoji'
import { TOWER_STATS } from '../lib/sim/stats'
import emblem from '../assets/clanify-emblem.webp'
import { Watch, atPause, baseline, introSim, isAttack, judge, myHero, pauseTime, playMine, type Outcome } from '../lib/sim/scenario'
import { HEROES } from '../lib/sim/heroes'
import { EVOS } from '../lib/sim/evos'
import { ArenaSim, type SimGhost } from './ArenaSim'
import { artFor } from './ArenaAnim'
import { Sandbox } from './Sandbox'
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
    // Закрытая задача тоже откроется - только показом решения
    return i >= 0 ? i : null
  })
  const streak = loadStreak()
  const openPlus = usePlusSheet()
  const [sandbox, setSandbox] = useState(false)
  const [details, toggleDetails] = useDetails()

  useEffect(() => {
    const load = () => api.getPlus().then(p => setUnlocked(p.unlocked || Boolean(p.clanPro))).catch(() => { /* без статуса - как без Плюса */ })
    load()
    window.addEventListener(PLUS_CHANGED, load)
    return () => window.removeEventListener(PLUS_CHANGED, load)
  }, [])

  const isOpen = (i: number) => unlocked || i < FREE_PUZZLES || i === daily

  if (sandbox) return <Sandbox onExit={() => setSandbox(false)} details={details} onDetails={toggleDetails} />

  // Песочница без Плюса - один запуск в день
  const openSandbox = () => {
    haptic('medium')
    if (!unlocked && sandboxUsedToday()) { openPlus('plus'); return }
    if (!unlocked) markSandboxUsed()
    setSandbox(true)
  }

  if (current !== null) {
    const next = () => {
      const after = PUZZLES.findIndex((_, i) => i > current && isOpen(i))
      setCurrent(after >= 0 ? after : null)
    }
    return (
      <TrainerGame key={current} puzzle={PUZZLES[current]} daily={current === daily} locked={!isOpen(current)}
        details={details} onDetails={toggleDetails} onPlus={() => openPlus('plus')}
        onExit={() => setCurrent(null)} onNext={next}
        onStars={n => {
          setStars(saveStars(PUZZLES[current].id, n))
          if (current === daily && n > 0) markDailySolved()
          if (n > 0) questDone('trainer')
        }} />
    )
  }

  const lockedCount = PUZZLES.filter((_, i) => !isOpen(i)).length
  // Только по задачам, что есть сейчас: звёзды за убранные задачи остаются в памяти телефона
  const totalStars = PUZZLES.reduce((a, p) => a + (stars[p.id] ?? 0), 0)

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

      <button className="tr-sandbox" onClick={openSandbox}>
        <span>🧪</span>
        <div>
          <b>{s.sandbox}</b><small>{s.sandboxLead}</small>
          {!unlocked && <small className="tr-sandbox-free">{sandboxUsedToday() ? `🔒 ${s.sandboxUsed}` : `🎁 ${s.sandboxFree}`}</small>}
        </div>
        <Icon name="chevronRight" size={18} />
      </button>

      <button className="tr-daily" onClick={() => { haptic('medium'); setCurrent(daily) }}>
        <TargetArt card={PUZZLES[daily].icon ?? PUZZLES[daily].enemyCard} />
        <div>
          <small>{s.daily}</small>
          <b>{PUZZLE_TEXT[lang][PUZZLES[daily].id]?.title ?? ''}</b>
          <Stars n={stars[PUZZLES[daily].id] ?? 0} />
        </div>
        <span className="tr-play"><GameIcon name="play" size={34} /></span>
      </button>

      {CATS.map(cat => {
        const list = PUZZLES.map((p, i) => ({ p, i })).filter(x => x.p.cat === cat)
        if (list.length === 0) return null
        const got = list.reduce((a, x) => a + (stars[x.p.id] ?? 0), 0)
        return (
          <section key={cat} className={`tr-cat tr-cat-${cat}`}>
            <div className="tr-cat-head">
              <b>{s.cats[cat]}</b>
              <small>{s.catsLead[cat]}</small>
              <span className="tr-cat-stars"><GameIcon name="crown" size={14} /> {got}/{list.length * 3}</span>
            </div>
            <div className="tr-grid">
              {list.map(({ p, i }) => {
                const ok = isOpen(i)
                return (
                  <button key={p.id} className={`tr-tile ${ok ? '' : 'tr-locked'}`}
                    onClick={() => { haptic('light'); setCurrent(i) }}>
                    <TargetArt card={p.icon ?? p.enemyCard} />
                    <b>{PUZZLE_TEXT[lang][p.id]?.title}</b>
                    {ok ? <Stars n={stars[p.id] ?? 0} /> : <span className="tr-lock"><GameIcon name="lock" size={16} /> {s.locked}</span>}
                  </button>
                )
              })}
            </div>
          </section>
        )
      })}
      {lockedCount > 0 && (
        <button className="pd-cta" onClick={() => { haptic('medium'); openPlus('plus') }}>
          <GameIcon name="gem" size={20} /> {s.unlock.replace('{n}', String(lockedCount))}
        </button>
      )}
    </div>
  )
}

/** Порядок разделов в списке задач: комбо-атаки - самое интересное, их первыми, дальше от основ к приёмам. */
const CATS: PuzzleCat[] = ['attack', 'defense', 'cheap', 'king', 'hero', 'evo']

function TargetArt({ card }: { card: string }) {
  const art = useCardArt()
  const src = artFor(art, card)
  return <span className={`tr-art ${HEROES[card] ? 'tr-art-hero' : EVOS[card] ? 'tr-art-evo' : ''}`}>{src ? <img src={src} alt="" /> : <span className="tr-emoji">{emojiOf(card)}</span>}</span>
}

function Stars({ n }: { n: number }) {
  return (
    <span className="tr-stars">
      {[0, 1, 2].map(i => <span key={i} className={i < n ? 'tr-star-on' : ''}>★</span>)}
    </span>
  )
}

/**
 * Фазы задачи. Сначала показ: бой идёт до паузы, на поле подсвечивается эталонный ход,
 * карта ставится сама, бой доигрывается - видно, куда и как. Потом то же самое делает
 * игрок: пауза, выбор карты и клетки, бой, итог.
 */
type Phase = 'demo-intro' | 'demo-show' | 'demo-battle' | 'demo-wait' | 'demo-done' | 'intro' | 'choose' | 'battle' | 'wait' | 'result'

const DEMO = new Set<Phase>(['demo-intro', 'demo-show', 'demo-battle', 'demo-wait', 'demo-done'])

/** Здоровье башни принцессы - столько урона значит «башня снесена». */
const TOWER_HP = TOWER_STATS.Princess.hp

const FAST_KEY = 'cwst_trainer_fast'

function loadFast(): boolean {
  try { return localStorage.getItem(FAST_KEY) === '1' } catch { return false }
}

const DETAILS_KEY = 'cwst_trainer_details'

/** Показ радиусов и целей - общий для задач и песочницы, запоминается. */
function useDetails(): [boolean, () => void] {
  const [on, setOn] = useState(() => { try { return localStorage.getItem(DETAILS_KEY) === '1' } catch { return false } })
  const toggle = () => {
    haptic('light')
    setOn(v => {
      try { localStorage.setItem(DETAILS_KEY, v ? '0' : '1') } catch { /* без памяти - до перезапуска */ }
      return !v
    })
  }
  return [on, toggle]
}

/**
 * @param locked Задача в Плюсе: показ решения - бесплатно, а вместо своей попытки - покупка.
 */
function TrainerGame({ puzzle, daily, locked, details, onDetails, onPlus, onExit, onNext, onStars }: {
  puzzle: Puzzle; daily: boolean; locked: boolean; details: boolean; onDetails: () => void; onPlus: () => void
  onExit: () => void; onNext: () => void; onStars: (n: number) => void
}) {
  const { t, lang } = useT()
  const s = t.trainer
  const n = (card: string) => CARD_NAMES[lang][card] ?? card
  const text = PUZZLE_TEXT[lang][puzzle.id]
  const art = useCardArt()
  const sc = puzzle.sc
  const at = useMemo(() => pauseTime(sc), [sc])
  // Тот же бой без защиты - с ним сравниваем урон по башне
  const base = useMemo(() => baseline(sc), [sc])
  const [round, setRound] = useState(0)
  const sim = useMemo(() => introSim(sc, at), [sc, at, round])
  const [phase, setPhase] = useState<Phase>('demo-intro')
  const [card, setCard] = useState<string | null>(null)
  const [ghost, setGhost] = useState<SimGhost | null>(null)
  const [hint, setHint] = useState<string | null>(null)
  const [fast, setFast] = useState(loadFast)
  const [outcome, setOutcome] = useState<Outcome | null>(null)
  const [demoNote, setDemoNote] = useState<string | null>(null)
  const [lines, setLines] = useState<string[]>([])
  // cards - все сыгранные карты (в комбо их несколько), since - секунда последнего хода
  const battle = useRef<{ watch: Watch; mine: Entity[]; card: string; cards: string[]; t0: number; since: number; pressed: boolean } | null>(null)
  // Сколько ходов уже сделано: в комбо следующий ход - эталон combo[step - 1]
  const [step, setStep] = useState(0)
  const moves = 1 + (puzzle.combo?.length ?? 0)
  // Показ решения нажал способность Героя - коротко подписать это на поле
  const [flash, setFlash] = useState<string | null>(null)
  const hasHero = Boolean(puzzle.abilityOnly || puzzle.hand.some(c => HEROES[c]))
  const resultRef = useRef<HTMLDivElement>(null)
  const demo = DEMO.has(phase)
  const sol = puzzle.solution
  // Ход, который сейчас показываем или ждём: первый - solution, дальше - из комбо
  const cur = step === 0 || !puzzle.combo ? sol : puzzle.combo[Math.min(step, puzzle.combo.length) - 1].move

  // Итог под доской - докручиваем к нему, чтобы не искать
  useEffect(() => {
    if (phase === 'result') resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  }, [phase])

  // Показ: подержать подсветку хода, чтобы успеть увидеть клетку и карту, - и поставить
  useEffect(() => {
    if (phase !== 'demo-show') return
    const id = setTimeout(() => place(cur.card, cur.x, cur.y, 'demo-battle'), fast ? 1100 : 1800)
    return () => clearTimeout(id)
    // place берёт бой текущего раунда - перезапуск нужен только при смене фазы или раунда
  }, [phase, round])

  const restart = (next: Phase) => {
    battle.current = null
    setPhase(next); setCard(null); setGhost(null); setHint(null); setOutcome(null); setLines([]); setDemoNote(null); setFlash(null)
    setStep(0)
    setRound(r => r + 1)
  }
  const play = () => { haptic('medium'); restart('intro') }
  const showAgain = () => { haptic('light'); restart('demo-intro') }

  const finish = (sim: Sim) => {
    const b = battle.current
    if (!b) return
    const o = judge(sc, sim, b.watch, b.cards, base, b.mine)
    setOutcome(o)
    // В комбо разбор одной карты ни о чём: главное - приём, его объясняет подсказка ниже
    setLines(puzzle.combo ? [] : explain(puzzle, o, b.card, sim.dealt[`me:${b.card}`] ?? 0, lang))
    const stars = o.grade === 'perfect' ? 3 : o.grade === 'ok' ? 2 : 0
    setPhase('result')
    onStars(stars)
    hapticNotify(stars > 0 ? 'success' : 'error')
    if (stars === 3) celebrate()
  }

  /** Конец показа: коротко, что вышло, - и предложение повторить самому. */
  const finishDemo = (sim: Sim) => {
    const b = battle.current
    if (!b) return
    const rt = RESULT_TEXT[lang]
    const o = judge(sc, sim, b.watch, b.cards, base, b.mine)
    const parts = isAttack(sc)
      ? [o.dealt >= TOWER_HP ? rt.towerDown : rt.enemyTower.replace('{n}', String(o.dealt))]
      : [o.dmg === 0 ? rt.tower0 : rt.tower.replace('{n}', String(o.dmg))]
    if (!isAttack(sc) && o.killed && o.killIn !== null) parts.push(rt.stopped.replace('{s}', o.killIn.toFixed(1)))
    if (sc.goal === 'king' && o.kingAwake) parts.push(rt.kingAwake)
    setDemoNote(parts.join(' · '))
    setPhase('demo-done')
    hapticNotify('success')
  }

  /** Бой без своего хода картой: Герой уже на поле, ход - нажать способность. */
  const startFight = (next: Phase) => {
    battle.current = { watch: new Watch(sc, sim), mine: [], card: '', cards: [], t0: sim.t, since: sim.t, pressed: false }
    setPhase(next)
  }

  const onTick = (sim: Sim): boolean => {
    if (phase === 'intro' || phase === 'demo-intro') {
      if (!atPause(sc, sim, at)) return false
      if (phase === 'intro') {
        setPhase('choose')
      } else if (puzzle.abilityOnly) {
        // Показ задачи на способность: бой сразу идёт дальше, кнопку нажмёт показ
        startFight('demo-battle')
        return false
      } else {
        // Подсветить эталонный ход: карта в руке и клетка на поле
        setCard(sol.card)
        setGhost({ card: sol.card, x: sol.x, y: sol.y, ok: true })
        setPhase('demo-show')
      }
      haptic('medium')
      return true
    }
    const b = battle.current
    // Комбо: бой идёт до момента следующего хода - и снова встаёт
    if ((phase === 'wait' || phase === 'demo-wait') && b && puzzle.combo) {
      const next = puzzle.combo[step - 1]
      // Время боя вышло раньше, чем настал момент хода, - итог как есть
      if (sim.t - b.watch.start >= sc.horizon) {
        if (phase === 'wait') finish(sim); else finishDemo(sim)
        return true
      }
      if (!next || !next.pause(sim, b.since)) return false
      // Сыграть уже нечем (всё дорого или сыграно) - бой просто идёт дальше
      const left = puzzle.hand.filter(c => !b.cards.includes(c) && cardInfo(c).cost <= sim.elixir.me + 1e-6)
      if (phase === 'wait' && left.length === 0) {
        setPhase('battle')
        return false
      }
      if (phase === 'wait') {
        setCard(null)
        setPhase('choose')
      } else {
        setCard(next.move.card)
        setGhost({ card: next.move.card, x: next.move.x, y: next.move.y, ok: true })
        setPhase('demo-show')
      }
      haptic('medium')
      return true
    }
    if ((phase === 'battle' || phase === 'demo-battle') && b) {
      if (phase === 'demo-battle' && sol.ability !== undefined && !b.pressed && sim.t >= b.t0 + sol.ability - 1e-6) {
        const h = myHero(sim)
        if (h && h.ability === 'ready' && sim.useAbility(h)) {
          b.pressed = true
          setFlash(s.demoAbility.replace('{name}', s.abilities[HEROES[h.card].kind]))
          haptic('heavy')
        }
      }
      if (b.watch.done(sim)) {
        if (phase === 'battle') finish(sim); else finishDemo(sim)
        return true
      }
    }
    return false
  }

  const place = (c: string, x: number, y: number, next: Phase = 'battle') => {
    const mine = playMine(sim, c, x, y)
    if (!mine) return
    const b = battle.current
    if (b) {
      // Следующий ход комбо: бой и счёт - те же, что с первого хода
      b.mine.push(...mine)
      b.cards.push(c)
      b.since = sim.t
      // Пока ждали хода, поле могло опустеть - бой считаем заново с этого хода
      b.watch.clearAt = null
    } else {
      battle.current = { watch: new Watch(sc, sim), mine, card: c, cards: [c], t0: sim.t, since: sim.t, pressed: false }
    }
    const done = step + 1
    setStep(done)
    setGhost(null)
    // Ещё не все ходы комбо - ждём момента следующего
    setPhase(done < moves ? (next === 'demo-battle' ? 'demo-wait' : 'wait') : next)
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
  const grade = outcome?.grade ?? 'fail'
  const stars = grade === 'perfect' ? 3 : grade === 'ok' ? 2 : 0
  const rt = RESULT_TEXT[lang]
  const running = phase === 'intro' || phase === 'battle' || phase === 'demo-intro' || phase === 'demo-battle' || phase === 'wait' || phase === 'demo-wait'
  // Сколько эликсира на этот ход: в комбо - сколько накопилось к нему
  const elixirNow = step === 0 ? sc.elixir : Math.floor(sim.elixir.me + 1e-6)
  const playedCards = battle.current?.cards ?? []

  return (
    <div className="tr-game fade-in">
      <div className="tr-top">
        <button className="btn-mini" onClick={() => { haptic('light'); onExit() }}><Icon name="chevronLeft" size={15} /> {s.back}</button>
        <span className="tr-top-right">
          {daily && <span className="tr-badge">{s.daily}</span>}
          <button className={`tr-toggle ${details ? 'tr-toggle-on' : ''}`} onClick={onDetails}>📐 {s.details}</button>
        </span>
      </div>
      <div className="tr-head">
        <h2 className="tr-title">{text.title}</h2>
        <p className="tr-task">{text.task}</p>
      </div>

      <div className="tr-stage">
        <div className="tr-stage-head"><img src={emblem} alt="" /><b>{s.arena}</b></div>
        <div className={`tr-board ${phase === 'choose' ? 'tr-board-live' : ''} ${demo ? 'tr-board-demo' : ''}`}>
          <ArenaSim key={round} sim={sim} running={running} speed={fast ? 2.5 : 1}
            onTick={onTick} onTap={phase === 'choose' ? tap : undefined} ghost={ghost} zone={zone} names={n}
            ghostLabel={phase === 'demo-show' ? s.demoHere.replace('{card}', n(cur.card)) : undefined}
            camp={{ name: s.campName, clan: s.campClan, you: s.you }} details={details} />
          {demo && phase !== 'demo-done' && <div className="tr-turn tr-turn-demo">🎬 {locked ? s.lockedDemo : s.demo}</div>}
          {phase === 'choose' && puzzle.abilityOnly && (
            <>
              <div className="tr-turn">{s.abilityTask}</div>
              <button className="tr-place" onClick={() => { haptic('heavy'); startFight('battle') }}>⚔️ {s.startBattle}</button>
            </>
          )}
          {phase === 'choose' && !puzzle.abilityOnly && !ghost?.ok && (
            <div className={`tr-turn ${hint && ghost ? 'tr-turn-bad' : ''}`}>
              {moves > 1 && <b className="tr-move-of">{s.moveOf.replace('{n}', String(step + 1)).replace('{m}', String(moves))} · </b>}
              {hint ?? (card ? (kind === 'spell' ? s.pickSpell : s.pickTile) : `${s.yourTurn} · ${s.pickCard}`)}
            </div>
          )}
          {flash && (phase === 'demo-battle' || phase === 'demo-done') && <div className="tr-flash fade-up">⚡ {flash}</div>}
          {running && (
            <button className={`tr-speed ${fast ? 'tr-speed-on' : ''}`} onClick={toggleFast}>⏩ {fast ? '×2.5' : '×1'}</button>
          )}
          {demo && phase !== 'demo-done' && !locked && <button className="tr-skip" onClick={play}>{s.skip} ›</button>}
          {phase === 'choose' && ghost?.ok && card && (
            <button className="tr-place" onClick={() => place(card, ghost.x, ghost.y)}>
              {artFor(art, card) ? <img className="tr-place-art" src={artFor(art, card)} alt="" /> : emojiOf(card)} {s.place}
            </button>
          )}
          {phase === 'demo-done' && (
            <div className="tr-demo-done fade-up">
              <b>✓ {demoNote}</b>
              {locked
                ? <button className="tr-go tr-go-plus" onClick={() => { haptic('medium'); onPlus() }}>🔒 {s.tryPlus}</button>
                : <button className="tr-go" onClick={play}>{s.nowYou} <Icon name="chevronRight" size={16} /></button>}
              <button className="tr-again" onClick={showAgain}><Icon name="refresh" size={14} /> {s.showAgain}</button>
            </div>
          )}
        </div>

        <div className="tr-tray">
          {hasHero && <AbilityBar sim={sim} active={phase === 'battle'} />}
          {puzzle.hand.length > 0 && <div className="tr-hand">
            {puzzle.hand.map(c => {
              const info = cardInfo(c)
              const tooDear = info.cost > elixirNow
              const used = playedCards.includes(c) && phase !== 'result'
              const shown = demo && c === cur.card && (phase === 'demo-show' || phase === 'demo-done')
              return (
                <button key={c} className={`tr-card ${card === c && !demo ? 'tr-card-on' : ''} ${shown ? 'tr-card-demo' : ''} ${HEROES[c] ? 'tr-card-hero' : EVOS[c] ? 'tr-card-evo' : ''}`}
                  disabled={phase !== 'choose' || tooDear || used}
                  onClick={() => {
                    haptic('light')
                    setCard(c)
                    setGhost(g => (g ? { ...g, card: c, ...snap(c, g.x, g.y), ok: canDeploy('me', c, snap(c, g.x, g.y).x, snap(c, g.x, g.y).y) } : null))
                    setHint(info.kind === 'spell' ? s.pickSpell : s.pickTile)
                  }}>
                  <span className="tr-card-art">{artFor(art, c) ? <img src={artFor(art, c)} alt="" /> : <span className="tr-emoji">{emojiOf(c)}</span>}</span>
                  <span className="tr-cost">{info.cost}</span>
                  <small>{n(c)}</small>
                </button>
              )
            })}
          </div>}
          <ElixirBar sim={sim} max={s.max} />
        </div>
      </div>

      {phase === 'result' && (
        <div ref={resultRef} className={`tr-result tr-result-${grade}`}>
          <div className="tr-result-head">
            <Stars n={stars} />
            <b>{grade === 'perfect' ? s.perfect : grade === 'ok' ? s.ok : s.fail}</b>
          </div>
          {outcome && isAttack(sc) && (
            <div className="tr-facts">
              <span className={outcome.dealt >= sc.limits[0] ? 'tr-fact-good' : 'tr-fact-bad'}>
                🏰 {outcome.dealt >= TOWER_HP ? rt.towerDown : rt.enemyTower.replace('{n}', String(outcome.dealt))}
                <small>{rt.noMove.replace('{n}', String(outcome.base))}</small>
              </span>
              <span className="tr-fact-good">
                💧 {(playedCards.length > 0 ? playedCards : [card ?? '']).map(c => CARD_NAMES[lang][c] ?? c).join(' + ')} · {outcome.myCost}
              </span>
            </div>
          )}
          {outcome && !isAttack(sc) && (
            <div className="tr-facts">
              <span className={outcome.dmg === 0 ? 'tr-fact-good' : 'tr-fact-bad'}>
                🏰 {outcome.dmg === 0 ? rt.tower0 : rt.tower.replace('{n}', String(outcome.dmg))}
                {outcome.base > 0 && <small>{rt.base.replace('{n}', String(outcome.base))}</small>}
              </span>
              <span className={outcome.killed ? 'tr-fact-good' : 'tr-fact-bad'}>
                ⏱ {outcome.killed && outcome.killIn !== null ? rt.stopped.replace('{s}', outcome.killIn.toFixed(1)) : rt.notStopped}
              </span>
              {sc.goal === 'king' && (
                <span className={outcome.kingAwake ? 'tr-fact-good' : 'tr-fact-bad'}>👑 {outcome.kingAwake ? rt.kingAwake : rt.kingAsleep}</span>
              )}
              <span className={outcome.myCost <= (sc.cost ?? outcome.enemyCost) ? 'tr-fact-good' : 'tr-fact-bad'}>
                💧 {rt.trade.replace('{me}', String(outcome.myCost)).replace('{en}', String(outcome.enemyCost))}
                <small>{outcome.enemyCost - outcome.myCost > 0 ? '+' : ''}{outcome.enemyCost - outcome.myCost}</small>
              </span>
            </div>
          )}
          {lines.map((l, i) => <p key={i}>{l}</p>)}
          {HINTS[lang][puzzle.id] && <p className="tr-trick"><b>💡 {s.trick}:</b> {HINTS[lang][puzzle.id]}</p>}
          <div className="tr-result-btns">
            <button className="btn btn-ghost" onClick={play}><Icon name="refresh" size={15} /> {s.retry}</button>
            <button className="btn btn-ghost" onClick={showAgain}><GameIcon name="play" size={16} /> {s.showAgain}</button>
            <button className="btn btn-ghost" onClick={() => openTip(puzzle.tip)}><GameIcon name="scroll" size={16} /> {s.how}</button>
            <button className="btn" onClick={() => { haptic('medium'); onNext() }}>{s.next} <Icon name="chevronRight" size={15} /></button>
          </div>
        </div>
      )}
    </div>
  )
}

/**
 * Салют за три звезды. Библиотека грузится отдельным кусочком только в этот момент,
 * в основной бандл не входит; кто просил меньше анимации - салюта не будет.
 */
function celebrate() {
  import('canvas-confetti').then(({ default: confetti }) => {
    const base = { disableForReducedMotion: true, zIndex: 300, colors: ['#ffd34d', '#3d8bff', '#ff8fb1', '#7dffb0', '#ffffff'] }
    confetti({ ...base, particleCount: 90, spread: 75, startVelocity: 42, origin: { x: 0.5, y: 0.75 } })
    setTimeout(() => {
      confetti({ ...base, particleCount: 45, angle: 60, spread: 55, origin: { x: 0, y: 0.8 } })
      confetti({ ...base, particleCount: 45, angle: 120, spread: 55, origin: { x: 1, y: 0.8 } })
    }, 220)
  }).catch(() => { /* без салюта - не страшно */ })
}

/**
 * Кнопка способности Героя: появляется, когда свой Герой на поле. Нажать можно один
 * раз за выход, если хватает эликсира; дальше показывает, что со способностью сейчас.
 */
function AbilityBar({ sim, active }: { sim: Sim; active: boolean }) {
  const { t } = useT()
  const s = t.trainer
  const art = useCardArt()
  const [, tick] = useState(0)
  useEffect(() => {
    const id = window.setInterval(() => tick(v => v + 1), 100)
    return () => window.clearInterval(id)
  }, [sim])
  const hero = sim.entities.find(e => e.alive && e.team === 'me' && e.ability !== undefined)
  // Был на поле и пропал - погиб; ещё не появлялся - выходит
  const seen = useRef<Sim | null>(null)
  if (hero) seen.current = sim
  if (!hero) return <div className="tr-ability tr-ability-wait"><small>{seen.current === sim ? s.abilityDead : s.abilityWait}</small></div>
  const h = HEROES[hero.card]
  const state = hero.deploy > 0 ? null : hero.ability ?? 'used'
  const can = active && state === 'ready' && sim.elixir.me >= h.cost - 1e-6
  const src = artFor(art, hero.card)
  return (
    <button className={`tr-ability ${can ? 'on' : ''} tr-ability-${state ?? 'deploy'}`} disabled={!can}
      onClick={() => { if (sim.useAbility(hero)) hapticNotify('success') }}>
      <span className="tr-ability-art">{src ? <img src={src} alt="" /> : emojiOf(hero.card)}</span>
      <span className="tr-ability-text">
        <b>⚡ {s.abilities[h.kind]}</b>
        <small>{state === null ? s.abilityWait : state === 'ready' ? (active ? s.abilityState.ready : '') : s.abilityState[state]}</small>
      </span>
      <span className="tr-ability-cost">{h.cost}</span>
    </button>
  )
}

/** Эликсир из боя: копится по 1 за 2,8 секунды, как в игре. */
function ElixirBar({ sim, max }: { sim: Sim; max: string }) {
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
      <span className="tr-drop"><b>{full}</b></span>
      <small className="tr-max">{max}</small>
      <div className="tr-elixir-bar">
        {Array.from({ length: 10 }, (_, i) => (
          <i key={i} className={i < full ? 'on' : ''}>
            {i === full && <span style={{ width: `${(v - full) * 100}%` }} />}
          </i>
        ))}
      </div>
    </div>
  )
}
