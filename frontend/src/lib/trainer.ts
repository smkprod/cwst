import type { Lang } from './i18n'
import type { TipId } from './tips'
import type { Sim } from './sim/engine'
import { COST, UNITS } from './sim/stats'
import type { Outcome, Scenario } from './sim/scenario'

/**
 * Тренажёр «Задефай атаку» на движке боя (lib/sim): соперник играет по сценарию, на
 * решающем моменте игра встаёт - игрок ставит карту, и бой досчитывается по
 * характеристикам карт из игры. Оценка - урон по башне против того же боя без защиты
 * и размен эликсира; объяснение собирается из того, что реально произошло.
 *
 * Координаты - клетки арены 18×32, y сверху: река 15-17, своя половина ниже 17.
 */

export type { Grade } from './sim/scenario'

export type CardKind = 'troop' | 'building' | 'spell'

export interface HandCard { card: string; cost: number; kind: CardKind }

const SPELL_CARDS = new Set(['Fireball', 'The Log', 'Goblin Barrel', 'Evo Goblin Barrel', 'Tornado'])

export function cardInfo(card: string): HandCard {
  const kind: CardKind = SPELL_CARDS.has(card) ? 'spell' : UNITS[card]?.building ? 'building' : 'troop'
  return { card, cost: COST[card] ?? 0, kind }
}

export const CARD_NAMES: Record<Lang, Record<string, string>> = {
  ru: { 'Cannon': 'Пушка', 'Knight': 'Рыцарь', 'Skeletons': 'Скелеты', 'Fireball': 'Огн. шар', 'The Log': 'Бревно', 'Musketeer': 'Мушкетёр',
    'Mini P.E.K.K.A': 'Мини-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големит', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-рыцарь', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гоблины', 'Balloon': 'Шар', 'Giant': 'Гигант', 'X-Bow': 'Икс-лук', 'Miner': 'Шахтёр', 'Tornado': 'Торнадо', 'Mortar': 'Мортира', 'P.E.K.K.A': 'ПЕККА', 'Ice Golem': 'Лед. голем', 'Ice Spirit': 'Лед. дух',
    'Valkyrie': 'Валькирия', 'Archers': 'Лучницы', 'Minions': 'Миньоны', 'Bats': 'Мыши', 'Tesla': 'Тесла',
    'Hero Knight': 'Герой Рыцарь', 'Hero Giant': 'Герой Гигант', 'Hero Musketeer': 'Герой Мушкетёр', 'Hero Mini P.E.K.K.A': 'Герой Мини-ПЕККА', 'Hero Turret': 'Турель',
    'Evo Knight': 'Эво-Рыцарь', 'Evo Skeletons': 'Эво-Скелеты', 'Evo Valkyrie': 'Эво-Валькирия', 'Evo Ice Spirit': 'Эво-Лед. дух', 'Evo Tesla': 'Эво-Тесла',
    'Evo Mega Knight': 'Эво-Мега-рыцарь', 'Evo P.E.K.K.A': 'Эво-ПЕККА', 'Evo Bats': 'Эво-Мыши', 'Evo Mortar': 'Эво-Мортира', 'Evo Archers': 'Эво-Лучницы',
    'Evo Goblin Barrel': 'Эво-Бочка', 'Decoy Goblins': 'Гоблины-приманки' },
  uk: { 'Cannon': 'Гармата', 'Knight': 'Лицар', 'Skeletons': 'Скелети', 'Fireball': 'Вогн. куля', 'The Log': 'Колода', 'Musketeer': 'Мушкетерка',
    'Mini P.E.K.K.A': 'Міні-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големіт', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-лицар', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гобліни', 'Balloon': 'Куля', 'Giant': 'Гігант', 'X-Bow': 'Ікс-лук', 'Miner': 'Шахтар', 'Tornado': 'Торнадо', 'Mortar': 'Мортира', 'P.E.K.K.A': 'ПЕККА', 'Ice Golem': 'Криж. голем', 'Ice Spirit': 'Криж. дух',
    'Valkyrie': 'Валькірія', 'Archers': 'Лучниці', 'Minions': 'Міньйони', 'Bats': 'Кажани', 'Tesla': 'Тесла',
    'Hero Knight': 'Герой Лицар', 'Hero Giant': 'Герой Гігант', 'Hero Musketeer': 'Герой Мушкетерка', 'Hero Mini P.E.K.K.A': 'Герой Міні-ПЕККА', 'Hero Turret': 'Турель',
    'Evo Knight': 'Ево-Лицар', 'Evo Skeletons': 'Ево-Скелети', 'Evo Valkyrie': 'Ево-Валькірія', 'Evo Ice Spirit': 'Ево-Криж. дух', 'Evo Tesla': 'Ево-Тесла',
    'Evo Mega Knight': 'Ево-Мега-лицар', 'Evo P.E.K.K.A': 'Ево-ПЕККА', 'Evo Bats': 'Ево-Кажани', 'Evo Mortar': 'Ево-Мортира', 'Evo Archers': 'Ево-Лучниці',
    'Evo Goblin Barrel': 'Ево-Бочка', 'Decoy Goblins': 'Гобліни-приманки' },
  en: { 'Cannon': 'Cannon', 'Knight': 'Knight', 'Skeletons': 'Skeletons', 'Fireball': 'Fireball', 'The Log': 'Log', 'Musketeer': 'Musketeer',
    'Mini P.E.K.K.A': 'Mini PEKKA', 'Golem': 'Golem', 'Golemite': 'Golemite', 'Hog Rider': 'Hog', 'Mega Knight': 'Mega Knight', 'Goblin Barrel': 'Barrel',
    'Goblins': 'Goblins', 'Balloon': 'Balloon', 'Giant': 'Giant', 'X-Bow': 'X-Bow', 'Miner': 'Miner', 'Tornado': 'Tornado', 'Mortar': 'Mortar', 'P.E.K.K.A': 'P.E.K.K.A', 'Ice Golem': 'Ice Golem', 'Ice Spirit': 'Ice Spirit',
    'Valkyrie': 'Valkyrie', 'Archers': 'Archers', 'Minions': 'Minions', 'Bats': 'Bats', 'Tesla': 'Tesla',
    'Hero Knight': 'Hero Knight', 'Hero Giant': 'Hero Giant', 'Hero Musketeer': 'Hero Musketeer', 'Hero Mini P.E.K.K.A': 'Hero Mini PEKKA', 'Hero Turret': 'Turret',
    'Evo Knight': 'Evo Knight', 'Evo Skeletons': 'Evo Skeletons', 'Evo Valkyrie': 'Evo Valkyrie', 'Evo Ice Spirit': 'Evo Ice Spirit', 'Evo Tesla': 'Evo Tesla',
    'Evo Mega Knight': 'Evo Mega Knight', 'Evo P.E.K.K.A': 'Evo P.E.K.K.A', 'Evo Bats': 'Evo Bats', 'Evo Mortar': 'Evo Mortar', 'Evo Archers': 'Evo Archers',
    'Evo Goblin Barrel': 'Evo Barrel', 'Decoy Goblins': 'Decoy Goblins' },
}

/** Раздел списка задач: защита, «разбуди короля», «отбейся дёшево», атака. */
export type PuzzleCat = 'defense' | 'king' | 'cheap' | 'hero' | 'evo' | 'attack'

export interface Puzzle {
  id: string
  cat: PuzzleCat
  tip: TipId
  /** Чем атакует соперник - для иконки и объяснений. */
  enemyCard: string
  /** Карта на плитке задачи, если не карта соперника (задача про приём). */
  icon?: string
  hand: string[]
  sc: Scenario
  /**
   * Эталонный ход на три звезды - его показываем перед попыткой (проверен прогоном боя).
   * ability - через сколько секунд после хода нажать способность Героя.
   */
  solution: { card: string; x: number; y: number; ability?: number }
  /** Герой уже на поле по условию: ход игрока - только вовремя нажать способность. */
  abilityOnly?: boolean
  /**
   * Комбо в несколько ходов: что делать после первого. pause - когда снова ход игрока
   * (since - секунда предыдущего хода), move - эталон этого хода для показа решения.
   */
  combo?: { pause: (sim: Sim, since: number) => boolean; move: { card: string; x: number; y: number } }[]
}

/** Куда встаёт карта: здание 2×2 - на угол клеток, войско - в центр клетки, заклинание - куда тапнули. */
export function snap(card: string, x: number, y: number): { x: number; y: number } {
  const kind = cardInfo(card).kind
  if (kind === 'building') return { x: Math.min(17, Math.max(1, Math.round(x))), y: Math.min(31, Math.max(18, Math.round(y))) }
  if (kind === 'spell') return { x: Math.round(x * 2) / 2, y: Math.round(y * 2) / 2 }
  return { x: Math.min(17.5, Math.max(0.5, Math.floor(x) + 0.5)), y: Math.floor(y) + 0.5 }
}

/** Пауза, когда юнит соперника дошёл до ряда y. */
const reach = (card: string, y: number) => (sim: Sim) =>
  sim.entities.some(e => e.alive && e.team === 'enemy' && e.card === card && e.deploy <= 0 && e.y >= y)

/** Пауза, когда снаряд соперника пролетел долю пути. */
const flown = (part: number) => (sim: Sim) => sim.projectiles.some(p => {
  if (p.team !== 'enemy') return false
  const all = Math.hypot(p.tx - p.sx, p.ty - p.sy) || 1
  return 1 - Math.hypot(p.tx - p.x, p.ty - p.y) / all >= part
})

/** Пауза, когда Шахтёр вылез из-под земли. */
const surfaced = (sim: Sim) => sim.entities.some(e => e.alive && e.team === 'enemy' && e.card === 'Miner' && !e.tunnel)

/** Любой свой юнит на поле (не под землёй) перешёл ряд y - идёт к сопернику. */
const mineCrossed = (y: number) => (sim: Sim) =>
  sim.entities.some(e => e.alive && e.team === 'me' && e.deploy <= 0 && !e.tunnel && e.y <= y)

/** Соперник кидает Бревно в первую кучу мелочи, перешедшую на его сторону. */
const logFirstSwarm = (sim: Sim) => {
  const g = sim.entities.find(e => e.alive && e.team === 'me' && (e.card === 'Goblins' || e.card === 'Skeletons') && e.deploy <= 0 && e.y < 14)
  return g ? { card: 'The Log', x: g.x, y: Math.max(1, g.y - 2.5) } : null
}

/** Соперник ставит Мушкетёра за башню той линии, куда летит Шар, как только тот перешёл реку. */
const musketeerOnBalloon = (sim: Sim) => {
  const b = sim.entities.find(e => e.alive && e.team === 'me' && e.card === 'Balloon' && e.deploy <= 0 && e.y <= 13)
  return b ? { card: 'Musketeer', x: b.x < 9 ? 3.5 : 14.5, y: 3.5 } : null
}

/** Соперник встречает первый перешедший реку юнит Гоблинами прямо перед ним. */
const goblinsOnFirst = (sim: Sim) => {
  const u = sim.entities.find(e => e.alive && e.team === 'me' && e.kind === 'troop' && e.deploy <= 0 && !e.tunnel && e.y < 12)
  return u ? { card: 'Goblins', x: u.x, y: Math.max(1.5, u.y - 2) } : null
}

/** Пауза, когда соперник поставил ответ этой картой. */
const answered = (card: string, wait: number) => (sim: Sim, since: number) =>
  sim.entities.some(e => e.alive && e.team === 'enemy' && e.card === card) || sim.t >= since + wait

/** Пауза, когда свой юнит из условия дошёл до ряда y (идёт вверх, к сопернику). */
const mineAt = (card: string, y: number) => (sim: Sim) =>
  sim.entities.some(e => e.alive && e.team === 'me' && e.card === card && e.deploy <= 0 && e.y <= y)

export const PUZZLES: Puzzle[] = [
  {
    // Хог с самого края моста обходит пушку в «4-3» (центр); пушка на клетку ближе к башне - «4-2» - его тянет
    id: 'hog_edge', cat: 'defense', tip: 'anti_hog', enemyCard: 'Hog Rider', hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Cannon', x: 7, y: 21 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 0.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [100, 400] },
  },
  {
    // Торнадо на вторую клетку перед королём со сдвигом к башне принцессы, когда Хог подходит к ней:
    // Хога затягивает в радиус короля - король просыпается и вместе с принцессой сбивает его
    id: 'tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Hog Rider', icon: 'Tornado', hand: ['Tornado', 'Cannon', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 7.5, y: 26 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 3.5, y: 14 }], pause: reach('Hog Rider', 21.5), horizon: 14, limits: [350, 700], goal: 'king' },
  },
  {
    id: 'mk', cat: 'defense', tip: 'kite', enemyCard: 'Mega Knight', hand: ['Skeletons', 'Mini P.E.K.K.A', 'Musketeer', 'Fireball'],
    solution: { card: 'Mini P.E.K.K.A', x: 3.5, y: 18.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mega Knight', x: 3.5, y: 13.5 }], pause: reach('Mega Knight', 17.6), horizon: 16, limits: [100, 600] },
  },
  {
    // Шар с края обходит центральное здание; пушка правее центра у реки перехватывает его
    id: 'balloon_edge', cat: 'defense', tip: 'anti_air', enemyCard: 'Balloon', hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Cannon', x: 10, y: 21 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Balloon', x: 17.5, y: 13.5 }], pause: reach('Balloon', 18.5), horizon: 14, limits: [100, 450] },
  },
  {
    // ПЕККА сносит башню целиком; Ледяной голем у середины реки, когда она перешла мост, уводит её в другую линию
    id: 'pekka_kite', cat: 'defense', tip: 'kite', enemyCard: 'P.E.K.K.A', icon: 'Ice Golem', hand: ['Ice Golem', 'Knight', 'Skeletons', 'Mini P.E.K.K.A'],
    solution: { card: 'Ice Golem', x: 9.5, y: 18.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'P.E.K.K.A', x: 3.5, y: 13.5 }], pause: reach('P.E.K.K.A', 17.5), horizon: 30, limits: [100, 1200] },
  },
  {
    // Мортира не стреляет ближе 3,5 клетки: юнит вплотную ломает её, а она бьёт только по башне
    id: 'mortar', cat: 'defense', tip: 'anti_siege', enemyCard: 'Mortar', hand: ['Knight', 'Skeletons', 'Fireball', 'Cannon'],
    solution: { card: 'Knight', x: 14.5, y: 17.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mortar', x: 14.5, y: 12.5 }], pause: 0.6, horizon: 18, limits: [300, 600] },
  },
  {
    // Бочка в центр правой башни; Торнадо между башней и королём за полсекунды до приземления
    // затягивает гоблинов в радиус короля: король просыпается, башня цела
    id: 'barrel_tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Goblin Barrel', hand: ['Tornado', 'The Log', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 11.5, y: 26.5 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Goblin Barrel', x: 14.5, y: 25.5 }], pause: flown(0.86), horizon: 10, limits: [60, 130], goal: 'king' },
  },
  // --- Король: Торнадо затягивает к королю не только Хога ---
  {
    // Мини-ПЕККА у правой башни: Торнадо перед королём на две клетки вправо
    id: 'mpk_tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Mini P.E.K.K.A', icon: 'Tornado', hand: ['Tornado', 'Skeletons', 'Knight', 'Fireball'],
    solution: { card: 'Tornado', x: 11, y: 26 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Mini P.E.K.K.A', x: 14.5, y: 13.5 }], pause: reach('Mini P.E.K.K.A', 21.5), horizon: 14, limits: [350, 800], goal: 'king' },
  },
  {
    // Шахтёр вылез у левой башни: Торнадо между башней и королём тянет его в радиус короля
    id: 'miner_tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Miner', icon: 'Tornado', hand: ['Tornado', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 8.5, y: 24 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Miner', x: 3.5, y: 23.5 }], pause: surfaced, horizon: 14, limits: [250, 600], goal: 'king' },
  },
  {
    // Гигант бьёт пушку в центре - Торнадо перед королём к правой стороне: король просыпается
    id: 'giant_tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Giant', icon: 'Tornado', hand: ['Tornado', 'Skeletons', 'Mini P.E.K.K.A', 'Fireball'],
    solution: { card: 'Tornado', x: 10.5, y: 25.5 },
    sc: { elixir: 6, mine: [{ at: 0, card: 'Cannon', x: 9, y: 22 }], enemy: [{ at: 0.5, card: 'Giant', x: 14.5, y: 13.5 }], pause: reach('Giant', 20), horizon: 18, limits: [100, 700], goal: 'king' },
  },
  {
    // Торнадо тянет и летающих: Шар над левой линией - в радиус короля
    id: 'loon_tornado', cat: 'king', tip: 'king_tornado', enemyCard: 'Balloon', icon: 'Tornado', hand: ['Tornado', 'Archers', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 8.5, y: 23.5 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Balloon', x: 3.5, y: 13.5 }], pause: reach('Balloon', 20.5), horizon: 14, limits: [250, 700], goal: 'king' },
  },
  // --- Дёшево: отбейся за 1-2 эликсира против 3-7 ---
  {
    // Скелеты на пути Мини-ПЕККА: она бьёт по одному, башня добивает - 1 против 4
    id: 'mpk_skel', cat: 'cheap', tip: 'cheap_def', enemyCard: 'Mini P.E.K.K.A', icon: 'Skeletons', hand: ['Skeletons', 'Knight', 'Mini P.E.K.K.A', 'Fireball'],
    solution: { card: 'Skeletons', x: 3.5, y: 19.5 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Mini P.E.K.K.A', x: 3.5, y: 13.5 }], pause: reach('Mini P.E.K.K.A', 17.4), horizon: 14, limits: [150, 600], cost: 2 },
  },
  {
    // Мыши на пути Хога: он бьёт только здания, пять мышей и башня сбивают его - 2 против 4
    id: 'hog_bats', cat: 'cheap', tip: 'cheap_def', enemyCard: 'Hog Rider', icon: 'Bats', hand: ['Bats', 'Skeletons', 'Knight', 'Fireball'],
    solution: { card: 'Bats', x: 14.5, y: 20.5 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Hog Rider', x: 14.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [330, 800], cost: 2 },
  },
  {
    // Ледяной голем в центр: Мега-рыцарь идёт за ним между башнями, обе башни бьют - 2 против 7
    id: 'mk_icegolem', cat: 'cheap', tip: 'kite', enemyCard: 'Mega Knight', icon: 'Ice Golem', hand: ['Ice Golem', 'Skeletons', 'Knight', 'Fireball'],
    solution: { card: 'Ice Golem', x: 8.5, y: 18.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mega Knight', x: 3.5, y: 13.5 }], pause: reach('Mega Knight', 17.6), horizon: 22, limits: [300, 1500], cost: 2 },
  },
  {
    // Бревно катится вперёд: брошенное за башню, оно проходит через место приземления бочки,
    // когда гоблины только вылезли, - и снимает всех; прямо на бочку или перед башней - мимо
    id: 'barrel_log', cat: 'cheap', tip: 'cheap_def', enemyCard: 'Goblin Barrel', icon: 'The Log', hand: ['The Log', 'Skeletons', 'Knight', 'Fireball'],
    solution: { card: 'The Log', x: 14.5, y: 28 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Goblin Barrel', x: 14.5, y: 25.5 }], pause: flown(0.86), horizon: 10, limits: [60, 300], cost: 2 },
  },
  // --- Защита: танки ---
  // --- Атака: свой ход, чтобы снести башню соперника ---
  {
    // Гигант впереди держит башню и Мини-ПЕККА соперника, Шар за ним долетает до башни
    id: 'atk_loon_giant', cat: 'attack', tip: 'tank_support', enemyCard: 'Mini P.E.K.K.A', icon: 'Balloon', hand: ['Balloon', 'Musketeer', 'Skeletons', 'Fireball'],
    solution: { card: 'Balloon', x: 3.5, y: 18.5 },
    sc: { elixir: 6, mine: [{ at: 0, card: 'Giant', x: 3.5, y: 18.5 }], enemy: [{ at: 5.6, card: 'Mini P.E.K.K.A', x: 3.5, y: 10 }], pause: mineAt('Giant', 15.4), horizon: 22, limits: [2500, 1300], goal: 'attack' },
  },
  // --- Комбо: атака в несколько ходов, соперник отвечает на них ---
  {
    // Шар на мост, Шахтёр на башню, как только Шар перешёл реку: Шахтёр под землёй быстрее и
    // приходит первым - башня и Мушкетёр бьют его, а Шар долетает. Шахтёр раньше - умрёт до Шара
    id: 'combo_loon_miner', cat: 'attack', tip: 'loon_miner', enemyCard: 'Musketeer', icon: 'Balloon', hand: ['Balloon', 'Miner', 'Fireball', 'The Log'],
    solution: { card: 'Balloon', x: 14.5, y: 17.5 },
    combo: [{ pause: (s, since) => mineCrossed(16)(s) || s.t >= since + 4, move: { card: 'Miner', x: 14.5, y: 9 } }],
    sc: { elixir: 8, enemy: [], react: [musketeerOnBalloon], pause: 0.3, horizon: 22, limits: [1100, 600], goal: 'attack' },
  },
  {
    // У соперника Бревно, и он кинет его в первую мелочь: сначала дешёвая приманка у моста,
    // а Бочку - когда Бревно уже ушло. Бочка первой - Бревно её и встретит
    id: 'combo_log_bait', cat: 'attack', tip: 'log_bait', enemyCard: 'The Log', icon: 'Goblin Barrel', hand: ['Goblin Barrel', 'Skeletons', 'Goblins', 'Ice Spirit'],
    solution: { card: 'Skeletons', x: 14.5, y: 17.5 },
    combo: [{ pause: (s, since) => s.rollers.some(r => r.team === 'enemy') || s.t >= since + 5, move: { card: 'Goblin Barrel', x: 15.5, y: 6 } }],
    sc: { elixir: 5, enemy: [], react: [logFirstSwarm], pause: 0.3, horizon: 18, limits: [300, 200], goal: 'attack' },
  },
  {
    // Хог на мост, соперник встречает его Гоблинами; Бревно - сразу по ним, а не заранее:
    // Гоблины сносят Хога за пару ударов, без них он бьёт башню до конца
    id: 'combo_hog_log', cat: 'attack', tip: 'hog_log', enemyCard: 'Goblins', icon: 'Hog Rider', hand: ['Hog Rider', 'The Log', 'Ice Spirit', 'Skeletons'],
    solution: { card: 'Hog Rider', x: 14.5, y: 17.5 },
    combo: [{ pause: answered('Goblins', 5), move: { card: 'The Log', x: 14.5, y: 13 } }],
    sc: { elixir: 5, enemy: [], react: [goblinsOnFirst], pause: 0.3, horizon: 20, limits: [1500, 900], goal: 'attack' },
  },
  // --- Герои: поставить и вовремя нажать способность ---
  {
    // Мини-ПЕККА и Мушкетёр в одной линии: Герой Рыцарь в центре, насмешка - оба бьют его щит под двумя башнями
    id: 'hero_knight_def', cat: 'hero', tip: 'heroes', enemyCard: 'Mini P.E.K.K.A', icon: 'Hero Knight', hand: ['Hero Knight', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Hero Knight', x: 9.5, y: 21.5, ability: 0.5 },
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Mini P.E.K.K.A', x: 3.5, y: 13.5 }, { at: 0.8, card: 'Musketeer', x: 3.5, y: 11.5 }], pause: reach('Mini P.E.K.K.A', 17.4), horizon: 18, limits: [100, 1000] },
  },
  {
    // Твой Хог идёт на башню с Мушкетёром: Герой Рыцарь рядом, насмешка у башни - башня и Мушкетёр бьют щит, Хог ломает башню
    id: 'hero_knight_atk', cat: 'hero', tip: 'heroes', enemyCard: 'Musketeer', icon: 'Hero Knight', hand: ['Hero Knight', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Hero Knight', x: 14.5, y: 17.5, ability: 2.5 },
    sc: { elixir: 7, mine: [{ at: 0, card: 'Hog Rider', x: 14.5, y: 18.5 }], enemy: [{ at: 2.2, card: 'Musketeer', x: 12.5, y: 8.5 }], pause: 0.3, horizon: 16, limits: [1300, 700], goal: 'attack' },
  },
  {
    // Герой Мини-ПЕККА против ПЕККИ: блины после двух-трёх ударов - +2-3 уровня и лечение, раньше мало, позже поздно
    id: 'hero_mpk_boost', cat: 'hero', tip: 'heroes', enemyCard: 'P.E.K.K.A', icon: 'Hero Mini P.E.K.K.A', hand: [], abilityOnly: true,
    solution: { card: 'Hero Mini P.E.K.K.A', x: 0, y: 0, ability: 4.25 },
    sc: { elixir: 3, mine: [{ at: 0.2, card: 'Hero Mini P.E.K.K.A', x: 3.5, y: 19.5 }], enemy: [{ at: 0, card: 'P.E.K.K.A', x: 3.5, y: 13.5 }], pause: 0.3, horizon: 26, limits: [150, 1600] },
  },
  {
    // Шар с Миньонами: Герой Мушкетёр у реки и турель сразу - вместе снимают и рой, и Шар
    id: 'hero_musk_turret', cat: 'hero', tip: 'heroes', enemyCard: 'Balloon', icon: 'Hero Musketeer', hand: ['Hero Musketeer', 'Musketeer', 'Knight', 'Fireball'],
    solution: { card: 'Hero Musketeer', x: 10.5, y: 17.5, ability: 0 },
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Balloon', x: 14.5, y: 13.5 }, { at: 0.5, card: 'Minions', x: 14.5, y: 12.5 }], pause: reach('Balloon', 17.5), horizon: 16, limits: [100, 1000] },
  },
  {
    // Твой Герой Гигант идёт на башню, его встретит Мега-рыцарь: бросок заранее - схватит его и кинет в другую линию
    id: 'hero_giant_hurl', cat: 'hero', tip: 'heroes', enemyCard: 'Mega Knight', icon: 'Hero Giant', hand: [], abilityOnly: true,
    solution: { card: 'Hero Giant', x: 0, y: 0, ability: 2 },
    sc: { elixir: 3, mine: [{ at: 0, card: 'Hero Giant', x: 14.5, y: 18.5 }], enemy: [{ at: 4.8, card: 'Mega Knight', x: 14.5, y: 9 }], pause: 0.6, horizon: 26, limits: [2200, 1500], goal: 'attack' },
  },
  // --- Эволюции: то, что обычная карта здесь не вытянет ---
  {
    // ПЕККА бьёт по одному, а эво-скелеты делают копию после каждого удара - она вязнет в них под башней
    id: 'evo_skel_pekka', cat: 'evo', tip: 'evos', enemyCard: 'P.E.K.K.A', icon: 'Evo Skeletons', hand: ['Evo Skeletons', 'Skeletons', 'Knight', 'Fireball'],
    solution: { card: 'Evo Skeletons', x: 3.5, y: 18.5 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'P.E.K.K.A', x: 3.5, y: 13.5 }], pause: reach('P.E.K.K.A', 17.5), horizon: 30, limits: [300, 1500], cost: 2 },
  },
  {
    // Каждый удар эво-Валькирии - вихрь: Хога и гоблинов затягивает к ней, и они не доходят до башни
    id: 'evo_valk_hog', cat: 'evo', tip: 'evos', enemyCard: 'Hog Rider', icon: 'Evo Valkyrie', hand: ['Evo Valkyrie', 'Valkyrie', 'Knight', 'Fireball'],
    solution: { card: 'Evo Valkyrie', x: 13.5, y: 20.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Hog Rider', x: 14.5, y: 14 }, { at: 0.2, card: 'Goblins', x: 15.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [150, 800] },
  },
  {
    // Эво-Рыцарь на ходу получает на 60% меньше: он доходит до Мушкетёра почти целым и держит обоих
    id: 'evo_knight_def', cat: 'evo', tip: 'evos', enemyCard: 'Mini P.E.K.K.A', icon: 'Evo Knight', hand: ['Evo Knight', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Evo Knight', x: 7.5, y: 24.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mini P.E.K.K.A', x: 3.5, y: 13.5 }, { at: 0.8, card: 'Musketeer', x: 3.5, y: 11.5 }], pause: reach('Mini P.E.K.K.A', 17.4), horizon: 18, limits: [100, 1000] },
  },
  {
    // Апперкот эво-Мега-рыцаря отшвыривает Мушкетёра на 4 клетки - Гигант остаётся без поддержки
    id: 'evo_mk_uppercut', cat: 'evo', tip: 'evos', enemyCard: 'Giant', icon: 'Evo Mega Knight', hand: ['Evo Mega Knight', 'Mega Knight', 'Mini P.E.K.K.A', 'Fireball'],
    solution: { card: 'Evo Mega Knight', x: 14.5, y: 17.5 },
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Giant', x: 14.5, y: 13.5 }, { at: 1.5, card: 'Musketeer', x: 14.5, y: 12.5 }], pause: reach('Giant', 17.4), horizon: 22, limits: [400, 1600] },
  },
  {
    // Эво-ПЕККА лечится за каждое убийство: гоблины и Мушкетёр подлечивают её, и она дотягивает до Мега-рыцаря
    id: 'evo_pekka_heal', cat: 'evo', tip: 'evos', enemyCard: 'Mega Knight', icon: 'Evo P.E.K.K.A', hand: ['Evo P.E.K.K.A', 'P.E.K.K.A', 'Knight', 'Fireball'],
    solution: { card: 'Evo P.E.K.K.A', x: 3.5, y: 17.5 },
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Mega Knight', x: 3.5, y: 13.5 }, { at: 0.5, card: 'Musketeer', x: 3.5, y: 11.5 }, { at: 1.0, card: 'Goblins', x: 4.5, y: 13 }], pause: reach('Mega Knight', 17.6), horizon: 24, limits: [200, 2000] },
  },
  {
    // Эво-Лучницы бьют ×1,5 по целям дальше 4 клеток: поставленные в стороне от линии Шара, они
    // стреляют издалека усиленными выстрелами и сбивают его до башни; обычные Лучницы - не успевают
    id: 'evo_archers_far', cat: 'evo', tip: 'evos', enemyCard: 'Balloon', icon: 'Evo Archers', hand: ['Evo Archers', 'Archers', 'Knight', 'Fireball'],
    solution: { card: 'Evo Archers', x: 9.5, y: 22.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Balloon', x: 14.5, y: 13.5 }], pause: reach('Balloon', 17.5), horizon: 14, limits: [100, 700] },
  },
]

/** Сколько задач открыто целиком без Плюса (плюс задача дня); в остальных бесплатно только показ решения. */
export const FREE_PUZZLES = 1

const SANDBOX_KEY = 'cwst_sandbox_day'

/** Бесплатная песочница - раз в день: был ли уже сегодня запуск. */
export function sandboxUsedToday(now = new Date()): boolean {
  try { return localStorage.getItem(SANDBOX_KEY) === now.toISOString().slice(0, 10) } catch { return false }
}

export function markSandboxUsed(now = new Date()) {
  try { localStorage.setItem(SANDBOX_KEY, now.toISOString().slice(0, 10)) } catch { /* без памяти - не считаем */ }
}

/** Задача дня - по кругу, одна на всех: можно сравнить с кланом. */
export function dailyIndex(now = new Date()): number {
  const day = Math.floor(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) / 86400000)
  return day % PUZZLES.length
}

const STARS_KEY = 'cwst_trainer_stars'
const STREAK_KEY = 'cwst_trainer_streak'

export function loadStars(): Record<string, number> {
  try { return JSON.parse(localStorage.getItem(STARS_KEY) ?? '{}') } catch { return {} }
}

export function saveStars(id: string, stars: number): Record<string, number> {
  const all = loadStars()
  if ((all[id] ?? 0) < stars) all[id] = stars
  try { localStorage.setItem(STARS_KEY, JSON.stringify(all)) } catch { /* без памяти - без звёзд */ }
  return all
}

/** Серия дней с решённой задачей дня. */
export function loadStreak(): { days: number; last: string | null } {
  try { return JSON.parse(localStorage.getItem(STREAK_KEY) ?? '{"days":0,"last":null}') } catch { return { days: 0, last: null } }
}

export function markDailySolved(now = new Date()): { days: number; last: string } {
  const today = now.toISOString().slice(0, 10)
  const s = loadStreak()
  if (s.last === today) return { days: s.days, last: today }
  const yesterday = new Date(now.getTime() - 86400000).toISOString().slice(0, 10)
  const next = { days: s.last === yesterday ? s.days + 1 : 1, last: today }
  try { localStorage.setItem(STREAK_KEY, JSON.stringify(next)) } catch { /* пусть так */ }
  return next
}

export interface PuzzleText { title: string; task: string }

export const PUZZLE_TEXT: Record<Lang, Record<string, PuzzleText>> = {
  ru: {
    mk: { title: 'Мега-рыцарь у моста', task: 'Мега-рыцарь перешёл мост и идёт на левую башню. Останови его.' },
    hog_edge: { title: 'Хог с края моста', task: 'Хог прыгает с самого края. Пушка в центре его не дотянет — найди клетку, которая дотянет.' },
    balloon_edge: { title: 'Шар с края', task: 'Шар летит по самому краю. Перехвати его зданием, пока он не долетел до башни.' },
    pekka_kite: { title: 'ПЕККА: уведи в другую линию', task: 'ПЕККА перешла мост — без защиты она снесёт башню. Уведи её туда, где её добьют.' },
    mortar: { title: 'Мортира у моста', task: 'Соперник поставил Мортиру. Сломай её, не подставляя свои карты под её выстрелы.' },
    tornado: { title: 'Торнадо будит короля', task: 'Хог подходит к левой башне. Разбуди короля Торнадо — пусть башни вдвоём собьют Хога.' },
    barrel_tornado: { title: 'Бочка: Торнадо к королю', task: 'Бочка летит на правую башню. Затяни гоблинов к королю Торнадо — разбуди его.' },
    mpk_tornado: { title: 'Мини-ПЕККА: Торнадо к королю', task: 'Мини-ПЕККА подходит к правой башне. Разбуди короля Торнадо, чтобы башни сбили её вдвоём.' },
    miner_tornado: { title: 'Шахтёр: Торнадо к королю', task: 'Шахтёр вылез у левой башни. Затяни его к королю Торнадо — король проснётся и добьёт.' },
    giant_tornado: { title: 'Гигант на пушке: буди короля', task: 'Гигант бьёт пушку в центре. Разбуди короля Торнадо — он пригодится до конца боя.' },
    loon_tornado: { title: 'Шар: Торнадо к королю', task: 'Шар летит на левую башню. Торнадо тянет и летающих — затяни его к королю.' },
    mpk_skel: { title: 'Мини-ПЕККА за 1 эликсир', task: 'Мини-ПЕККА перешла мост. Отбейся дешевле, чем за 2 эликсира.' },
    hog_bats: { title: 'Хог за 2 эликсира', task: 'Хог идёт на правую башню. Отбейся за 2 эликсира — без здания.' },
    mk_icegolem: { title: 'Мега-рыцарь за 2 эликсира', task: 'Мега-рыцарь перешёл мост. Отбейся за 2 эликсира — уведи его под обе башни.' },
    atk_loon_giant: { title: 'Атака: Шар за Гигантом', task: 'Твой Гигант перешёл реку, соперник встретит его Мини-ПЕККА. Добавь карту так, чтобы снести башню.' },
    combo_loon_miner: { title: 'Комбо: Шахтёр + Шар', task: 'Соперник встретит Шар Мушкетёром за башней. Снеси башню Шаром — в два хода, вторую карту выбери сам.' },
    combo_log_bait: { title: 'Комбо: приманка для Бревна', task: 'У соперника Бревно, и он кинет его в первую мелочь. Доведи Бочку до башни — в два хода.' },
    combo_hog_log: { title: 'Комбо: Хог + Бревно', task: 'Соперник встретит Хога Гоблинами у башни. Доведи Хога до башни — в два хода, второй сделай, когда увидишь ответ.' },
    hero_knight_def: { title: 'Герой Рыцарь: насмешка', task: 'Мини-ПЕККА с Мушкетёром идут на левую башню. Поставь Героя Рыцаря и вовремя включи насмешку.' },
    hero_knight_atk: { title: 'Атака: насмешка на башню', task: 'Твой Хог идёт на правую башню, у соперника там Мушкетёр. Поставь Героя Рыцаря и забери огонь на него.' },
    hero_mpk_boost: { title: 'Герой Мини-ПЕККА: блины', task: 'Твоя Мини-ПЕККА встречает ПЕККУ. Нажми блины в нужный момент — не раньше и не позже.' },
    hero_musk_turret: { title: 'Герой Мушкетёр: турель', task: 'Шар с Миньонами летят на правую башню. Поставь Героя Мушкетёра и турель.' },
    hero_giant_hurl: { title: 'Атака: бросок Гиганта', task: 'Твой Герой Гигант идёт на башню, его встретит Мега-рыцарь. Нажми бросок.' },
    evo_skel_pekka: { title: 'Эво-Скелеты против ПЕККИ', task: 'ПЕККА перешла мост. Отбейся за 1 эликсир — у тебя эво-скелеты.' },
    evo_valk_hog: { title: 'Эво-Валькирия: вихрь', task: 'Хог с гоблинами идут на правую башню. Поставь эво-Валькирию так, чтобы вихрь их затянул.' },
    evo_knight_def: { title: 'Эво-Рыцарь: щит на ходу', task: 'Мини-ПЕККА с Мушкетёром идут на левую башню. Удержи их эво-Рыцарем.' },
    evo_mk_uppercut: { title: 'Эво-Мега-рыцарь: апперкот', task: 'Гигант с Мушкетёром в правой линии. Встреть их эво-Мега-рыцарем.' },
    evo_pekka_heal: { title: 'Эво-ПЕККА: лечение за убийства', task: 'Мега-рыцарь, Мушкетёр и гоблины — большой пуш. Выстой эво-ПЕККОЙ.' },
    barrel_log: { title: 'Бочка: Бревно из-за башни', task: 'Бочка летит на правую башню. Сними гоблинов Бревном за 2 эликсира так, чтобы башня почти не пострадала.' },
    evo_archers_far: { title: 'Эво-Лучницы: стреляй издалека', task: 'Шар летит на правую башню. Поставь эво-Лучниц так, чтобы они сбили его до удара.' },
  },
  uk: {
    mk: { title: 'Мега-лицар біля мосту', task: 'Мега-лицар перейшов міст і йде на ліву вежу. Зупини його.' },
    hog_edge: { title: 'Хог з краю мосту', task: 'Хог стрибає з самого краю. Гармата в центрі його не дотягне — знайди клітинку, яка дотягне.' },
    balloon_edge: { title: 'Куля з краю', task: 'Куля летить самим краєм. Перехопи її будівлею, поки вона не долетіла до вежі.' },
    pekka_kite: { title: 'ПЕККА: відведи в іншу лінію', task: 'ПЕККА перейшла міст — без захисту вона знесе вежу. Відведи її туди, де її доб\'ють.' },
    mortar: { title: 'Мортира біля мосту', task: 'Суперник поставив Мортиру. Злам її, не підставляючи свої карти під її постріли.' },
    tornado: { title: 'Торнадо будить короля', task: 'Хог підходить до лівої вежі. Розбуди короля Торнадо — хай вежі вдвох зіб\'ють Хога.' },
    barrel_tornado: { title: 'Бочка: Торнадо до короля', task: 'Бочка летить на праву вежу. Затягни гоблінів до короля Торнадо — розбуди його.' },
    mpk_tornado: { title: 'Міні-ПЕККА: Торнадо до короля', task: 'Міні-ПЕККА підходить до правої вежі. Розбуди короля Торнадо, щоб вежі зібили її вдвох.' },
    miner_tornado: { title: 'Шахтар: Торнадо до короля', task: 'Шахтар виліз біля лівої вежі. Затягни його до короля Торнадо — король прокинеться й доб\'є.' },
    giant_tornado: { title: 'Гігант на гарматі: буди короля', task: 'Гігант б\'є гармату в центрі. Розбуди короля Торнадо — він знадобиться до кінця бою.' },
    loon_tornado: { title: 'Куля: Торнадо до короля', task: 'Куля летить на ліву вежу. Торнадо тягне й летючих — затягни її до короля.' },
    mpk_skel: { title: 'Міні-ПЕККА за 1 еліксир', task: 'Міні-ПЕККА перейшла міст. Відбийся дешевше, ніж за 2 еліксиру.' },
    hog_bats: { title: 'Хог за 2 еліксиру', task: 'Хог іде на праву вежу. Відбийся за 2 еліксиру — без будівлі.' },
    mk_icegolem: { title: 'Мега-лицар за 2 еліксиру', task: 'Мега-лицар перейшов міст. Відбийся за 2 еліксиру — відведи його під обидві вежі.' },
    atk_loon_giant: { title: 'Атака: Куля за Гігантом', task: 'Твій Гігант перейшов річку, суперник зустріне його Міні-ПЕККА. Додай карту так, щоб знести вежу.' },
    combo_loon_miner: { title: 'Комбо: Шахтар + Куля', task: 'Суперник зустріне Кулю Мушкетеркою за вежею. Знеси вежу Кулею — за два ходи, другу карту обери сам.' },
    combo_log_bait: { title: 'Комбо: приманка для Колоди', task: 'У суперника Колода, і він кине її в першу дрібноту. Доведи Бочку до вежі — за два ходи.' },
    combo_hog_log: { title: 'Комбо: Хог + Колода', task: 'Суперник зустріне Хога Гоблінами біля вежі. Доведи Хога до вежі — за два ходи, другий зроби, коли побачиш відповідь.' },
    hero_knight_def: { title: 'Герой Лицар: глузування', task: 'Міні-ПЕККА з Мушкетеркою йдуть на ліву вежу. Постав Героя Лицаря й вчасно ввімкни глузування.' },
    hero_knight_atk: { title: 'Атака: глузування на вежу', task: 'Твій Хог іде на праву вежу, у суперника там Мушкетерка. Постав Героя Лицаря й забери вогонь на нього.' },
    hero_mpk_boost: { title: 'Герой Міні-ПЕККА: млинці', task: 'Твоя Міні-ПЕККА зустрічає ПЕККУ. Натисни млинці в потрібний момент — не раніше й не пізніше.' },
    hero_musk_turret: { title: 'Герой Мушкетерка: турель', task: 'Куля з Міньйонами летять на праву вежу. Постав Героя Мушкетерку й турель.' },
    hero_giant_hurl: { title: 'Атака: кидок Гіганта', task: 'Твій Герой Гігант іде на вежу, його зустріне Мега-лицар. Натисни кидок.' },
    evo_skel_pekka: { title: 'Ево-Скелети проти ПЕККИ', task: 'ПЕККА перейшла міст. Відбийся за 1 еліксир — у тебе ево-скелети.' },
    evo_valk_hog: { title: 'Ево-Валькірія: вихор', task: 'Хог із гоблінами йдуть на праву вежу. Постав ево-Валькірію так, щоб вихор їх затягнув.' },
    evo_knight_def: { title: 'Ево-Лицар: щит на ходу', task: 'Міні-ПЕККА з Мушкетеркою йдуть на ліву вежу. Утримай їх ево-Лицарем.' },
    evo_mk_uppercut: { title: 'Ево-Мега-лицар: апперкот', task: 'Гігант із Мушкетеркою в правій лінії. Зустрінь їх ево-Мега-лицарем.' },
    evo_pekka_heal: { title: 'Ево-ПЕККА: лікування за вбивства', task: 'Мега-лицар, Мушкетерка й гобліни — великий пуш. Вистій ево-ПЕККОЮ.' },
    barrel_log: { title: 'Бочка: Колода з-за вежі', task: 'Бочка летить на праву вежу. Зніми гоблінів Колодою за 2 еліксири так, щоб вежа майже не постраждала.' },
    evo_archers_far: { title: 'Ево-Лучниці: стріляй здалеку', task: 'Куля летить на праву вежу. Постав ево-Лучниць так, щоб вони збили її до удару.' },
  },
  en: {
    mk: { title: 'Mega Knight at the bridge', task: 'The Mega Knight crossed the bridge toward your left tower. Stop it.' },
    hog_edge: { title: 'Hog from the edge', task: 'The Hog jumps from the very edge. A middle Cannon won\'t pull it — find the tile that will.' },
    balloon_edge: { title: 'Balloon on the edge', task: 'The Balloon flies along the edge. Catch it with a building before it reaches your tower.' },
    pekka_kite: { title: 'P.E.K.K.A: pull to the other lane', task: 'P.E.K.K.A crossed the bridge — undefended it takes your tower. Pull it where it gets finished.' },
    mortar: { title: 'Mortar at the bridge', task: 'They placed a Mortar. Break it without putting your cards under its shots.' },
    tornado: { title: 'Tornado wakes the King', task: 'The Hog nears your left tower. Wake your King with Tornado — let both towers take the Hog down.' },
    barrel_tornado: { title: 'Barrel: Tornado to the King', task: 'A barrel flies at your right tower. Pull the goblins to your King with Tornado — wake him up.' },
    mpk_tornado: { title: 'Mini PEKKA: Tornado to the King', task: 'Mini PEKKA nears your right tower. Wake your King with Tornado so both towers take it down.' },
    miner_tornado: { title: 'Miner: Tornado to the King', task: 'The Miner popped up at your left tower. Pull it to your King with Tornado — he wakes and finishes it.' },
    giant_tornado: { title: 'Giant on the Cannon: wake the King', task: 'The Giant is hitting your middle Cannon. Wake your King with Tornado — he helps for the rest of the match.' },
    loon_tornado: { title: 'Balloon: Tornado to the King', task: 'A Balloon flies at your left tower. Tornado pulls air units too — drag it to your King.' },
    mpk_skel: { title: 'Mini PEKKA for 1 elixir', task: 'Mini PEKKA crossed the bridge. Defend for less than 2 elixir.' },
    hog_bats: { title: 'Hog for 2 elixir', task: 'The Hog goes for your right tower. Defend for 2 elixir — no building.' },
    mk_icegolem: { title: 'Mega Knight for 2 elixir', task: 'The Mega Knight crossed the bridge. Defend for 2 elixir — pull it under both towers.' },
    atk_loon_giant: { title: 'Attack: Balloon behind the Giant', task: 'Your Giant crossed the river and they\'ll meet it with Mini PEKKA. Add a card to take the tower.' },
    combo_loon_miner: { title: 'Combo: Miner + Balloon', task: 'They will meet the Balloon with a Musketeer behind the tower. Take the tower with the Balloon — in two moves, you pick the second card.' },
    combo_log_bait: { title: 'Combo: bait the Log', task: 'They have a Log and will throw it at the first swarm. Get the Barrel to the tower — in two moves.' },
    combo_hog_log: { title: 'Combo: Hog + Log', task: 'They will meet the Hog with Goblins at the tower. Get the Hog to the tower — in two moves, the second once you see their answer.' },
    hero_knight_def: { title: 'Hero Knight: taunt', task: 'Mini PEKKA and Musketeer push your left tower. Place the Hero Knight and taunt at the right time.' },
    hero_knight_atk: { title: 'Attack: taunt the tower', task: 'Your Hog goes for their right tower, defended by a Musketeer. Place the Hero Knight and draw the fire.' },
    hero_mpk_boost: { title: 'Hero Mini PEKKA: pancakes', task: 'Your Mini PEKKA meets a P.E.K.K.A. Hit Breakfast Boost at the right moment — not too early, not too late.' },
    hero_musk_turret: { title: 'Hero Musketeer: turret', task: 'Balloon and Minions fly at your right tower. Place the Hero Musketeer and her turret.' },
    hero_giant_hurl: { title: 'Attack: Giant\'s hurl', task: 'Your Hero Giant walks to the tower and a Mega Knight will meet him. Use the hurl.' },
    evo_skel_pekka: { title: 'Evo Skeletons vs P.E.K.K.A', task: 'P.E.K.K.A crossed the bridge. Defend for 1 elixir — you have Evo Skeletons.' },
    evo_valk_hog: { title: 'Evo Valkyrie: the whirlwind', task: 'Hog and Goblins go for your right tower. Place the Evo Valkyrie so her whirlwind pulls them in.' },
    evo_knight_def: { title: 'Evo Knight: shield on the move', task: 'Mini PEKKA and Musketeer push your left tower. Hold them with the Evo Knight.' },
    evo_mk_uppercut: { title: 'Evo Mega Knight: uppercut', task: 'Giant and Musketeer in the right lane. Meet them with the Evo Mega Knight.' },
    evo_pekka_heal: { title: 'Evo P.E.K.K.A: heal on kills', task: 'Mega Knight, Musketeer and Goblins — a big push. Hold with the Evo P.E.K.K.A.' },
    barrel_log: { title: 'Barrel: Log from behind the tower', task: 'A barrel flies at your right tower. Clear the goblins with a 2-elixir Log so the tower barely gets touched.' },
    evo_archers_far: { title: 'Evo Archers: shoot from afar', task: 'A Balloon flies at your right tower. Place the Evo Archers so they shoot it down before it hits.' },
  },
}

/** Приём, который решает задачу, - показываем после боя. */
export const HINTS: Record<Lang, Record<string, string>> = {
  ru: {
    mk: 'Мега-рыцаря держит Мини-ПЕККА: 720 урона за удар. Дешёвых юнитов он давит прыжком.',
    hog_edge: 'Хог с края обходит пушку в «4-3» (центр, 4 клетки от реки). Сдвинь её на клетку к башне — «4-2»: так она в обзоре Хога с любого края.',
    balloon_edge: 'Шар с края не видит здание в центре. Ставь пушку ближе к его краю и к реке — правее центра на 3–4 клетки ниже реки.',
    pekka_kite: 'Ледяной голем бьёт только здания и идёт по своей линии, а ПЕККА бьёт ближайшего и идёт за ним. Ставь его у середины реки, на клетку в другую линию, ровно когда ПЕККА перешла мост.',
    mortar: 'Мортира не стреляет ближе 3,5 клетки. Юнит, дошедший до неё вплотную, ломает её без потерь — она бьёт только по башне.',
    tornado: 'Когда Хог подходит к башне, кидай Торнадо на вторую клетку перед королём, на две клетки к этой башне: Хога затянет вплотную к королю, и король проснётся до конца боя.',
    barrel_tornado: 'Торнадо между башней и королём — за полсекунды до приземления бочки: гоблинов затянет вплотную к королю, король проснётся, а башня останется целой. Раньше нельзя — вихрь кончится до приземления.',
    mpk_tornado: 'Торнадо на вторую клетку перед королём, на две клетки к башне, когда враг уже у неё: его затянет вплотную к королю — король проснётся. Работает против любого бойца ближнего боя.',
    miner_tornado: 'Шахтёр вылезает у самой башни, ему некуда уйти: Торнадо между башней и королём (ближе к королю) затягивает его в радиус короля.',
    giant_tornado: 'Танк, привязанный к зданию в центре, стоит на месте — идеальная цель для Торнадо к королю. Здание держит, Торнадо будит, король стреляет до конца боя.',
    loon_tornado: 'Торнадо тянет и летающих. Кидай его между башней и королём, когда Шар уже над твоей половиной: его утянет к королю, и две башни собьют его до бомбы по башне.',
    mpk_skel: 'Мини-ПЕККА бьёт по одному скелету за удар — трое отнимают у неё три удара, а башня успевает её добить. 1 эликсир против 4.',
    hog_bats: 'Хог бьёт только здания и не отвечает мышам. Пять мышей на его пути плюс башня сбивают его за пару секунд: 2 эликсира против 4.',
    mk_icegolem: 'Мега-рыцарь бьёт ближайшего. Ледяной голем в центре у реки уводит его в середину — там его достают обе башни, а голем при смерти замедляет.',
    atk_loon_giant: 'Танк впереди, урон сзади: башня бьёт ближайшего — Гиганта, а Шар в той же линии сразу за ним долетает целым. Мушкетёр сзади тоже работает: он снимает защитника.',
    combo_loon_miner: 'Шар на мост, а как только он перешёл реку — Шахтёр на башню. Под землёй Шахтёр быстрее и приходит первым: башня и Мушкетёр начинают бить его и так и держат, а Шар долетает целым. Шахтёр раньше Шара умирает до его прилёта, без Шахтёра Шар не долетает.',
    combo_hog_log: 'Хог на мост и ждём. Соперник ставит Гоблинов перед башней — тут же Бревно по ним, пока они только встают: Бревно катится от реки к башне и снимает всех. Без Гоблинов одна башня Хога не держит. Бревно заранее уходит в пустоту, а Гоблины потом спокойно съедят Хога.',
    combo_log_bait: 'Против Бревна Бочку не кидают первой. Сначала дешёвая приманка у моста — Скелеты или Гоблины: соперник кидает в них Бревно. А когда Бревно ушло, Бочку на башню уже нечем встретить. Бочка первой — Бревно встретит именно её.',
    hero_knight_def: 'Насмешка заставляет всех врагов рядом бить только Рыцаря — сначала его щит. Поставь его в центр, чтобы враги стояли под обеими башнями, и жми насмешку сразу, как он появился.',
    hero_knight_atk: 'Насмешка работает и на башни: башня и Мушкетёр бьют щит Рыцаря, а Хог в это время ломает башню. Рыцарь идёт рядом с Хогом, жми, когда они подходят к башне.',
    hero_mpk_boost: 'Блины копятся со временем и от ударов: каждый удар — почти полполосы. Жми после 2–3 ударов по ПЕККЕ: +2–3 уровня и лечение. Сразу — слабый буст, поздно — ПЕККА уже разменяла её.',
    hero_musk_turret: 'Турель встаёт на 3 клетки впереди и при установке бьёт по земле, а потом стреляет по воздуху. Ставь Мушкетёра у реки и турель сразу — рой не успеет долететь.',
    hero_giant_hurl: 'Бросок ждёт цель сам: нажми заранее, и Гигант схватит самого живучего врага рядом, кинет на 9 клеток в другую линию и оглушит на 2 секунды. В защите осторожно — можно бросить врага на свою вторую башню.',
    evo_skel_pekka: 'ПЕККА бьёт по одному, а каждый эво-скелет после удара делает копию — до восьми. Она вязнет в них под башней. Обычные три скелета она снимает за три удара.',
    evo_valk_hog: 'Каждый удар эво-Валькирии — вихрь радиусом 5: он затягивает к ней всех вокруг, даже Хога, который бьёт только здания. Ставь её на пути Хога, перед башней.',
    evo_knight_def: 'Эво-Рыцарь, пока идёт, получает на 60% меньше урона. Поставь его подальше, чтобы он подходил к врагам на ходу, — он доберётся до Мушкетёра почти целым.',
    evo_mk_uppercut: 'Каждый удар эво-Мега-рыцаря — апперкот: цель отлетает на 4 клетки (тяжёлые — на 2,5). Мушкетёра отбрасывает от Гиганта, и Гигант остаётся один под башней.',
    evo_pekka_heal: 'Эво-ПЕККА лечится за каждое убийство — чем живучее убитый, тем больше, и может подняться на 50% выше полного. Пусть сначала добьёт мелочь: гоблины и Мушкетёр подлечат её к Мега-рыцарю.',
    barrel_log: 'Бревно катится вперёд, к сопернику. Брось его за башню — оно пройдёт через место приземления как раз, когда гоблины вылезут, и снимет всех трёх. Прямо на бочку или перед башней — проскочит мимо половины гоблинов.',
    evo_archers_far: 'Эво-Лучницы бьют в полтора раза сильнее по целям дальше 4 клеток. Поставь их в стороне от линии Шара, ближе к центру: они начнут стрелять издалека усиленными выстрелами и собьют его на подлёте. Вплотную — обычный урон, и Шар успевает ударить.',
  },
  uk: {
    mk: 'Мега-лицаря тримає Міні-ПЕККА: 720 урону за удар. Дешевих юнітів він тисне стрибком.',
    hog_edge: 'Хог з краю обходить гармату в «4-3» (центр, 4 клітинки від річки). Зсунь її на клітинку до вежі — «4-2»: так вона в огляді Хога з будь-якого краю.',
    balloon_edge: 'Куля з краю не бачить будівлю в центрі. Став гармату ближче до її краю й до річки — правіше від центру на 3–4 клітинки нижче річки.',
    pekka_kite: 'Крижаний голем б\'є лише будівлі й іде своєю лінією, а ПЕККА б\'є найближчого й іде за ним. Став його біля середини річки, на клітинку в іншу лінію, саме коли ПЕККА перейшла міст.',
    mortar: 'Мортира не стріляє ближче 3,5 клітинки. Юніт, що дійшов до неї впритул, ламає її без втрат — вона б\'є лише по вежі.',
    tornado: 'Коли Хог підходить до вежі, кидай Торнадо на другу клітинку перед королем, на дві клітинки до цієї вежі: Хога затягне впритул до короля, і король прокинеться до кінця бою.',
    barrel_tornado: 'Торнадо між вежею й королем — за пів секунди до приземлення бочки: гоблінів затягне впритул до короля, король прокинеться, а вежа залишиться цілою. Раніше не можна — вихор скінчиться до приземлення.',
    mpk_tornado: 'Торнадо на другу клітинку перед королем, на дві клітинки до вежі, коли ворог уже біля неї: його затягне впритул до короля — король прокинеться. Працює проти будь-якого бійця ближнього бою.',
    miner_tornado: 'Шахтар вилазить біля самої вежі, йому нікуди піти: Торнадо між вежею й королем (ближче до короля) затягує його в радіус короля.',
    giant_tornado: 'Танк, прив\'язаний до будівлі в центрі, стоїть на місці — ідеальна ціль для Торнадо до короля. Будівля тримає, Торнадо будить, король стріляє до кінця бою.',
    loon_tornado: 'Торнадо тягне й летючих. Кидай його між вежею й королем, коли Куля вже над твоєю половиною: її потягне до короля, і дві вежі зіб\'ють її до бомби по вежі.',
    mpk_skel: 'Міні-ПЕККА б\'є одного скелета за удар — троє забирають у неї три удари, а вежа встигає її добити. 1 еліксир проти 4.',
    hog_bats: 'Хог б\'є лише будівлі й не відповідає кажанам. П\'ять кажанів на його шляху плюс вежа збивають його за пару секунд: 2 еліксиру проти 4.',
    mk_icegolem: 'Мега-лицар б\'є найближчого. Крижаний голем у центрі біля річки відводить його в середину — там його дістають обидві вежі, а голем при смерті сповільнює.',
    atk_loon_giant: 'Танк попереду, урон ззаду: вежа б\'є найближчого — Гіганта, а Куля в тій самій лінії одразу за ним долітає цілою. Мушкетерка ззаду теж працює: вона знімає захисника.',
    combo_loon_miner: 'Куля на міст, а щойно вона перейшла річку — Шахтар на вежу. Під землею Шахтар швидший і приходить першим: вежа й Мушкетерка починають бити його й так і тримають, а Куля долітає цілою. Шахтар раніше за Кулю гине до її прильоту, без Шахтаря Куля не долітає.',
    combo_hog_log: 'Хог на міст і чекаємо. Суперник ставить Гоблінів перед вежею — одразу Колоду по них, поки вони тільки встають: Колода котиться від річки до вежі й знімає всіх. Без Гоблінів сама вежа Хога не тримає. Колода заздалегідь іде в порожнечу, а Гобліни потім спокійно з\'їдять Хога.',
    combo_log_bait: 'Проти Колоди Бочку не кидають першою. Спершу дешева приманка біля мосту — Скелети чи Гобліни: суперник кидає в них Колоду. А коли Колода пішла, Бочку на вежу вже нічим зустріти. Бочка першою — Колода зустріне саме її.',
    hero_knight_def: 'Глузування змушує всіх ворогів поруч бити лише Лицаря — спершу його щит. Постав його в центр, щоб вороги стояли під обома вежами, і тисни глузування одразу, як він з\'явився.',
    hero_knight_atk: 'Глузування діє й на вежі: вежа й Мушкетерка б\'ють щит Лицаря, а Хог тим часом ламає вежу. Лицар іде поруч із Хогом, тисни, коли вони підходять до вежі.',
    hero_mpk_boost: 'Млинці копляться з часом і від ударів: кожен удар — майже пів смуги. Тисни після 2–3 ударів по ПЕККІ: +2–3 рівні й лікування. Одразу — слабкий буст, пізно — ПЕККА вже розміняла її.',
    hero_musk_turret: 'Турель стає на 3 клітинки попереду й при встановленні б\'є по землі, а потім стріляє по повітрю. Став Мушкетерку біля річки й турель одразу — рій не встигне долетіти.',
    hero_giant_hurl: 'Кидок сам чекає ціль: натисни заздалегідь, і Гігант схопить найживучішого ворога поруч, кине на 9 клітинок в іншу лінію й оглушить на 2 секунди. У захисті обережно — можна кинути ворога на свою другу вежу.',
    evo_skel_pekka: 'ПЕККА б\'є по одному, а кожен ево-скелет після удару робить копію — до восьми. Вона грузне в них під вежею. Звичайних трьох скелетів вона знімає за три удари.',
    evo_valk_hog: 'Кожен удар ево-Валькірії — вихор радіусом 5: він затягує до неї всіх довкола, навіть Хога, який б\'є лише будівлі. Став її на шляху Хога, перед вежею.',
    evo_knight_def: 'Ево-Лицар, поки йде, отримує на 60% менше урону. Постав його подалі, щоб він підходив до ворогів на ходу, — він дістанеться до Мушкетерки майже цілим.',
    evo_mk_uppercut: 'Кожен удар ево-Мега-лицаря — апперкот: ціль відлітає на 4 клітинки (важкі — на 2,5). Мушкетерку відкидає від Гіганта, і Гігант лишається сам під вежею.',
    evo_pekka_heal: 'Ево-ПЕККА лікується за кожне вбивство — що живучіший убитий, то більше, і може піднятися на 50% вище повного. Хай спершу доб\'є дрібноту: гобліни й Мушкетерка підлікують її до Мега-лицаря.',
    barrel_log: 'Колода котиться вперед, до суперника. Кинь її за вежу — вона пройде через місце приземлення якраз тоді, коли гобліни вилізуть, і зніме всіх трьох. Просто на бочку або перед вежею — проскочить повз половину гоблінів.',
    evo_archers_far: 'Ево-Лучниці б\'ють у півтора раза сильніше по цілях, далі ніж 4 клітинки. Постав їх осторонь від лінії Кулі, ближче до центру: вони почнуть стріляти здалеку посиленими пострілами й зіб\'ють її на підльоті. Впритул — звичайний урон, і Куля встигає вдарити.',
  },
  en: {
    mk: 'Mini PEKKA holds the Mega Knight: 720 damage per hit. Cheap units get crushed by its jump.',
    hog_edge: 'An edge Hog bypasses a 4-3 Cannon (middle, 4 tiles from the river). Shift it one tile toward the tower — 4-2: then it\'s in the Hog\'s sight from any edge.',
    balloon_edge: 'An edge Balloon doesn\'t see a middle building. Place the Cannon closer to its side and to the river — right of center, 3–4 tiles below the river.',
    pekka_kite: 'Ice Golem only hits buildings and walks its own lane, while P.E.K.K.A hits the closest target and follows it. Drop it at the middle of the river, one tile into the other lane, right as P.E.K.K.A crosses.',
    mortar: 'The Mortar can\'t shoot closer than 3.5 tiles. A unit that walks right up to it breaks it for free — it only hits your tower.',
    tornado: 'As the Hog nears your tower, drop Tornado on the second tile in front of your King, two tiles toward that tower: the Hog gets pulled right up to your King and he wakes for the rest of the match.',
    barrel_tornado: 'Tornado between the tower and your King, half a second before the barrel lands: the goblins get pulled right up to your King, he wakes up and the tower stays clean. Not earlier — the Tornado ends before they land.',
    mpk_tornado: 'Tornado on the second tile in front of your King, two tiles toward the tower, once the attacker is at it: it gets pulled right up to the King and he wakes. Works against any melee troop.',
    miner_tornado: 'The Miner pops up right at the tower and has nowhere to go: Tornado between the tower and the King (closer to the King) pulls it into the King\'s range.',
    giant_tornado: 'A tank locked onto a middle building stands still — a perfect Tornado target. The building holds it, Tornado wakes the King, and he shoots for the rest of the match.',
    loon_tornado: 'Tornado pulls air units too. Drop it between the tower and the King once the Balloon is over your half: it gets dragged to the King and both towers shoot it before it bombs.',
    mpk_skel: 'Mini PEKKA kills one skeleton per hit — three of them cost it three hits, and the tower finishes it. 1 elixir vs 4.',
    hog_bats: 'The Hog only hits buildings and ignores Bats. Five Bats in its path plus the tower take it down in a couple of seconds: 2 elixir vs 4.',
    mk_icegolem: 'The Mega Knight hits the closest target. An Ice Golem in the middle at the river pulls it to the center — both towers reach it there, and the Golem slows it on death.',
    atk_loon_giant: 'Tank in front, damage behind: the tower targets the closest — the Giant — so a Balloon right behind it in the same lane arrives intact. A Musketeer behind works too: it kills the defender.',
    combo_loon_miner: 'Balloon at the bridge, and the moment it crosses the river — Miner on the tower. Underground the Miner is faster and arrives first: the tower and the Musketeer lock onto him and stay locked, so the Balloon arrives intact. A Miner sent before the Balloon dies before it lands; no Miner and the Balloon never makes it.',
    combo_hog_log: 'Hog at the bridge, then wait. They drop Goblins in front of the tower — Log them right away while they are still deploying: the Log rolls from the river to the tower and takes them all. Without the Goblins the tower alone can\'t hold the Hog. Throw the Log early and it rolls over nothing, then the Goblins eat the Hog.',
    combo_log_bait: 'Against a Log, never throw the Barrel first. Send a cheap bait to the bridge first — Skeletons or Goblins: they spend the Log on it. Once the Log is gone, nothing meets the Barrel on the tower. Barrel first — and the Log meets exactly that.',
    hero_knight_def: 'Taunt makes every enemy nearby hit only the Knight — his shield first. Place him in the middle so the enemies stand under both towers, and taunt as soon as he lands.',
    hero_knight_atk: 'Taunt works on towers too: the tower and the Musketeer hit the Knight\'s shield while the Hog breaks the tower. The Knight walks next to the Hog — taunt as they reach the tower.',
    hero_mpk_boost: 'Pancakes fill over time and with hits — each hit is almost half a bar. Use it after 2–3 hits on the P.E.K.K.A: +2–3 levels and a heal. Right away is a weak boost, too late and the P.E.K.K.A has already won the trade.',
    hero_musk_turret: 'The turret lands 3 tiles ahead and hits the ground on deploy, then shoots air. Place the Musketeer at the river and the turret right away — the swarm won\'t make it.',
    hero_giant_hurl: 'The hurl waits for a target by itself: use it early and the Giant grabs the toughest enemy nearby, throws it 9 tiles into the other lane and stuns it for 2 seconds. Careful on defense — you can throw the enemy onto your other tower.',
    evo_skel_pekka: 'P.E.K.K.A kills one at a time, but each Evo Skeleton copies itself after a hit — up to eight. She gets stuck in them under your tower. Three normal Skeletons die in three hits.',
    evo_valk_hog: 'Each Evo Valkyrie swing is a radius-5 whirlwind that pulls everything to her — even the Hog, which only hits buildings. Place her in the Hog\'s path in front of the tower.',
    evo_knight_def: 'The Evo Knight takes 60% less damage while walking. Place him further back so he reaches them on the move — he gets to the Musketeer almost untouched.',
    evo_mk_uppercut: 'Every Evo Mega Knight hit is an uppercut: the target flies 4 tiles (heavy ones 2.5). The Musketeer gets knocked off the Giant, leaving him alone under your tower.',
    evo_pekka_heal: 'The Evo P.E.K.K.A heals on every kill — the tougher the victim, the more — and can go up to 50% over full HP. Let her clean up the small units first: Goblins and the Musketeer heal her for the Mega Knight.',
    barrel_log: 'The Log rolls forward, toward the opponent. Drop it behind your tower — it rolls through the landing spot right as the goblins pop out and takes all three. Right on the barrel or in front of the tower, it skips half of them.',
    evo_archers_far: 'Evo Archers deal 1.5× damage to targets more than 4 tiles away. Place them off the Balloon\'s lane, closer to the middle: they open fire from afar with power shots and drop it on approach. Up close it is normal damage and the Balloon gets its hit in.',
  },
}

export interface ResultText {
  tower0: string; tower: string; base: string; stopped: string; notStopped: string; trade: string
  onlyBuildings: string; noAir: string; missed: string; weak: string; lost: string
  kingAwake: string; kingAsleep: string; kingHint: string
  enemyTower: string; noMove: string; towerDown: string; attackShort: string; heroTiming: string
}

export const RESULT_TEXT: Record<Lang, ResultText> = {
  ru: {
    tower0: 'Башня цела', tower: 'Башня −{n}', base: 'без защиты: −{n}',
    stopped: 'Атака отбита за {s} с', notStopped: 'Атака не отбита', trade: '{me} против {en} эликсира',
    onlyBuildings: '{e} бьёт только здания и на войска не отвлекается.',
    noAir: 'Эта карта не бьёт по воздуху — {e} летит мимо.',
    missed: 'Заклинание прилетело, когда там ещё никого не было.',
    weak: 'Заклинание сняло {n} из {hp} здоровья — мало, чтобы убить.',
    lost: 'Твоя карта не пережила бой.',
    kingAwake: 'Король проснулся', kingAsleep: 'Король спит',
    kingHint: 'Король так и не проснулся: врага нужно затянуть вплотную к королевской башне — Торнадо ближе к королю и в момент, когда враг уже у башни.',
    enemyTower: 'Башня соперника −{n}', noMove: 'без хода: −{n}', towerDown: 'Башня снесена!',
    attackShort: 'Урона не хватило: {n} из {need}. Посмотри, кого бьёт башня и кто мешает твоей атаке.',
    heroTiming: 'Здесь решает момент: посмотри показ решения — когда там нажата способность.',
  },
  uk: {
    tower0: 'Вежа ціла', tower: 'Вежа −{n}', base: 'без захисту: −{n}',
    stopped: 'Атаку відбито за {s} с', notStopped: 'Атаку не відбито', trade: '{me} проти {en} еліксиру',
    onlyBuildings: '{e} б\'є лише будівлі й на війська не відволікається.',
    noAir: 'Ця карта не б\'є по повітрю — {e} летить повз.',
    missed: 'Закляття прилетіло, коли там ще нікого не було.',
    weak: 'Закляття зняло {n} з {hp} здоров\'я — замало, щоб убити.',
    lost: 'Твоя карта не пережила бій.',
    kingAwake: 'Король прокинувся', kingAsleep: 'Король спить',
    kingHint: 'Король так і не прокинувся: ворога треба затягнути впритул до королівської вежі — Торнадо ближче до короля й у момент, коли ворог уже біля вежі.',
    enemyTower: 'Вежа суперника −{n}', noMove: 'без ходу: −{n}', towerDown: 'Вежу знесено!',
    attackShort: 'Урону не вистачило: {n} з {need}. Подивись, кого б\'є вежа і хто заважає твоїй атаці.',
    heroTiming: 'Тут вирішує момент: подивись показ рішення — коли там натиснуто здібність.',
  },
  en: {
    tower0: 'Tower untouched', tower: 'Tower −{n}', base: 'undefended: −{n}',
    stopped: 'Attack stopped in {s}s', notStopped: 'Attack not stopped', trade: '{me} vs {en} elixir',
    onlyBuildings: '{e} only hits buildings and ignores troops.',
    noAir: 'This card can\'t hit air — {e} flies past.',
    missed: 'The spell landed before anyone was there.',
    weak: 'The spell dealt {n} of {hp} HP — not enough to kill.',
    lost: 'Your card didn\'t survive the fight.',
    kingAwake: 'King is awake', kingAsleep: 'King is asleep',
    kingHint: 'Your King never woke up: pull the attacker right up to your King Tower — Tornado closer to the King, right when they reach your tower.',
    enemyTower: 'Their tower −{n}', noMove: 'without a move: −{n}', towerDown: 'Tower down!',
    attackShort: 'Not enough damage: {n} of {need}. Check what their tower shoots and what stops your push.',
    heroTiming: 'Timing decides here: watch the solution — when the ability is used.',
  },
}

/** Почему так вышло - из того, что произошло в бою и из свойств карт. */
export function explain(p: Puzzle, o: Outcome, card: string, dealt: number, lang: Lang): string[] {
  const tx = RESULT_TEXT[lang]
  const n = (c: string) => CARD_NAMES[lang][c] ?? c
  const foe = p.enemyCard === 'Goblin Barrel' ? 'Goblins' : p.enemyCard
  const fs = UNITS[foe]
  const kind = cardInfo(card).kind
  const lines: string[] = []
  if (p.solution.ability !== undefined && o.grade !== 'perfect') lines.push(tx.heroTiming)
  if (p.abilityOnly) return lines
  if (p.sc.goal === 'attack') {
    if (o.grade !== 'perfect') lines.push(tx.attackShort.replace('{n}', String(o.dealt)).replace('{need}', String(p.sc.limits[0])))
    return lines
  }
  if (p.sc.goal === 'king' && !o.kingAwake) lines.push(tx.kingHint)
  if (o.grade !== 'perfect') {
    if (kind === 'spell' && dealt === 0) lines.push(tx.missed)
    else if (kind === 'spell' && !o.killed && fs) lines.push(tx.weak.replace('{n}', String(Math.round(dealt))).replace('{hp}', String(fs.hp)))
    else if (fs?.flying && kind !== 'spell' && !UNITS[card]?.air) lines.push(tx.noAir.replace('{e}', n(foe)))
    else if (fs?.buildingsOnly && kind === 'troop' && o.dmg > p.sc.limits[0]) lines.push(tx.onlyBuildings.replace('{e}', n(foe)))
    if (kind !== 'spell' && !o.survived && lines.length === 0) lines.push(tx.lost)
  }
  return lines
}

/** Открыть тренажёр (на нужной задаче) из любого места приложения - App ловит событие. */
export const OPEN_TRAINER = 'cwst:open-trainer'
let pending: string | null = null

export function openTrainer(puzzleId?: string) {
  pending = puzzleId ?? null
  window.dispatchEvent(new Event(OPEN_TRAINER))
}

/** Задача, с которой открыли тренажёр; читается один раз. */
export function takePendingPuzzle(): string | null {
  const p = pending
  pending = null
  return p
}

/** Задача тренажёра на ту же тему, что фишка. */
export function puzzleForTip(tip: string | null): string | null {
  return PUZZLES.find(p => p.tip === tip)?.id ?? null
}
