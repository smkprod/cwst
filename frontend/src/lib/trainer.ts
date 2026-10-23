import type { Lang } from './i18n'
import type { Actor, ActorKind, Key } from '../components/ArenaAnim'
import type { TipId } from './tips'

/**
 * Тренажёр «Задефай атаку»: враг идёт, на решающем моменте игра встаёт - игрок
 * выбирает карту и клетку. Оценка - по зонам: «идеально», «сработает» и «мимо»,
 * у каждого исхода своя анимация и объяснение «почему». Это не симулятор игры, а
 * задачник: зоны взяты из тех же приёмов, что и в фишках.
 *
 * Координаты - клетки арены, как в ArenaAnim: река 15-17, своя половина ниже 17.
 */

export type Grade = 'perfect' | 'ok' | 'fail'

export interface HandCard { card: string; cost: number; kind: ActorKind; r?: number; spell?: boolean }

export const CARDS: Record<string, HandCard> = {
  'Cannon': { card: 'Cannon', cost: 3, kind: 'building', r: 0.95 },
  'Knight': { card: 'Knight', cost: 3, kind: 'mine', r: 0.7 },
  'Skeletons': { card: 'Skeletons', cost: 1, kind: 'mine', r: 0.55 },
  'Fireball': { card: 'Fireball', cost: 4, kind: 'spell', r: 2.5, spell: true },
  'The Log': { card: 'The Log', cost: 2, kind: 'log', r: 1.95, spell: true },
  'Musketeer': { card: 'Musketeer', cost: 4, kind: 'mine', r: 0.65 },
  'Mini P.E.K.K.A': { card: 'Mini P.E.K.K.A', cost: 4, kind: 'mine', r: 0.65 },
  'Golem': { card: 'Golem', cost: 8, kind: 'mine', r: 1.0 },
  'Hog Rider': { card: 'Hog Rider', cost: 4, kind: 'mine', r: 0.75 },
}

export const CARD_NAMES: Record<Lang, Record<string, string>> = {
  ru: { 'Cannon': 'Пушка', 'Knight': 'Рыцарь', 'Skeletons': 'Скелеты', 'Fireball': 'Огн. шар', 'The Log': 'Бревно', 'Musketeer': 'Мушкетёр',
    'Mini P.E.K.K.A': 'Мини-ПЕККА', 'Golem': 'Голем', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-рыцарь', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гоблины', 'Balloon': 'Шар', 'Giant': 'Гигант', 'X-Bow': 'Икс-лук', 'Miner': 'Шахтёр' },
  uk: { 'Cannon': 'Гармата', 'Knight': 'Лицар', 'Skeletons': 'Скелети', 'Fireball': 'Вогн. куля', 'The Log': 'Колода', 'Musketeer': 'Мушкетерка',
    'Mini P.E.K.K.A': 'Міні-ПЕККА', 'Golem': 'Голем', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-лицар', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гобліни', 'Balloon': 'Куля', 'Giant': 'Гігант', 'X-Bow': 'Ікс-лук', 'Miner': 'Шахтар' },
  en: { 'Cannon': 'Cannon', 'Knight': 'Knight', 'Skeletons': 'Skeletons', 'Fireball': 'Fireball', 'The Log': 'Log', 'Musketeer': 'Musketeer',
    'Mini P.E.K.K.A': 'Mini PEKKA', 'Golem': 'Golem', 'Hog Rider': 'Hog', 'Mega Knight': 'Mega Knight', 'Goblin Barrel': 'Barrel',
    'Goblins': 'Goblins', 'Balloon': 'Balloon', 'Giant': 'Giant', 'X-Bow': 'X-Bow', 'Miner': 'Miner' },
}

export interface Verdict { grade: Grade; reason: string }

export interface Puzzle {
  id: string
  tip: TipId
  enemyCard: string
  elixir: number
  hand: string[]
  /** Враг до решающего момента. */
  intro: (n: (card: string) => string) => Actor[]
  decision: number
  grade: (card: string, x: number, y: number) => Verdict
  /** Что было дальше - с нуля от решающего момента. */
  outcome: (card: string, x: number, y: number, v: Verdict, n: (card: string) => string) => { actors: Actor[]; duration: number }
}

/** Куда встаёт карта: здание 2×2 - на угол клеток, войско - в центр клетки, заклинание - куда тапнули. */
export function snap(card: string, x: number, y: number): { x: number; y: number } {
  const c = CARDS[card]
  if (c?.kind === 'building') return { x: Math.min(17, Math.max(1, Math.round(x))), y: Math.min(31, Math.max(18, Math.round(y))) }
  if (c?.spell) return { x: Math.round(x * 2) / 2, y: Math.round(y * 2) / 2 }
  return { x: Math.floor(x) + 0.5, y: Math.floor(y) + 0.5 }
}

/** Войска и здания - только на свою половину; заклинания - куда угодно. */
export function canPlace(card: string, y: number): boolean {
  return CARDS[card]?.spell ? true : y >= 17
}

const inBox = (x: number, y: number, x0: number, x1: number, y0: number, y1: number) => x >= x0 && x <= x1 && y >= y0 && y <= y1
const dist = (x: number, y: number, a: number, b: number) => Math.hypot(x - a, y - b)

/** Путь от точки к точке со скоростью (клеток в секунду). */
function walk(from: { x: number; y: number }, to: { x: number; y: number }, t0: number, speed: number): Key[] {
  const d = dist(from.x, from.y, to.x, to.y)
  return [{ t: t0, x: from.x, y: from.y }, { t: t0 + d / speed, x: to.x, y: to.y }]
}

/** Точка, где юнит встаёт бить цель: не доходя radius клеток. */
function stopNear(from: { x: number; y: number }, target: { x: number; y: number }, radius: number) {
  const d = dist(from.x, from.y, target.x, target.y)
  if (d <= radius) return from
  const k = (d - radius) / d
  return { x: from.x + (target.x - from.x) * k, y: from.y + (target.y - from.y) * k }
}

const hit = (id: string, x: number, y: number, text: string, t0: number): Actor =>
  ({ id, kind: 'text', label: text, keys: [{ t: t0, x, y }, { t: t0 + 0.9, x, y }] })

function placed(card: string, x: number, y: number, t0: number, t1: number, label = ''): Actor {
  const c = CARDS[card]
  return { id: 'mine', kind: c.kind, card, label, r: c.r, keys: [{ t: t0, x, y }, { t: t1, x, y }] }
}

/** Бревно катится вперёд, к сопернику, на 10 клеток. */
function logRoll(x: number, y: number, t0: number): Actor {
  return { id: 'log', kind: 'log', card: 'The Log', label: '', r: 1.95, keys: [{ t: t0, x, y }, { t: t0 + 1.0, x, y: y - 10 }] }
}

function boom(x: number, y: number, t0: number): Actor {
  return { id: 'boom', kind: 'spell', card: 'Fireball', label: '', r: 2.5, keys: [{ t: t0, x, y }, { t: t0 + 0.5, x, y }] }
}

const PL = { x: 3.5, y: 23.6 }   // перед левой принцессой - туда бьют юниты
const PR = { x: 14.5, y: 23.6 }

export const PUZZLES: Puzzle[] = [
  {
    id: 'hog', tip: 'anti_hog', enemyCard: 'Hog Rider', elixir: 6, hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'], decision: 1.1,
    intro: n => [{ id: 'e', kind: 'enemy', card: 'Hog Rider', label: n('Hog Rider'), r: 0.75, keys: [{ t: 0, x: 3.5, y: 11.5 }, { t: 1.1, x: 3.5, y: 17.3 }] }],
    grade: (card, x, y) => {
      if (card === 'Cannon') return inBox(x, y, 8, 10, 21, 23) ? { grade: 'perfect', reason: 'hog_perfect' }
        : inBox(x, y, 6.5, 11.5, 19.5, 24.5) ? { grade: 'ok', reason: 'hog_cannon_ok' } : { grade: 'fail', reason: 'hog_cannon_far' }
      if (card === 'Knight') return inBox(x, y, 2, 6, 17, 24) ? { grade: 'ok', reason: 'hog_knight' } : { grade: 'fail', reason: 'hog_knight_far' }
      if (card === 'Skeletons') return { grade: 'fail', reason: 'hog_skeletons' }
      return { grade: 'fail', reason: 'hog_fireball' }
    },
    outcome: (card, x, y, v, n) => {
      const start = { x: 3.5, y: 17.3 }
      const label = n('Hog Rider')
      if (card === 'Cannon' && v.grade !== 'fail') {
        const stop = stopNear(start, { x, y }, 1.3)
        const k = walk(start, stop, 0, 3)
        const die = k[1].t + (v.grade === 'perfect' ? 1.6 : 2.4)
        return { duration: die + 0.8, actors: [
          { id: 'e', kind: 'enemy', card: 'Hog Rider', label, r: 0.75, keys: [...k, { t: die, x: stop.x, y: stop.y }] },
          placed('Cannon', x, y, 0, die + 0.8),
          ...(v.grade === 'ok' ? [hit('h', x, y - 0.8, '−250', k[1].t + 0.6)] : []),
        ] }
      }
      const k = walk(start, PL, 0, 3)
      const die = k[1].t + (card === 'Knight' && v.grade === 'ok' ? 1.6 : 3.2)
      return { duration: die + 0.8, actors: [
        { id: 'e', kind: 'enemy', card: 'Hog Rider', label, r: 0.75, keys: [...k, { t: die, x: PL.x, y: PL.y }] },
        card === 'Fireball' ? boom(x, y, 0) : card === 'Knight'
          ? { id: 'mine', kind: 'mine', card, label: '', r: 0.7, keys: [{ t: 0, x, y }, ...walk({ x, y }, stopNear({ x, y }, PL, 1.1), 0.2, 2).slice(1), { t: die + 0.8, x: PL.x, y: PL.y - 1 }] }
          : placed(card, x, y, 0, 1.2),
        hit('h1', PL.x, PL.y + 1.5, card === 'Knight' && v.grade === 'ok' ? '−300' : '−1 200', k[1].t + 0.3),
        ...(card === 'Knight' && v.grade === 'ok' ? [] : [hit('h2', PL.x, PL.y + 1.5, '−1 200', k[1].t + 1.6)]),
      ] }
    },
  },
  {
    id: 'mk', tip: 'kite', enemyCard: 'Mega Knight', elixir: 5, hand: ['Skeletons', 'Cannon', 'Musketeer', 'Fireball'], decision: 1.1,
    intro: n => [{ id: 'e', kind: 'enemy', card: 'Mega Knight', label: n('Mega Knight'), r: 1.0, keys: [{ t: 0, x: 3.5, y: 12.5 }, { t: 1.1, x: 3.5, y: 18.6 }] }],
    grade: (card, x, y) => {
      if (card === 'Skeletons') return inBox(x, y, 7.5, 10.5, 22.5, 26) ? { grade: 'perfect', reason: 'mk_perfect' }
        : inBox(x, y, 6, 12, 20.5, 27.5) ? { grade: 'ok', reason: 'mk_skel_ok' } : { grade: 'fail', reason: 'mk_skel_far' }
      if (card === 'Cannon') return inBox(x, y, 7, 11, 20, 24) ? { grade: 'ok', reason: 'mk_cannon' } : { grade: 'fail', reason: 'mk_skel_far' }
      if (card === 'Musketeer') return { grade: 'fail', reason: 'mk_musk' }
      return { grade: 'fail', reason: 'mk_fireball' }
    },
    outcome: (card, x, y, v, n) => {
      const start = { x: 3.5, y: 18.6 }
      const label = n('Mega Knight')
      if (v.grade !== 'fail') {
        const stop = stopNear(start, { x, y }, 1.2)
        const k = walk(start, stop, 0, 2)
        const die = k[1].t + (v.grade === 'perfect' ? 1.8 : 2.4)
        return { duration: die + 0.8, actors: [
          { id: 'e', kind: 'enemy', card: 'Mega Knight', label, r: 1.0, keys: [...k, { t: die, x: stop.x, y: stop.y }] },
          placed(card, x, y, 0, k[1].t + 0.3),
        ] }
      }
      const stop = { x: 3.5, y: 23.0 }
      const k = walk(start, stop, 0, 2)
      return { duration: k[1].t + 3, actors: [
        { id: 'e', kind: 'enemy', card: 'Mega Knight', label, r: 1.0, keys: [...k, { t: k[1].t + 2.6, x: stop.x, y: stop.y }] },
        card === 'Fireball' ? boom(x, y, 0) : placed(card, x, y, 0, card === 'Musketeer' ? k[1].t : 1),
        hit('h1', PL.x, PL.y + 1.5, '−1 000', k[1].t + 0.2),
        hit('h2', PL.x, PL.y + 1.5, '−1 000', k[1].t + 1.4),
      ] }
    },
  },
  {
    id: 'barrel', tip: 'log_barrel', enemyCard: 'Goblin Barrel', elixir: 4, hand: ['The Log', 'Fireball', 'Skeletons', 'Knight'], decision: 0.7,
    intro: n => [{ id: 'b', kind: 'barrel', card: 'Goblin Barrel', label: n('Goblin Barrel'), keys: [{ t: 0, x: 9, y: 11.5 }, { t: 0.7, x: 6.5, y: 17.2 }] }],
    grade: (card, x, y) => {
      if (card === 'The Log') return inBox(x, y, 2, 5, 23.8, 28.5) ? { grade: 'perfect', reason: 'barrel_perfect' }
        : inBox(x, y, 1.5, 5.5, 21.5, 23.8) ? { grade: 'ok', reason: 'barrel_log_front' } : { grade: 'fail', reason: 'barrel_log_miss' }
      if (card === 'Fireball') return inBox(x, y, 1.5, 5.5, 21, 25) ? { grade: 'ok', reason: 'barrel_fireball' } : { grade: 'fail', reason: 'barrel_log_miss' }
      return { grade: 'fail', reason: 'barrel_troops' }
    },
    outcome: (card, x, y, v, n) => {
      const land = 0.6
      const gob = (id: string, gx: number, gy: number, die: number): Actor =>
        ({ id, kind: 'enemy', card: 'Goblins', label: id === 'g2' ? n('Goblins') : '', r: 0.45, keys: [{ t: land, x: gx, y: gy }, { t: die, x: gx, y: gy - 0.2 }] })
      const barrel: Actor = { id: 'b', kind: 'barrel', card: 'Goblin Barrel', label: '', keys: [{ t: 0, x: 6.5, y: 17.2 }, { t: land, x: 3.6, y: 23.4 }] }
      if (v.grade !== 'fail') {
        const sweep = card === 'The Log' ? Math.max(land + 0.05, 0.45 + Math.max(0, y - 22.8) / 10) : 0.75
        return { duration: 2.4, actors: [
          barrel, gob('g1', 2.6, 22.9, sweep), gob('g2', 3.6, 22.6, sweep), gob('g3', 4.6, 22.9, sweep),
          card === 'The Log' ? logRoll(x, y, 0.45) : boom(x, y, 0.55),
        ] }
      }
      const die = 2.8
      return { duration: 3.4, actors: [
        barrel, gob('g1', 2.6, 22.9, die), gob('g2', 3.6, 22.6, die), gob('g3', 4.6, 22.9, die),
        card === 'The Log' ? logRoll(x, y, 0.2) : card === 'Fireball' ? boom(x, y, 0.2) : placed(card, x, y, 0, 1.4),
        hit('h1', PL.x, PL.y + 1.5, '−350', 1.0), hit('h2', PL.x, PL.y + 1.5, '−350', 1.7), hit('h3', PL.x, PL.y + 1.5, '−350', 2.4),
      ] }
    },
  },
  {
    id: 'balloon', tip: 'anti_air', enemyCard: 'Balloon', elixir: 5, hand: ['Musketeer', 'Cannon', 'Knight', 'Skeletons'], decision: 2.0,
    intro: n => [{ id: 'e', kind: 'enemy', card: 'Balloon', label: n('Balloon'), r: 0.9, keys: [{ t: 0, x: 14.5, y: 11.5 }, { t: 2.0, x: 14.5, y: 19.5 }] }],
    grade: (card, x, y) => {
      if (card === 'Musketeer') return inBox(x, y, 12, 17, 25.5, 30.5) ? { grade: 'perfect', reason: 'balloon_perfect' }
        : inBox(x, y, 11, 17.5, 20, 25.5) ? { grade: 'ok', reason: 'balloon_front' } : { grade: 'fail', reason: 'balloon_far' }
      return { grade: 'fail', reason: 'balloon_no_air' }
    },
    outcome: (card, x, y, v, n) => {
      const start = { x: 14.5, y: 19.5 }
      const stop = { x: 14.5, y: 22.8 }
      const k = walk(start, stop, 0, 3.5)
      const label = n('Balloon')
      if (v.grade !== 'fail') {
        const die = k[1].t + (v.grade === 'perfect' ? 1.0 : 1.6)
        return { duration: die + 0.8, actors: [
          { id: 'e', kind: 'enemy', card: 'Balloon', label, r: 0.9, keys: [...k, { t: die, x: stop.x, y: stop.y }] },
          placed(card, x, y, 0, die + 0.8),
          ...(v.grade === 'ok' ? [hit('h', PR.x, PR.y + 1.5, '−800', k[1].t + 0.4)] : []),
        ] }
      }
      return { duration: k[1].t + 3, actors: [
        { id: 'e', kind: 'enemy', card: 'Balloon', label, r: 0.9, keys: [...k, { t: k[1].t + 2.4, x: stop.x, y: stop.y }] },
        placed(card, x, y, 0, k[1].t + 2.4),
        hit('h1', PR.x, PR.y + 1.5, '−1 600', k[1].t + 0.3), hit('h2', PR.x, PR.y + 1.5, '−1 600', k[1].t + 1.5),
      ] }
    },
  },
  {
    id: 'giant', tip: 'anti_tank', enemyCard: 'Giant', elixir: 7, hand: ['Cannon', 'Mini P.E.K.K.A', 'Skeletons', 'Fireball'], decision: 2.2,
    intro: n => [{ id: 'e', kind: 'enemy', card: 'Giant', label: n('Giant'), r: 1.05, keys: [{ t: 0, x: 14.5, y: 11.5 }, { t: 2.2, x: 14.5, y: 17.6 }] }],
    grade: (card, x, y) => {
      if (card === 'Cannon') return inBox(x, y, 8, 10, 21, 23) ? { grade: 'perfect', reason: 'giant_perfect' }
        : inBox(x, y, 6.5, 11.5, 19.5, 24.5) ? { grade: 'ok', reason: 'giant_cannon_ok' } : { grade: 'fail', reason: 'giant_far' }
      if (card === 'Mini P.E.K.K.A') return inBox(x, y, 11.5, 17.5, 18, 25) ? { grade: 'ok', reason: 'giant_mp' } : { grade: 'fail', reason: 'giant_far' }
      return { grade: 'fail', reason: 'giant_weak' }
    },
    outcome: (card, x, y, v, n) => {
      const start = { x: 14.5, y: 17.6 }
      const label = n('Giant')
      if (card === 'Cannon' && v.grade !== 'fail') {
        const stop = stopNear(start, { x, y }, 1.6)
        const k = walk(start, stop, 0, 1.8)
        const die = k[1].t + (v.grade === 'perfect' ? 2.0 : 2.6)
        return { duration: die + 0.8, actors: [
          { id: 'e', kind: 'enemy', card: 'Giant', label, r: 1.05, keys: [...k, { t: die, x: stop.x, y: stop.y }] },
          placed('Cannon', x, y, 0, die + 0.8),
        ] }
      }
      const stop = { x: 14.5, y: 22.9 }
      const k = walk(start, stop, 0, 1.8)
      const ok = v.grade === 'ok'
      const die = k[1].t + (ok ? 1.4 : 3.2)
      return { duration: die + 0.8, actors: [
        { id: 'e', kind: 'enemy', card: 'Giant', label, r: 1.05, keys: [...k, { t: die, x: stop.x, y: stop.y }] },
        card === 'Fireball' ? boom(x, y, 0) : ok
          ? { id: 'mine', kind: 'mine', card, label: '', r: 0.65, keys: [...walk({ x, y }, stopNear({ x, y }, stop, 1.4), 0, 2.2), { t: die + 0.8, x: stop.x, y: stop.y + 1.4 }] }
          : placed(card, x, y, 0, 1.4),
        hit('h1', PR.x, PR.y + 1.5, ok ? '−300' : '−700', k[1].t + 0.3),
        ...(ok ? [] : [hit('h2', PR.x, PR.y + 1.5, '−700', k[1].t + 1.5), hit('h3', PR.x, PR.y + 1.5, '−700', k[1].t + 2.7)]),
      ] }
    },
  },
  {
    id: 'xbow', tip: 'anti_siege', enemyCard: 'X-Bow', elixir: 6, hand: ['Knight', 'Fireball', 'Skeletons', 'Cannon'], decision: 0.5,
    intro: n => [{ id: 'e', kind: 'enemy', card: 'X-Bow', label: n('X-Bow'), r: 0.9, keys: [{ t: 0.1, x: 14.5, y: 12.8 }, { t: 0.5, x: 14.5, y: 12.8 }] }],
    grade: (card, x, y) => {
      if (card === 'Knight') return inBox(x, y, 13, 16.5, 17, 18.9) ? { grade: 'perfect', reason: 'xbow_perfect' }
        : inBox(x, y, 12, 17, 18.9, 22.5) ? { grade: 'ok', reason: 'xbow_late' } : { grade: 'fail', reason: 'xbow_far' }
      if (card === 'Fireball') return dist(x, y, 14.5, 12.8) <= 2.5 ? { grade: 'ok', reason: 'xbow_fireball' } : { grade: 'fail', reason: 'xbow_far' }
      if (card === 'Skeletons') return { grade: 'fail', reason: 'xbow_skel' }
      return { grade: 'fail', reason: 'xbow_cannon' }
    },
    outcome: (card, x, y, v, n) => {
      const bow = { x: 14.5, y: 12.8 }
      const xbow = (die: number): Actor => ({ id: 'e', kind: 'enemy', card: 'X-Bow', label: n('X-Bow'), r: 0.9, keys: [{ t: 0, ...bow }, { t: die, ...bow }] })
      if (card === 'Knight' && v.grade !== 'fail') {
        const stop = stopNear({ x, y }, bow, 1.3)
        const k = walk({ x, y }, stop, 0, 2)
        const die = k[1].t + 1.6
        return { duration: die + 0.8, actors: [
          xbow(die), { id: 'mine', kind: 'mine', card: 'Knight', label: '', r: 0.7, keys: [...k, { t: die + 0.8, ...stop }] },
          ...(v.grade === 'ok' ? [hit('h', PR.x, PR.y + 1.5, '−400', 0.6)] : []),
        ] }
      }
      if (card === 'Fireball' && v.grade === 'ok') {
        return { duration: 3.6, actors: [xbow(3.0), boom(x, y, 0.1), hit('h', PR.x, PR.y + 1.5, '−400', 1.2), hit('h2', PR.x, PR.y + 1.5, '−400', 2.2)] }
      }
      return { duration: 4, actors: [
        xbow(4), card === 'Fireball' ? boom(x, y, 0.1) : placed(card, x, y, 0, card === 'Skeletons' ? 0.9 : 4),
        hit('h1', PR.x, PR.y + 1.5, '−400', 0.6), hit('h2', PR.x, PR.y + 1.5, '−400', 1.5),
        hit('h3', PR.x, PR.y + 1.5, '−400', 2.4), hit('h4', PR.x, PR.y + 1.5, '−400', 3.3),
      ] }
    },
  },
  {
    id: 'miner', tip: 'anti_miner', enemyCard: 'Miner', elixir: 4, hand: ['Skeletons', 'Knight', 'The Log', 'Fireball'], decision: 1.15,
    intro: n => [
      { id: 'dig', kind: 'spell', label: '', r: 0.3, keys: [{ t: 0, x: 3.5, y: 11.5 }, { t: 1.0, x: 3.5, y: 23.3 }] },
      { id: 'e', kind: 'enemy', card: 'Miner', label: n('Miner'), r: 0.7, keys: [{ t: 1.0, x: 3.5, y: 23.4 }, { t: 1.15, x: 3.5, y: 23.4 }] },
    ],
    grade: (card, x, y) => {
      const d = dist(x, y, 3.5, 23.4)
      if (card === 'Skeletons') return d <= 1.8 ? { grade: 'perfect', reason: 'miner_perfect' } : d <= 3 ? { grade: 'ok', reason: 'miner_skel_ok' } : { grade: 'fail', reason: 'miner_far' }
      if (card === 'Knight') return d <= 2.2 ? { grade: 'ok', reason: 'miner_knight' } : { grade: 'fail', reason: 'miner_far' }
      if (card === 'The Log') return inBox(x, y, 2, 5, 23, 27.5) ? { grade: 'ok', reason: 'miner_log' } : { grade: 'fail', reason: 'miner_far' }
      return { grade: 'fail', reason: 'miner_fireball' }
    },
    outcome: (card, x, y, v, n) => {
      const m = { x: 3.5, y: 23.4 }
      const label = n('Miner')
      if (v.grade !== 'fail') {
        const die = v.grade === 'perfect' ? 1.3 : 1.9
        return { duration: die + 0.8, actors: [
          { id: 'e', kind: 'enemy', card: 'Miner', label, r: 0.7, keys: [{ t: 0, ...m }, { t: die, ...m }] },
          card === 'The Log' ? logRoll(x, y, 0.1) : placed(card, x, y, 0, die + 0.8),
          hit('h', PL.x, PL.y + 1.5, '−150', 0.3),
          ...(v.grade === 'ok' ? [hit('h2', PL.x, PL.y + 1.5, '−150', 1.0)] : []),
        ] }
      }
      return { duration: 3.2, actors: [
        { id: 'e', kind: 'enemy', card: 'Miner', label, r: 0.7, keys: [{ t: 0, ...m }, { t: 2.8, ...m }] },
        card === 'Fireball' ? boom(x, y, 0) : card === 'The Log' ? logRoll(x, y, 0.1) : placed(card, x, y, 0, 3.2),
        hit('h1', PL.x, PL.y + 1.5, '−200', 0.3), hit('h2', PL.x, PL.y + 1.5, '−200', 1.0),
        hit('h3', PL.x, PL.y + 1.5, '−200', 1.7), hit('h4', PL.x, PL.y + 1.5, '−200', 2.4),
      ] }
    },
  },
  {
    id: 'elixir', tip: 'elixir', enemyCard: 'Golem', elixir: 10, hand: ['Golem', 'Hog Rider', 'Musketeer', 'Skeletons'], decision: 0.3,
    intro: () => [],
    grade: (card, _x, y) => {
      if (card === 'Golem') return y >= 30 ? { grade: 'perfect', reason: 'elixir_perfect' } : y >= 26 ? { grade: 'ok', reason: 'elixir_golem_mid' } : { grade: 'fail', reason: 'elixir_golem_bridge' }
      if (card === 'Hog Rider') return { grade: 'ok', reason: 'elixir_hog' }
      if (card === 'Musketeer') return y >= 26 ? { grade: 'ok', reason: 'elixir_musk' } : { grade: 'fail', reason: 'elixir_musk_front' }
      return { grade: 'fail', reason: 'elixir_skel' }
    },
    outcome: (card, x, y, v, _n) => {
      const lane = x < 9 ? 3.5 : 14.5
      const speed = card === 'Golem' ? 1.2 : card === 'Hog Rider' ? 3 : 1.8
      const path = walk({ x, y }, { x: lane, y: Math.min(y, 17.6) }, 0.2, speed)
      const end = Math.min(path[1].t, 4.2)
      return { duration: end + 0.6, actors: [
        { id: 'mine', kind: 'mine', card, label: '', r: CARDS[card].r, keys: [{ t: 0, x, y }, ...path] , to: end + 0.6 },
        ...(v.grade === 'fail' && card === 'Skeletons' ? [hit('h', x, y - 1, '−9 💧', 0.4)] : []),
      ] }
    },
  },
]

export const FREE_PUZZLES = 3

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
    hog: { title: 'Хог в левой линии', task: 'Хог перешёл мост. Отбейся так, чтобы башня не пострадала.' },
    mk: { title: 'Мега-рыцарь у моста', task: 'Мега-рыцарь идёт на левую башню. Уведи его под огонь.' },
    barrel: { title: 'Гоблинская бочка', task: 'Бочка летит на левую башню. Сбей гоблинов при приземлении.' },
    balloon: { title: 'Шар над рекой', task: 'Шар летит на правую башню. Встреть его, пока он не начал бить.' },
    giant: { title: 'Гигант в правой линии', task: 'Гигант перешёл мост. Защитись с минимумом урона.' },
    xbow: { title: 'Икс-лук у реки', task: 'Соперник поставил Икс-лук. Сломай его, пока он не набил урон.' },
    miner: { title: 'Шахтёр у башни', task: 'Шахтёр вылез у левой башни. Убери его дешевле.' },
    elixir: { title: '10 эликсира', task: 'У тебя 10 эликсира, соперник молчит. Начни атаку правильно.' },
  },
  uk: {
    hog: { title: 'Хог у лівій лінії', task: 'Хог перейшов міст. Відбийся так, щоб вежа не постраждала.' },
    mk: { title: 'Мега-лицар біля мосту', task: 'Мега-лицар іде на ліву вежу. Відведи його під вогонь.' },
    barrel: { title: 'Гоблінська бочка', task: 'Бочка летить на ліву вежу. Збий гоблінів під час приземлення.' },
    balloon: { title: 'Куля над річкою', task: 'Куля летить на праву вежу. Зустрінь її, поки вона не почала бити.' },
    giant: { title: 'Гігант у правій лінії', task: 'Гігант перейшов міст. Захистися з мінімумом урону.' },
    xbow: { title: 'Ікс-лук біля річки', task: 'Суперник поставив Ікс-лук. Злам його, поки він не набив урону.' },
    miner: { title: 'Шахтар біля вежі', task: 'Шахтар виліз біля лівої вежі. Прибери його дешевше.' },
    elixir: { title: '10 еліксиру', task: 'У тебе 10 еліксиру, суперник мовчить. Почни атаку правильно.' },
  },
  en: {
    hog: { title: 'Hog in the left lane', task: 'The Hog crossed the bridge. Defend without tower damage.' },
    mk: { title: 'Mega Knight at the bridge', task: 'The Mega Knight goes for your left tower. Pull it under fire.' },
    barrel: { title: 'Goblin Barrel', task: 'A barrel flies at your left tower. Kill the goblins as they land.' },
    balloon: { title: 'Balloon over the river', task: 'A Balloon heads for your right tower. Meet it before it hits.' },
    giant: { title: 'Giant in the right lane', task: 'The Giant crossed the bridge. Defend with minimal damage.' },
    xbow: { title: 'X-Bow at the river', task: 'They placed an X-Bow. Break it before it deals damage.' },
    miner: { title: 'Miner at the tower', task: 'A Miner popped up at your left tower. Deal with it cheaply.' },
    elixir: { title: '10 elixir', task: 'You have 10 elixir and the field is quiet. Start your push right.' },
  },
}

export const REASONS: Record<Lang, Record<string, string>> = {
  ru: {
    hog_perfect: 'Пушка в центре — Хог свернул к ней и погиб под башнями.',
    hog_cannon_ok: 'Сработало, но пушка не в центре: часть урона она приняла сама. Центр, 4 клетки от реки — надёжнее.',
    hog_cannon_far: 'Пушка далеко — Хог её не заметил и доехал до башни.',
    hog_knight: 'Рыцарь добил Хога, но башня успела получить урон. Здание в центр защищает без потерь.',
    hog_knight_far: 'Рыцарь не успел — Хог доехал до башни.',
    hog_skeletons: 'Хог бьёт только здания: скелетов он просто проигнорировал.',
    hog_fireball: 'Огненный шар не убивает Хога — он доехал до башни.',
    mk_perfect: 'Скелеты в центре увели Мега-рыцаря под огонь обеих башен.',
    mk_skel_ok: 'Сработало, но скелеты не в центре — по рыцарю стреляла одна башня.',
    mk_skel_far: 'Мимо: Мега-рыцарь пошёл прямо на башню.',
    mk_cannon: 'Сработало: пушка в центре тоже тянет рыцаря. Но 3 эликсира против 1 у скелетов.',
    mk_musk: 'Мега-рыцарь прыгнул и раздавил мушкетёра — дорогой размен.',
    mk_fireball: 'Огненный шар Мега-рыцаря не убивает — он дошёл до башни.',
    barrel_perfect: 'Бревно с башни прокатилось вперёд и снесло гоблинов при приземлении.',
    barrel_log_front: 'Сработало, но бревно перед башней задело не всех сразу. Кидай прямо на башню.',
    barrel_log_miss: 'Мимо: гоблины приземлились и бьют башню.',
    barrel_fireball: 'Сработало, но 4 эликсира за бочку в 3 — минус в размене. Бревно за 2 лучше.',
    barrel_troops: 'Гоблины быстрее: пока войска подходят, башня получает урон.',
    balloon_perfect: 'Мушкетёр за башней вместе с башней сбили Шар раньше, чем он ударил.',
    balloon_front: 'Сработало, но Шар успел ударить по башне. За башней мушкетёр в безопасности.',
    balloon_far: 'Мушкетёр не достаёт — Шар бьёт башню.',
    balloon_no_air: 'Эта карта не бьёт по воздуху — Шар её не заметил.',
    giant_perfect: 'Пушка в центре утянула Гиганта, башня цела.',
    giant_cannon_ok: 'Сработало, но не из центра — башни стреляли меньше.',
    giant_far: 'Мимо: Гигант дошёл до башни.',
    giant_mp: 'Сработало: Мини-ПЕККА разобрала Гиганта, но башня получила урон.',
    giant_weak: 'Слабо против Гиганта — он дошёл до башни.',
    xbow_perfect: 'Рыцарь сразу на мосту — Икс-лук сломан без урона.',
    xbow_late: 'Сработало, но поздно: лук успел пострелять. Ставь танка прямо у моста.',
    xbow_far: 'Мимо: Икс-лук бьёт башню издалека.',
    xbow_fireball: 'Помогло, но лук выжил и пострелял. Танк у моста надёжнее.',
    xbow_skel: 'Икс-лук сносит скелетов за секунду.',
    xbow_cannon: 'Пушка не достаёт до лука — он бьёт башню с чужой стороны.',
    miner_perfect: 'Скелеты вплотную — Шахтёр убран за 1 эликсир.',
    miner_skel_ok: 'Сработало, но скелеты шли дольше — башня получила лишний удар.',
    miner_far: 'Мимо: Шахтёр бьёт башню.',
    miner_knight: 'Сработало, но 3 эликсира за 3 — нейтрально. Скелеты дешевле.',
    miner_log: 'Сработало: бревно с башни задело Шахтёра.',
    miner_fireball: 'Огненный шар не убивает Шахтёра — и 4 эликсира за 3.',
    elixir_perfect: 'Голем из тыла — пока дойдёт, наберёшь эликсир на поддержку.',
    elixir_golem_mid: 'Неплохо, но из самого тыла дольше идти — успеешь собрать пуш.',
    elixir_golem_bridge: 'Голем у моста — соперник разменяет его до подхода поддержки.',
    elixir_hog: 'Тоже вариант: эликсир не сгорает. Но тяж из тыла даёт пуш сильнее.',
    elixir_musk: 'Сработало: эликсир не сгорает, а мушкетёр поддержит следующую атаку.',
    elixir_musk_front: 'Мушкетёр у моста — лёгкая цель для заклинания.',
    elixir_skel: '1 эликсир не решает: 9 так и копятся и сгорают.',
  },
  uk: {
    hog_perfect: 'Гармата в центрі — Хог звернув до неї й загинув під вежами.',
    hog_cannon_ok: 'Спрацювало, але гармата не в центрі: частину урону вона прийняла сама. Центр, 4 клітинки від річки — надійніше.',
    hog_cannon_far: 'Гармата далеко — Хог її не помітив і доїхав до вежі.',
    hog_knight: 'Лицар добив Хога, але вежа встигла отримати урон. Будівля в центрі захищає без втрат.',
    hog_knight_far: 'Лицар не встиг — Хог доїхав до вежі.',
    hog_skeletons: 'Хог б\'є лише будівлі: скелетів він просто проігнорував.',
    hog_fireball: 'Вогняна куля не вбиває Хога — він доїхав до вежі.',
    mk_perfect: 'Скелети в центрі відвели Мега-лицаря під вогонь обох веж.',
    mk_skel_ok: 'Спрацювало, але скелети не в центрі — по лицарю стріляла одна вежа.',
    mk_skel_far: 'Повз: Мега-лицар пішов просто на вежу.',
    mk_cannon: 'Спрацювало: гармата в центрі теж тягне лицаря. Але 3 еліксиру проти 1 у скелетів.',
    mk_musk: 'Мега-лицар стрибнув і розчавив мушкетерку — дорогий розмін.',
    mk_fireball: 'Вогняна куля не вбиває Мега-лицаря — він дійшов до вежі.',
    barrel_perfect: 'Колода з вежі покотилася вперед і знесла гоблінів під час приземлення.',
    barrel_log_front: 'Спрацювало, але колода перед вежею зачепила не всіх одразу. Кидай просто на вежу.',
    barrel_log_miss: 'Повз: гобліни приземлилися й б\'ють вежу.',
    barrel_fireball: 'Спрацювало, але 4 еліксиру за бочку в 3 — мінус у розміні. Колода за 2 краща.',
    barrel_troops: 'Гобліни швидші: поки війська підходять, вежа отримує урон.',
    balloon_perfect: 'Мушкетерка за вежею разом із вежею збили Кулю раніше, ніж вона вдарила.',
    balloon_front: 'Спрацювало, але Куля встигла вдарити по вежі. За вежею мушкетерка в безпеці.',
    balloon_far: 'Мушкетерка не дістає — Куля б\'є вежу.',
    balloon_no_air: 'Ця карта не б\'є по повітрю — Куля її не помітила.',
    giant_perfect: 'Гармата в центрі потягла Гіганта, вежа ціла.',
    giant_cannon_ok: 'Спрацювало, але не з центру — вежі стріляли менше.',
    giant_far: 'Повз: Гігант дійшов до вежі.',
    giant_mp: 'Спрацювало: Міні-ПЕККА розібрала Гіганта, але вежа отримала урон.',
    giant_weak: 'Слабко проти Гіганта — він дійшов до вежі.',
    xbow_perfect: 'Лицар одразу на мосту — Ікс-лук зламано без урону.',
    xbow_late: 'Спрацювало, але пізно: лук устиг постріляти. Став танка просто біля мосту.',
    xbow_far: 'Повз: Ікс-лук б\'є вежу здалеку.',
    xbow_fireball: 'Допомогло, але лук вижив і постріляв. Танк біля мосту надійніший.',
    xbow_skel: 'Ікс-лук зносить скелетів за секунду.',
    xbow_cannon: 'Гармата не дістає до лука — він б\'є вежу з чужого боку.',
    miner_perfect: 'Скелети впритул — Шахтаря прибрано за 1 еліксир.',
    miner_skel_ok: 'Спрацювало, але скелети йшли довше — вежа отримала зайвий удар.',
    miner_far: 'Повз: Шахтар б\'є вежу.',
    miner_knight: 'Спрацювало, але 3 еліксиру за 3 — нейтрально. Скелети дешевші.',
    miner_log: 'Спрацювало: колода з вежі зачепила Шахтаря.',
    miner_fireball: 'Вогняна куля не вбиває Шахтаря — і 4 еліксиру за 3.',
    elixir_perfect: 'Голем із тилу — поки дійде, набереш еліксир на підтримку.',
    elixir_golem_mid: 'Непогано, але із самого тилу довше йти — встигнеш зібрати пуш.',
    elixir_golem_bridge: 'Голем біля мосту — суперник розміняє його до підходу підтримки.',
    elixir_hog: 'Теж варіант: еліксир не згорає. Але важкий із тилу дає пуш сильніший.',
    elixir_musk: 'Спрацювало: еліксир не згорає, а мушкетерка підтримає наступну атаку.',
    elixir_musk_front: 'Мушкетерка біля мосту — легка ціль для закляття.',
    elixir_skel: '1 еліксир не вирішує: 9 так і накопичуються й згорають.',
  },
  en: {
    hog_perfect: 'The Cannon in the middle pulled the Hog and it died under your towers.',
    hog_cannon_ok: 'It worked, but the Cannon is off-center and took damage itself. Middle, 4 tiles from the river is safer.',
    hog_cannon_far: 'The Cannon was too far — the Hog ignored it and reached your tower.',
    hog_knight: 'The Knight killed the Hog, but the tower took damage. A building in the middle defends for free.',
    hog_knight_far: 'The Knight was too late — the Hog reached your tower.',
    hog_skeletons: 'The Hog only hits buildings: it just ignored the Skeletons.',
    hog_fireball: 'Fireball doesn\'t kill the Hog — it reached your tower.',
    mk_perfect: 'Skeletons in the middle pulled the Mega Knight under both towers.',
    mk_skel_ok: 'It worked, but off-center — only one tower was shooting.',
    mk_skel_far: 'Missed: the Mega Knight walked straight to your tower.',
    mk_cannon: 'It worked: a middle Cannon pulls it too. But 3 elixir vs 1 for Skeletons.',
    mk_musk: 'The Mega Knight jumped and crushed the Musketeer — a costly trade.',
    mk_fireball: 'Fireball doesn\'t kill the Mega Knight — it reached your tower.',
    barrel_perfect: 'The Log rolled forward from your tower and swept the goblins as they landed.',
    barrel_log_front: 'It worked, but the Log in front missed some goblins. Log right onto your tower.',
    barrel_log_miss: 'Missed: the goblins landed and hit your tower.',
    barrel_fireball: 'It worked, but 4 elixir for a 3-elixir barrel is a negative trade. A 2-elixir Log is better.',
    barrel_troops: 'Goblins are faster: your tower takes damage while troops walk over.',
    balloon_perfect: 'The Musketeer behind the tower and the tower itself shot the Balloon down before it hit.',
    balloon_front: 'It worked, but the Balloon got a hit in. Behind the tower the Musketeer is safe.',
    balloon_far: 'The Musketeer is out of range — the Balloon hits your tower.',
    balloon_no_air: 'This card can\'t hit air — the Balloon ignored it.',
    giant_perfect: 'A middle Cannon pulled the Giant, the tower is untouched.',
    giant_cannon_ok: 'It worked, but off-center — the towers shot less.',
    giant_far: 'Missed: the Giant reached your tower.',
    giant_mp: 'It worked: Mini PEKKA took the Giant down, but the tower took damage.',
    giant_weak: 'Too weak against a Giant — it reached your tower.',
    xbow_perfect: 'Knight right at the bridge — X-Bow broken with no damage.',
    xbow_late: 'It worked, but late: the X-Bow got shots in. Place the tank right at the bridge.',
    xbow_far: 'Missed: the X-Bow hits your tower from range.',
    xbow_fireball: 'It helped, but the X-Bow survived and shot. A tank at the bridge is safer.',
    xbow_skel: 'The X-Bow shreds Skeletons in a second.',
    xbow_cannon: 'The Cannon can\'t reach the X-Bow — it fires from their side.',
    miner_perfect: 'Skeletons right next to it — Miner handled for 1 elixir.',
    miner_skel_ok: 'It worked, but the Skeletons walked longer — the tower took an extra hit.',
    miner_far: 'Missed: the Miner hits your tower.',
    miner_knight: 'It worked, but 3 elixir for 3 is neutral. Skeletons are cheaper.',
    miner_log: 'It worked: the Log from your tower hit the Miner.',
    miner_fireball: 'Fireball doesn\'t kill the Miner — and costs 4 for 3.',
    elixir_perfect: 'Golem from the back — by the time it walks you have elixir for support.',
    elixir_golem_mid: 'Not bad, but from the very back it walks longer — more time to build the push.',
    elixir_golem_bridge: 'Golem at the bridge — they\'ll trade it before support arrives.',
    elixir_hog: 'Also an option: no elixir wasted. But a tank from the back makes a stronger push.',
    elixir_musk: 'It worked: no elixir wasted, and the Musketeer supports your next push.',
    elixir_musk_front: 'A Musketeer at the bridge is an easy spell target.',
    elixir_skel: '1 elixir doesn\'t solve it: 9 more keep piling up and burning.',
  },
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
