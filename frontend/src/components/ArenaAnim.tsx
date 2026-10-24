import { useEffect, useId, useRef, useState } from 'react'
import { useCardArt } from '../lib/cardArt'
import { emojiOf } from '../lib/sim/emoji'
import kingMe from '../assets/keepers/king-me.webp'
import kingFoe from '../assets/keepers/king-foe.webp'
import princessArt from '../assets/keepers/princess.webp'
import emblem from '../assets/clanify-emblem.webp'

/**
 * Схема арены с анимацией: куда и когда ставить карту.
 *
 * Геометрия - как в игре: поле 18×32 клетки целиком, по 15 рядов на сторону и река
 * в 2 ряда посередине; башни принцесс 3×3 с центром на 3,5 и 14,5 в 6,5 ряда от
 * своего края, король 4×4 по центру в 3 рядах от края, за ним 1 свободный ряд.
 * Радиус принцесс 7,5 клетки, короля 7. Как в игре, поле видно под углом: клетка
 * сплюснута по высоте (TILE_H), поэтому радиусы рисуются эллипсами.
 *
 * Юниты - жетоны с артом карты и кольцом стороны (свои синие, чужие красные),
 * здания - квадратные жетоны, заклинания - зона. Сцена - таймлайн: ключевые точки
 * (секунда, клетка), между ними - линейно. Башни стреляют снарядами по чужим в радиусе.
 */

export type ActorKind = 'enemy' | 'mine' | 'building' | 'spell' | 'barrel' | 'log' | 'text'

export interface Key { t: number; x: number; y: number }

export interface Actor {
  id: string
  kind: ActorKind
  label: string
  /** Карта для арта жетона - английское имя из каталога игры. */
  card?: string
  keys: Key[]
  from?: number
  to?: number
  /** Радиус жетона или зоны в клетках. */
  r?: number
}

/** Подсветка клетки «сюда»; size - сторона в клетках (здание 2×2). */
export interface Mark { x: number; y: number; from: number; to: number; label?: string; size?: number }

/** Линейка «столько клеток»: от y1 до y2 по столбцу x. */
export interface Ruler { x: number; y1: number; y2: number; label: string; from: number; to: number }

export interface Scene { duration: number; actors: Actor[]; marks?: Mark[]; rulers?: Ruler[]; noShots?: boolean }

/** Ширина клетки в пикселях схемы. */
export const TILE = 20
/** Высота клетки: в игре поле видно под углом, клетка сплюснута примерно до 0,8. */
export const TILE_H = 16
export const VIEW_TOP = 0
const VIEW_ROWS = 32
export const W = 18 * TILE
export const H = VIEW_ROWS * TILE_H
const RIVER_TOP = 15
const RIVER_BOTTOM = 17

export const TOWERS = [
  { id: 'pl', x: 3.5, y: 25.5, size: 3, range: 7.5, king: false, enemy: false },
  { id: 'pr', x: 14.5, y: 25.5, size: 3, range: 7.5, king: false, enemy: false },
  { id: 'k', x: 9, y: 29, size: 4, range: 7, king: true, enemy: false },
  { id: 'el', x: 3.5, y: 6.5, size: 3, range: 7.5, king: false, enemy: true },
  { id: 'er', x: 14.5, y: 6.5, size: 3, range: 7.5, king: false, enemy: true },
  { id: 'ek', x: 9, y: 3, size: 4, range: 7, king: true, enemy: true },
]

export const px = (x: number) => x * TILE
export const py = (y: number) => (y - VIEW_TOP) * TILE_H

function span(a: Actor) {
  return { from: a.from ?? a.keys[0].t, to: a.to ?? a.keys[a.keys.length - 1].t }
}

function at(a: Actor, t: number): { x: number; y: number } | null {
  const { from, to } = span(a)
  if (t < from || t > to) return null
  const k = a.keys
  if (t <= k[0].t) return { x: k[0].x, y: k[0].y }
  for (let i = 1; i < k.length; i++) {
    if (t <= k[i].t) {
      const f = (t - k[i - 1].t) / Math.max(0.0001, k[i].t - k[i - 1].t)
      return { x: k[i - 1].x + (k[i].x - k[i - 1].x) * f, y: k[i - 1].y + (k[i].y - k[i - 1].y) * f }
    }
  }
  const last = k[k.length - 1]
  return { x: last.x, y: last.y }
}

function lastPos(a: Actor): { x: number; y: number } {
  const k = a.keys[a.keys.length - 1]
  return { x: k.x, y: k.y }
}

/** Фишки - про свою половину: показываем ряды от края чужой половины до своего края. */
const TIP_ROWS: [number, number] = [11, 32]

/**
 * @param at Показать один кадр на этой секунде (без анимации).
 * @param loop false - сыграть сцену один раз и встать на последнем кадре, позвав onEnd.
 * @param rows Какие ряды поля показать - то же поле, просто кадр.
 */
export function ArenaAnim({ scene, paused = false, at: frameAt, loop = true, onEnd, rows = TIP_ROWS }: {
  scene: Scene
  paused?: boolean
  at?: number
  loop?: boolean
  onEnd?: () => void
  rows?: [number, number]
}) {
  const still = frameAt ?? (paused ? scene.duration * 0.35 : null)
  const [t, setT] = useState(still ?? 0)
  const startRef = useRef<number | null>(null)
  const endRef = useRef(onEnd)
  endRef.current = onEnd
  const uid = useId().replace(/:/g, '')
  const art = useCardArt()

  useEffect(() => {
    if (still !== null) { setT(still); return }
    let raf = 0
    let done = false
    startRef.current = null
    const tick = (now: number) => {
      if (startRef.current === null) startRef.current = now
      const el = (now - startRef.current) / 1000
      if (!loop && el >= scene.duration) {
        setT(scene.duration)
        if (!done) { done = true; endRef.current?.() }
        return
      }
      setT(loop ? el % scene.duration : el)
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [scene, still, loop])

  const live = scene.actors.map(a => ({ a, p: at(a, t) })).filter(x => x.p) as { a: Actor; p: { x: number; y: number } }[]
  const enemies = live.filter(x => x.a.kind === 'enemy')

  // Снаряды: башня бьёт ближайшего чужого в радиусе (до края жетона)
  const shots: { from: { x: number; y: number }; to: { x: number; y: number }; key: string }[] = []
  if (!scene.noShots) {
    for (const tw of TOWERS.filter(tw => !tw.enemy)) {
      const target = enemies
        .map(e => ({ e, d: Math.hypot(e.p.x - tw.x, e.p.y - tw.y) - (e.a.r ?? 0.6) }))
        .filter(x => x.d <= tw.range)
        .sort((a, b) => a.d - b.d)[0]
      if (target) shots.push({ from: tw, to: target.e.p, key: tw.id })
    }
  }

  // Вспышка, где юнит погиб: 0,4 секунды после его конца
  const poofs = scene.actors
    .filter(a => a.kind === 'enemy' && span(a).to < scene.duration - 0.05)
    .map(a => ({ a, dt: t - span(a).to }))
    .filter(x => x.dt >= 0 && x.dt <= 0.45)

  const g = (name: string) => `${name}-${uid}`

  return (
    <svg className="arena" viewBox={`0 ${py(rows[0])} ${W} ${py(rows[1]) - py(rows[0])}`} role="img" aria-label="arena">
      <ArenaDefs g={g} />
      <ArenaGround t={t} g={g} />

      {/* Радиусы своих башен - едва заметно, ярче у той, что стреляет */}
      {TOWERS.filter(tw => !tw.enemy).map(tw => (
        <ellipse key={`r${tw.id}`} cx={px(tw.x)} cy={py(tw.y)} rx={tw.range * TILE} ry={tw.range * TILE_H} fill="none"
          stroke={shots.some(s => s.key === tw.id) ? 'rgba(255,236,150,0.75)' : 'rgba(255,255,255,0.22)'} strokeDasharray="3 7" />
      ))}

      {TOWERS.map(tw => <TowerArt key={tw.id} x={tw.x} y={tw.y} size={tw.size} king={tw.king} enemy={tw.enemy} g={g} />)}

      {(scene.rulers ?? []).filter(r => t >= r.from && t <= r.to).map((r, i) => (
        <g key={`ru${i}`}>
          <line x1={px(r.x)} x2={px(r.x)} y1={py(r.y1)} y2={py(r.y2)} stroke="#ffe08a" strokeWidth="2" strokeDasharray="4 3" />
          {Array.from({ length: Math.round(Math.abs(r.y2 - r.y1)) + 1 }, (_, k) => (
            <line key={k} x1={px(r.x) - 4} x2={px(r.x) + 4} y1={py(Math.min(r.y1, r.y2) + k)} y2={py(Math.min(r.y1, r.y2) + k)} stroke="#ffe08a" strokeWidth="2" />
          ))}
          <Pill x={px(r.x) + 34} y={py((r.y1 + r.y2) / 2)} text={r.label} tone="gold" />
        </g>
      ))}

      {(scene.marks ?? []).filter(m => t >= m.from && t <= m.to).map((m, i) => {
        const pulse = 0.5 + 0.5 * Math.sin(t * 9)
        const sw = (m.size ?? 1) * TILE, sh = (m.size ?? 1) * TILE_H
        return (
          <g key={`m${i}`}>
            <rect x={px(m.x) - sw / 2 - 2} y={py(m.y) - sh / 2 - 2} width={sw + 4} height={sh + 4} rx="4"
              fill={`rgba(255,224,138,${0.25 + 0.35 * pulse})`} stroke="#fff3c4" strokeWidth="2.5" />
            {m.label && <Pill x={px(m.x)} y={py(m.y) - sh / 2 - 13} text={m.label} tone="gold" />}
          </g>
        )
      })}

      {shots.map(s => {
        const f = (t * 2.6) % 1
        const x = px(s.from.x) + (px(s.to.x) - px(s.from.x)) * f
        const y = py(s.from.y - 0.6) + (py(s.to.y) - py(s.from.y - 0.6)) * f
        return <circle key={s.key} cx={x} cy={y} r="3.2" fill="#ffe08a" filter={`url(#${g('glow')})`} />
      })}

      {live.map(({ a, p }) => a.kind === 'text'
        ? <FloatText key={a.id} a={a} x={px(p.x)} y={py(p.y)} k={(t - span(a).from) / Math.max(0.01, span(a).to - span(a).from)} />
        : <Token key={a.id} a={a} x={px(p.x)} y={py(p.y)} t={t} g={g} art={a.card ? artFor(art, a.card) : undefined} />)}

      {poofs.map(({ a, dt }) => {
        const p = lastPos(a)
        const k = dt / 0.45
        return <circle key={`p${a.id}`} cx={px(p.x)} cy={py(p.y)} r={10 + 18 * k} fill="none" stroke="#fff" strokeWidth={3 * (1 - k)} opacity={1 - k} />
      })}
    </svg>
  )
}

/** Градиенты и узоры арены; g - имя с уникальным суффиксом, чтобы две арены на экране не путались. */
export function ArenaDefs({ g }: { g: (n: string) => string }) {
  return (
    <defs>
      {/* Поле в шахматку, как в игре (клетка - TILE × TILE_H), но в цветах Clanify: ночной индиго */}
      <pattern id={g('tiles')} width={TILE * 2} height={TILE_H * 2} patternUnits="userSpaceOnUse">
        <rect width={TILE * 2} height={TILE_H * 2} fill="#262e66" />
        <rect width={TILE} height={TILE_H} fill="#2c3574" />
        <rect x={TILE} y={TILE_H} width={TILE} height={TILE_H} fill="#2c3574" />
      </pattern>
      <linearGradient id={g('river')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#1a8fd0" />
        <stop offset="0.5" stopColor="#4fd2ff" />
        <stop offset="1" stopColor="#1a8fd0" />
      </linearGradient>
      {/* Дорожки - светящиеся полосы в фирменном переходе фиолетовый → синий → голубой */}
      <linearGradient id={g('track')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#9c6bff" />
        <stop offset="0.5" stopColor="#5b8cff" />
        <stop offset="1" stopColor="#3fc6f2" />
      </linearGradient>
      <linearGradient id={g('trackH')} x1="0" y1="0" x2="1" y2="0">
        <stop offset="0" stopColor="#9c6bff" />
        <stop offset="0.5" stopColor="#5b8cff" />
        <stop offset="1" stopColor="#9c6bff" />
      </linearGradient>
      <radialGradient id={g('shade')} cx="0.5" cy="0.5" r="0.75">
        <stop offset="0.55" stopColor="#0b0f2a" stopOpacity="0" />
        <stop offset="1" stopColor="#0b0f2a" stopOpacity="0.55" />
      </radialGradient>
      <radialGradient id={g('spell')}>
        <stop offset="0" stopColor="#ffe08a" stopOpacity="0.55" />
        <stop offset="1" stopColor="#ffb347" stopOpacity="0.12" />
      </radialGradient>
      {/* Жетон юнита рисуется в масштабе радиуса: круг и скруглённый квадрат радиусом 1 */}
      <clipPath id={g('tok')}><circle r="1" /></clipPath>
      <clipPath id={g('tokSq')}><rect x="-1" y="-1" width="2" height="2" rx="0.28" /></clipPath>
      <radialGradient id={g('fire')} cx="0.4" cy="0.38">
        <stop offset="0" stopColor="#fff7c2" />
        <stop offset="0.45" stopColor="#ffb020" />
        <stop offset="1" stopColor="#e2401b" />
      </radialGradient>
      <filter id={g('glow')} x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="2.2" result="b" />
        <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
      </filter>
    </defs>
  )
}

const LANES = [3.5, 14.5]

/**
 * Тренировочная арена Clanify: раскладка как в игре (дорожки от мостов к башням и
 * петля за королями, площадки под башнями, река в 2 ряда, мосты над линиями), а
 * цвета - фирменные: индиго-поле, светящиеся дорожки, неоновая река, фиолетовые
 * мосты, эмблема Clanify в центре и световые барьеры в углах за королями.
 */
export function ArenaGround({ t, g }: { t: number; g: (n: string) => string }) {
  // Дорожка - полоса в 1 клетку: вдоль линий от петли за королём до моста и поперёк за королями
  const paths: { x: number; y: number; w: number; h: number }[] = []
  for (const lx of LANES) {
    paths.push({ x: lx - 0.5, y: 3, w: 1, h: RIVER_TOP - 3 })
    paths.push({ x: lx - 0.5, y: RIVER_BOTTOM, w: 1, h: 29 - RIVER_BOTTOM })
  }
  paths.push({ x: 3, y: 3, w: 12, h: 1 }, { x: 3, y: 28, w: 12, h: 1 })
  const pads = TOWERS.map(tw => ({ x: tw.x - tw.size / 2, y: tw.y - tw.size / 2, s: tw.size, enemy: tw.enemy }))
  const pulse = 0.5 + 0.5 * Math.sin(t * 2)
  return (
    <>
      <rect width={W} height={H} fill={`url(#${g('tiles')})`} />

      {paths.map((r, i) => (
        <rect key={`p${i}`} x={px(r.x)} y={py(r.y)} width={r.w * TILE} height={r.h * TILE_H} rx="2"
          fill={`url(#${g(r.w > r.h ? 'trackH' : 'track')})`} opacity="0.32" />
      ))}
      {/* Осевая линия дорожки - тонкий световой шов */}
      {paths.map((r, i) => r.w > r.h
        ? <line key={`pl${i}`} x1={px(r.x) + 4} x2={px(r.x + r.w) - 4} y1={py(r.y + 0.5)} y2={py(r.y + 0.5)} stroke="#b9a6ff" strokeOpacity="0.45" strokeDasharray="6 6" />
        : <line key={`pl${i}`} x1={px(r.x + 0.5)} x2={px(r.x + 0.5)} y1={py(r.y) + 4} y2={py(r.y + r.h) - 4} stroke="#b9a6ff" strokeOpacity="0.45" strokeDasharray="6 6" />)}
      {/* Площадки под башнями - их клетки, обведены цветом стороны */}
      {pads.map((p, i) => (
        <rect key={`pad${i}`} x={px(p.x)} y={py(p.y)} width={p.s * TILE} height={p.s * TILE_H} rx="4"
          fill="#1a2050" stroke={p.enemy ? '#ff5d7a' : '#5b8cff'} strokeOpacity="0.7" strokeWidth="1.4" />
      ))}

      {/* Река: тёмные берега, неоновая вода, кристаллы у кромки */}
      <rect y={py(RIVER_TOP)} width={W} height={(RIVER_BOTTOM - RIVER_TOP) * TILE_H} fill="#161b46" />
      <rect y={py(RIVER_TOP + 0.3)} width={W} height={(RIVER_BOTTOM - RIVER_TOP - 0.6) * TILE_H} fill={`url(#${g('river')})`} />
      <rect y={py(RIVER_TOP + 0.3)} width={W} height={1.5} fill="#bdf0ff" opacity={0.5 + 0.3 * pulse} />
      <rect y={py(RIVER_BOTTOM - 0.3) - 1.5} width={W} height={1.5} fill="#bdf0ff" opacity={0.5 + 0.3 * pulse} />
      {[0, 1].map(i => (
        <path key={i} d={wave(py(RIVER_TOP + 0.75 + i * 0.5), t * 30 + i * 40)} fill="none" stroke="rgba(255,255,255,0.55)" strokeWidth="1.3" />
      ))}
      {[1.2, 7.3, 10.4, 16.9].map((sx, i) => {
        const cx = px(sx), cy = py(i % 2 ? RIVER_TOP + 0.2 : RIVER_BOTTOM - 0.2)
        return <path key={`st${i}`} d={`M${cx - 7} ${cy} L${cx - 2} ${cy - 4} L${cx + 6} ${cy - 2} L${cx + 7} ${cy + 2} L${cx} ${cy + 4} Z`}
          fill="#3c4596" stroke="#7b8cff" strokeWidth="1" />
      })}

      {/* Эмблема Clanify посреди поля - как герб арены в игре */}
      <g opacity="0.95">
        <ellipse cx={px(9)} cy={py(16)} rx={2.1 * TILE} ry={2.1 * TILE_H + 4} fill="#141a45" stroke={`url(#${g('trackH')})`} strokeWidth="2" />
        <ellipse cx={px(9)} cy={py(16)} rx={2.1 * TILE} ry={2.1 * TILE_H + 4} fill="none" stroke="#9c6bff" strokeWidth="2" opacity={0.4 + 0.4 * pulse} />
        <image href={emblem} x={px(9) - 26} y={py(16) - 27} width={52} height={53} />
      </g>

      {/* Мосты: фиолетовый металл с настилом, в 2 клетки шириной над каждой линией */}
      {LANES.map(lx => (
        <g key={lx}>
          <rect x={px(lx - 1)} y={py(RIVER_TOP - 0.45)} width={2 * TILE} height={(RIVER_BOTTOM - RIVER_TOP + 0.9) * TILE_H} rx="3"
            fill="#5a45c9" stroke="#2a1d73" strokeWidth="1.6" />
          {[-0.5, 0, 0.5].map(k => (
            <line key={k} x1={px(lx + k)} x2={px(lx + k)} y1={py(RIVER_TOP - 0.4)} y2={py(RIVER_BOTTOM + 0.4)} stroke="#8f75ff" strokeWidth="1.4" />
          ))}
          <rect x={px(lx - 1)} y={py(RIVER_TOP - 0.45)} width={2 * TILE} height={3} fill="#b6a3ff" />
        </g>
      ))}

      {/* Световые барьеры в углах за королями */}
      {[0.35, 31.65].map(fy => [1.5, 12.5].map(fx => <Fence key={`${fx}-${fy}`} x={fx} y={fy} pulse={pulse} />))}
      <rect width={W} height={H} fill={`url(#${g('shade')})`} pointerEvents="none" />
    </>
  )
}

function Fence({ x, y, pulse }: { x: number; y: number; pulse: number }) {
  const x0 = px(x), x1 = px(x + 4), cy = py(y)
  return (
    <g>
      <rect x={x0} y={cy - 1.5} width={x1 - x0} height={3} rx="1.5" fill="#3fc6f2" opacity={0.35 + 0.35 * pulse} />
      {[x0, (x0 + x1) / 2, x1 - 4].map(fx => <rect key={fx} x={fx} y={cy - 5} width={4} height={10} rx="1.5" fill="#2a3070" stroke="#7b8cff" strokeWidth="1" />)}
    </g>
  )
}

function wave(y: number, phase: number): string {
  let d = `M0 ${y}`
  for (let x = 0; x <= W; x += 12) d += ` L${x} ${y + Math.sin((x + phase) / 14) * 2.2}`
  return d
}

export function Pill({ x, y, text, tone }: { x: number; y: number; text: string; tone: 'gold' | 'red' | 'blue' }) {
  if (!text) return null
  const w = Math.max(30, text.length * 6.4 + 14)
  const fill = tone === 'gold' ? '#3a2a08' : tone === 'red' ? '#4a0f18' : '#0f2350'
  const stroke = tone === 'gold' ? '#ffe08a' : tone === 'red' ? '#ff7a85' : '#7fb5ff'
  return (
    <g>
      <rect x={x - w / 2} y={y - 8} width={w} height={16} rx="8" fill={fill} stroke={stroke} strokeWidth="1.2" opacity="0.95" />
      <text x={x} y={y + 4} textAnchor="middle" fontSize="10.5" fontWeight="800" fill="#fff">{text}</text>
    </g>
  )
}

/** Големит - кусок Голема: своей карты нет, берём арт Голема. */
const ART_OF: Record<string, string> = { Golemite: 'Golem' }

export const artFor = (art: Record<string, string>, card: string) => art[card] ?? art[ART_OF[card] ?? '']

/**
 * Портрет карты в жетоне, рисуется в масштабе радиуса r вокруг (0, 0): арт из
 * каталога игры, обрезанный по кругу (здания - по квадрату) так, чтобы рамка карты
 * ушла за край, а лицо осталось. Пока арт не загрузился - смайлик карты.
 */
export function UnitArt({ card, r, art, square = false, g }: {
  card: string; r: number; art?: string; square?: boolean; g: (n: string) => string
}) {
  if (!art) return <text y={1} textAnchor="middle" dominantBaseline="central" fontSize={r * 1.35}>{emojiOf(card)}</text>
  return (
    <g transform={`scale(${r})`}>
      <image href={art} x={-1.25} y={-1.3} width={2.5} height={3} clipPath={`url(#${g(square ? 'tokSq' : 'tok')})`} />
    </g>
  )
}

/** Огненный шар: раскалённое ядро и хвост пламени против хода. angle - куда летит, в градусах. */
export function FireballIcon({ size, angle = -90, g }: { size: number; angle?: number; g: (n: string) => string }) {
  return (
    <g transform={`rotate(${angle}) scale(${size / 12})`}>
      <path d="M-15 0 Q-8 -7 0 -6.5 A6.5 6.5 0 0 1 0 6.5 Q-8 7 -15 0 Z" fill="#ff8a1f" opacity="0.75" />
      <path d="M-10 0 Q-5 -4 0 -4 A4 4 0 0 1 0 4 Q-5 4 -10 0 Z" fill="#ffd34d" opacity="0.8" />
      <circle r="6" fill={`url(#${g('fire')})`} stroke="#b8360f" strokeWidth="0.8" />
    </g>
  )
}

/**
 * Вихрь Торнадо на земле: круг действия (эллипс - поле видно под углом) и закрученные
 * рукава, вращаются; k - сколько осталось от действия (0..1), гаснет к концу.
 */
export function TornadoSwirl({ rx, ry, t, k = 1 }: { rx: number; ry: number; t: number; k?: number }) {
  const arms = [0, 1, 2, 3].map(i => {
    const a0 = t * 7 + (i * Math.PI) / 2
    const pts = Array.from({ length: 14 }, (_, j) => {
      const f = j / 13
      const a = a0 + f * 2.6
      return `${(Math.cos(a) * rx * (1 - f * 0.85)).toFixed(1)},${(Math.sin(a) * ry * (1 - f * 0.85)).toFixed(1)}`
    })
    return `M${pts.join(' L')}`
  })
  return (
    <g opacity={0.35 + 0.65 * k}>
      <ellipse rx={rx} ry={ry} fill="rgba(63,198,242,0.10)" stroke="#9fe6ff" strokeWidth="1.5" strokeDasharray="6 5" />
      {arms.map((d, i) => <path key={i} d={d} fill="none" stroke={i % 2 ? '#b9a6ff' : '#9fe6ff'} strokeWidth="2.2" strokeLinecap="round" opacity="0.8" />)}
      <ellipse rx={rx * 0.16} ry={ry * 0.16} fill="#dff7ff" opacity="0.7" />
    </g>
  )
}

/** Бочка гоблинов: деревянная, с обручами и гоблином, выглядывающим сверху. */
export function BarrelIcon({ size }: { size: number }) {
  return (
    <g transform={`scale(${size / 20})`}>
      <ellipse cx="0" cy="-9" rx="5" ry="4" fill="#5cbf3a" stroke="#2f6a1c" strokeWidth="1" />
      <path d="M-5 -10 l-4 -3 l3 4 M5 -10 l4 -3 l-3 4" fill="#5cbf3a" stroke="#2f6a1c" strokeWidth="1" />
      <rect x="-8" y="-8" width="16" height="18" rx="5" fill="#a5642f" stroke="#4a2a12" strokeWidth="1.4" />
      {[-4.5, 4].map(by => <rect key={by} x="-8.4" y={by} width="16.8" height="2.6" rx="1" fill="#3b3f46" />)}
      <line x1="-2.5" x2="-2.5" y1="-7" y2="9" stroke="#7d4a20" strokeWidth="1" />
      <line x1="2.5" x2="2.5" y1="-7" y2="9" stroke="#7d4a20" strokeWidth="1" />
    </g>
  )
}

/** Бомба Шара: чёрное ядро с фитилём и искрой. */
export function BombIcon({ size, spark }: { size: number; spark: number }) {
  return (
    <g transform={`scale(${size / 18})`}>
      <circle r="7.5" fill="#23262e" stroke="#000" strokeWidth="1" />
      <circle cx="-2.6" cy="-2.6" r="2.2" fill="#5b616e" />
      <path d="M4.5 -5.5 q3 -4 6 -3" fill="none" stroke="#8a6a3a" strokeWidth="1.6" />
      <circle cx="10.5" cy="-8.5" r={2 + spark * 1.6} fill="#ffb020" />
      <circle cx="10.5" cy="-8.5" r={1 + spark * 0.6} fill="#fff3b0" />
    </g>
  )
}

/** Вспышка взрыва: звезда-всполох, k - время от 0 до 1. */
export function Burst({ r, k }: { r: number; k: number }) {
  const pts = Array.from({ length: 16 }, (_, i) => {
    const a = (i / 16) * Math.PI * 2
    const rr = (i % 2 ? 0.55 : 1) * r * (0.6 + 0.4 * k)
    return `${(Math.cos(a) * rr).toFixed(1)},${(Math.sin(a) * rr * 0.8).toFixed(1)}`
  }).join(' ')
  return (
    <g opacity={1 - k}>
      <polygon points={pts} fill="#ffcc33" stroke="#ff7a1a" strokeWidth="2" />
      <ellipse rx={r * 0.35} ry={r * 0.28} fill="#fff3b0" />
    </g>
  )
}

/** Хозяин башни - портрет в круглом медальоне посреди площадки: кольцо цвета стороны, золотой ободок. */
export function Medallion({ src, r, color, g }: { src: string; r: number; color: string; g: (n: string) => string }) {
  return (
    <g>
      <circle r={r + 3} fill={color} stroke="#0d1430" strokeWidth="1.2" />
      <circle r={r + 1} fill="#ffd34d" />
      <g transform={`scale(${r})`}>
        <image href={src} x={-1} y={-1} width={2} height={2} clipPath={`url(#${g('tok')})`} />
      </g>
    </g>
  )
}

export const keeperArt = (king: boolean, enemy: boolean) => (king ? (enemy ? kingFoe : kingMe) : princessArt)

/**
 * Размеры башни в пикселях: фасад и площадка сверху - как башня в игре под тем же
 * углом, чуть уже своих клеток. bottom - низ фасада по нижнему краю клеток.
 */
function towerBox(y: number, size: number, king: boolean) {
  const bottom = py(y + size / 2) - 1
  const w = size * TILE * (king ? 0.84 : 0.86)
  const front = size * TILE_H * (king ? 0.62 : 0.64)
  const deck = size * TILE_H * (king ? 0.5 : 0.52)
  return { bottom, w, front, deck, top: bottom - front - deck, face: king ? 18 : 15 }
}

/** Верх башни над медальоном и флажками - над ним рисуется полоска здоровья. */
export function towerTop(y: number, size: number, king: boolean): number {
  const b = towerBox(y, size, king)
  return Math.min(b.top - 20, b.top + b.deck * 0.4 - b.face - 8)
}

const TEAM = {
  me: { main: '#3d7be0', dark: '#1f4fa8' },
  enemy: { main: '#e04848', dark: '#9c2323' },
}

/**
 * Башня в духе игры: каменный фасад с кладкой и зубцами, полоса и флажки цвета
 * стороны, золотая корона на гербе, каменная площадка, посреди неё - хозяин башни
 * в круглом медальоне: король или принцесса.
 */
export function TowerArt({ x, y, size, king, enemy = false, dead = false, g }: {
  x: number; y: number; size: number; king: boolean; enemy?: boolean; dead?: boolean; g: (n: string) => string
}) {
  if (dead) {
    // Развалины: груда камней на площадке башни
    return (
      <g>
        {[[-0.7, 0.1, 9], [0.5, 0.25, 8], [0, -0.35, 10], [-0.2, 0.55, 7], [0.85, -0.3, 6]].map(([dx, dy, r], i) => (
          <ellipse key={i} cx={px(x + dx)} cy={py(y + dy)} rx={r} ry={r * 0.7} fill={i % 2 ? '#9aa1ad' : '#b9c0cb'} stroke="#6b717d" strokeWidth="1" />
        ))}
      </g>
    )
  }
  const { bottom, w, front, deck, top, face } = towerBox(y, size, king)
  const cx = px(x)
  const team = TEAM[enemy ? 'enemy' : 'me']
  const l = cx - w / 2
  const ft = bottom - front
  const merlons = king ? 6 : 5
  const mw = w / (merlons * 2 - 1)
  const k = king ? 1.25 : 1
  const pcy = ft + front * 0.58
  return (
    <g>
      <ellipse cx={cx} cy={bottom} rx={w * 0.56} ry={5} fill="rgba(0,0,0,0.28)" />
      {/* Флажки по задним углам */}
      {[-1, 1].map(sd => {
        const fx = cx + sd * (w / 2 - 3)
        return (
          <g key={sd}>
            <line x1={fx} y1={top + 3} x2={fx} y2={top - 13} stroke="#5b3a1e" strokeWidth="1.6" />
            <path d={`M${fx} ${top - 13} l${sd * 10} 3.2 l${-sd * 10} 3.2 z`} fill={team.main} stroke={team.dark} strokeWidth="0.8" />
          </g>
        )
      })}
      {/* Площадка: каменный борт и пол в тени борта */}
      <rect x={l} y={top} width={w} height={deck + 2} rx="3" fill="#d6dbe4" stroke="#4f5768" strokeWidth="1.2" />
      <rect x={l + 4} y={top + 3} width={w - 8} height={deck - 3} rx="2" fill="#b4bccb" />
      <rect x={l + 4} y={top + 3} width={w - 8} height={3} rx="1.5" fill="#9ca5b6" />
      {/* Фасад: кладка, полоса цвета стороны, зубцы по краю, герб с короной */}
      <rect x={l} y={ft} width={w} height={front} rx="2" fill="#aab2c1" stroke="#4f5768" strokeWidth="1.2" />
      <rect x={l + 1} y={bottom - front * 0.3} width={w - 2} height={front * 0.3 - 1} fill="#8f98aa" />
      {[0.42, 0.7].map(f => <line key={f} x1={l + 2} x2={l + w - 2} y1={ft + front * f} y2={ft + front * f} stroke="#7d8698" strokeWidth="1" />)}
      {Array.from({ length: Math.floor(w / 12) }, (_, i) => (
        <line key={i} x1={l + 6 + i * 12 + (i % 2) * 3} x2={l + 6 + i * 12 + (i % 2) * 3} y1={ft + front * 0.42} y2={ft + front * 0.7} stroke="#7d8698" strokeWidth="1" />
      ))}
      <rect x={l} y={ft} width={w} height={5} fill={team.main} />
      <rect x={l} y={ft + 5} width={w} height={1.5} fill={team.dark} />
      {Array.from({ length: merlons }, (_, i) => (
        <rect key={i} x={l + i * 2 * mw} y={ft - 5} width={mw} height={7} rx="1" fill="#dde2ea" stroke="#4f5768" strokeWidth="1" />
      ))}
      <rect x={cx - 9 * k} y={pcy - 6.5 * k} width={18 * k} height={13 * k} rx={3 * k} fill={team.main} stroke={team.dark} strokeWidth="1" />
      <path d={`M${cx - 6 * k} ${pcy + 3.5 * k} L${cx - 7 * k} ${pcy - 3 * k} L${cx - 3.5 * k} ${pcy} L${cx} ${pcy - 4.5 * k} L${cx + 3.5 * k} ${pcy} L${cx + 7 * k} ${pcy - 3 * k} L${cx + 6 * k} ${pcy + 3.5 * k} Z`}
        fill="#ffd34d" stroke="#a8740f" strokeWidth="1" />
      {/* Хозяин башни посреди площадки, поверх зубцов - как король на башне в игре */}
      <g transform={`translate(${cx} ${top + deck * 0.45})`}><Medallion src={keeperArt(king, enemy)} r={face} color={team.main} g={g} /></g>
    </g>
  )
}

/** Всплывающий урон: поднимается и гаснет. */
function FloatText({ a, x, y, k }: { a: Actor; x: number; y: number; k: number }) {
  return (
    <text x={x} y={y - 18 * k} textAnchor="middle" fontSize="15" fontWeight="900" fill="#ff6b78"
      stroke="#3a0610" strokeWidth="3" paintOrder="stroke" opacity={Math.min(1, 2 * (1 - k))}>{a.label}</text>
  )
}

function Token({ a, x, y, t, g, art }: { a: Actor; x: number; y: number; t: number; g: (n: string) => string; art?: string }) {
  const r = (a.r ?? 0.7) * TILE
  if (a.kind === 'spell') {
    return (
      <g>
        <ellipse cx={x} cy={y} rx={r} ry={r * (TILE_H / TILE)} fill={`url(#${g('spell')})`} stroke="#ffd35a" strokeWidth="2" />
        {a.card === 'Fireball' && <g transform={`translate(${x} ${y})`}><FireballIcon size={Math.min(22, r)} g={g} /></g>}
        {a.card === 'Tornado' && <g transform={`translate(${x} ${y})`}><TornadoSwirl rx={r} ry={r * (TILE_H / TILE)} t={t} /></g>}
        {a.label && <Pill x={x} y={y + r * (TILE_H / TILE) + 10} text={a.label} tone="gold" />}
      </g>
    )
  }
  if (a.kind === 'log') {
    // Бревно - широкое и катится: полоска с кольцами-годами
    const w = (a.r ?? 2) * TILE * 2
    return (
      <g>
        <rect x={x - w / 2} y={y - 7} width={w} height={14} rx="7" fill="#9b6a3a" stroke="#5c3a1f" strokeWidth="2" />
        {[-0.3, 0, 0.3].map(k => <line key={k} x1={x + k * w} x2={x + k * w} y1={y - 6} y2={y + 6} stroke="#6e4524" strokeWidth="2" />)}
        <circle cx={x + w / 2 - 6} cy={y} r={5.5} fill="#c79060" stroke="#5c3a1f" strokeWidth="1.5" transform={`rotate(${t * 720} ${x + w / 2 - 6} ${y})`} />
        {a.label && <Pill x={x} y={y + 20} text={a.label} tone="gold" />}
      </g>
    )
  }
  if (a.kind === 'barrel') {
    return (
      <g>
        <ellipse cx={x} cy={y + 16} rx={11} ry={4} fill="rgba(0,0,0,0.5)" />
        <g transform={`translate(${x} ${y - 6}) rotate(${t * 400})`}><BarrelIcon size={22} /></g>
        {a.label && <Pill x={x} y={y - 30} text={a.label} tone="red" />}
      </g>
    )
  }
  const enemy = a.kind === 'enemy'
  const ring = enemy ? '#ef4454' : a.kind === 'building' ? '#35d08a' : '#3d8bff'
  const bob = a.kind === 'building' ? 0 : Math.sin(t * 9 + x) * 1.2
  const box = a.kind === 'building'
  return (
    <g transform={`translate(0 ${bob})`}>
      <ellipse cx={x} cy={y + r * 0.95} rx={r * 0.9} ry={r * 0.32} fill="rgba(0,0,0,0.4)" />
      <g transform={`translate(${x} ${y})`}>
        {box
          ? <rect x={-r - 3} y={-r - 3} width={r * 2 + 6} height={r * 2 + 6} rx="7" fill={ring} stroke="#0d1430" strokeWidth="1.2" />
          : <circle r={r + 3} fill={ring} stroke="#0d1430" strokeWidth="1.2" />}
        {box
          ? <rect x={-r} y={-r} width={r * 2} height={r * 2} rx="5.5" fill="#16204a" />
          : <circle r={r} fill="#16204a" />}
        {a.card
          ? <UnitArt card={a.card} r={r} art={art} square={box} g={g} />
          : <text y={4} textAnchor="middle" fontSize="11" fontWeight="900" fill="#fff">{a.label.slice(0, 2)}</text>}
      </g>
      {a.label && <Pill x={x} y={y + r + 12} text={a.label} tone={enemy ? 'red' : 'blue'} />}
    </g>
  )
}
