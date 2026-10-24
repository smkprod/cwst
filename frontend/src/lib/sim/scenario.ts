import { COST, UNITS } from './stats'
import { DT, Sim, canDeploy, type Entity } from './engine'

/**
 * Задача тренажёра поверх движка: соперник играет по сценарию, на решающем моменте
 * игра встаёт, игрок ставит одну карту - и бой досчитывается честно. Оценка - урон
 * по своим башням против того же боя без защиты, плюс размен эликсира.
 */

export type Grade = 'perfect' | 'ok' | 'fail'

export interface Scenario {
  elixir: number
  /** Ходы соперника: секунда и клетка. */
  enemy: { at: number; card: string; x: number; y: number }[]
  /** Когда встать на паузу: секунда или условие на состояние боя. */
  pause: number | ((sim: Sim) => boolean)
  /** Сколько секунд боя считать после хода игрока, самое большее. */
  horizon: number
  /** Урон по башне, при котором ещё «идеально» и ещё «сработало». */
  limits: [perfect: number, ok: number]
  /** Особая цель: «король» - на три звезды нужно ещё и разбудить свою королевскую башню. */
  goal?: 'king'
}

/** Сколько секунд боя показываем до паузы. */
export const INTRO = 2.6
const SETTLE = 1.0

export function newSim(sc: Scenario): Sim {
  const sim = new Sim(sc.elixir)
  for (const e of sc.enemy) sim.schedule({ at: e.at, team: 'enemy', card: e.card, x: e.x, y: e.y })
  return sim
}

function paused(sc: Scenario, sim: Sim): boolean {
  return typeof sc.pause === 'number' ? sim.t >= sc.pause - 1e-6 : sc.pause(sim)
}

/** Секунда боя, на которой встаёт пауза. */
export function pauseTime(sc: Scenario): number {
  const sim = newSim(sc)
  while (!paused(sc, sim) && sim.t < 30) sim.tick()
  return sim.t
}

/** Бой за INTRO секунд до паузы - с него начинается показ. Эликсир к паузе дорастёт до нужного. */
export function introSim(sc: Scenario, at = pauseTime(sc)): Sim {
  const sim = newSim(sc)
  const from = Math.max(0, at - INTRO)
  while (sim.t < from - 1e-6) sim.tick()
  sim.elixir.me = Math.max(0, sc.elixir - (at - sim.t) / 2.8)
  return sim
}

/** Встали на паузу? Тогда эликсир ровно как в условии задачи. */
export function atPause(sc: Scenario, sim: Sim, at: number): boolean {
  if (sim.t < at - DT / 2) return false
  sim.elixir.me = sc.elixir
  return true
}

/** Следит за боем после хода: когда закончить и что запомнить. */
export class Watch {
  readonly start: number
  readonly dmg0: number
  clearAt: number | null = null
  constructor(private sc: Scenario, sim: Sim) {
    this.start = sim.t
    this.dmg0 = sim.towerDamage.me
  }

  /** Бой окончен: соперник выбит и всё улеглось, либо вышло время. */
  done(sim: Sim): boolean {
    if (this.clearAt === null && !sim.threat('enemy')) this.clearAt = sim.t
    if (this.clearAt !== null && sim.t - this.clearAt >= SETTLE) return true
    return sim.t - this.start >= this.sc.horizon
  }
}

/** Урон по своим башням без защиты - с чем сравнивать. */
export function baseline(sc: Scenario): number {
  const at = pauseTime(sc)
  const sim = newSim(sc)
  while (sim.t < at - 1e-6) sim.tick()
  const w = new Watch(sc, sim)
  while (!w.done(sim)) sim.tick()
  return sim.towerDamage.me - w.dmg0
}

export interface Outcome {
  grade: Grade
  dmg: number
  base: number
  killed: boolean
  /** За сколько секунд после хода соперник выбит. */
  killIn: number | null
  myCost: number
  enemyCost: number
  /** Свой юнит пережил бой. */
  survived: boolean
  /** Своя королевская башня проснулась и стреляет. */
  kingAwake: boolean
}

/** Оценка: идеально - выбил, башня почти цела и размен не в минус; сработало - башня потеряла немного. */
export function judge(sc: Scenario, sim: Sim, w: Watch, card: string, base: number, mine: Entity[]): Outcome {
  const dmg = Math.round(sim.towerDamage.me - w.dmg0)
  const killed = !sim.threat('enemy')
  const myCost = COST[card] ?? 0
  const enemyCost = sc.enemy.reduce((a, e) => a + (COST[e.card] ?? 0), 0)
  const [perfect, ok] = sc.limits
  const king = sim.tower('me', 'K')
  const kingAwake = king.alive && king.active
  const goalMet = sc.goal !== 'king' || kingAwake
  const grade: Grade = killed && dmg <= perfect && myCost <= enemyCost && goalMet ? 'perfect'
    : dmg <= ok && dmg < base ? 'ok' : 'fail'
  return {
    grade, dmg, base: Math.round(base), killed, killIn: killed && w.clearAt !== null ? w.clearAt - w.start : null,
    myCost, enemyCost, survived: mine.some(e => e.alive), kingAwake,
  }
}

/** Поставить карту игрока; вернёт его юнитов (для «выжил ли») или null, если сюда нельзя. */
export function playMine(sim: Sim, card: string, x: number, y: number): Entity[] | null {
  if (!canDeploy('me', card, x, y)) return null
  const before = new Set(sim.entities.map(e => e.id))
  if (!sim.play('me', card, x, y)) return null
  return sim.entities.filter(e => !before.has(e.id))
}

/** Карта бьёт по воздуху / только здания - для объяснений. */
export const hitsAir = (card: string) => UNITS[card]?.air ?? card === 'Fireball'
export const buildingsOnly = (card: string) => UNITS[card]?.buildingsOnly ?? false
export const isFlying = (card: string) => UNITS[card]?.flying ?? false
