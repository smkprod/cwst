import { useEffect, useId, useRef, useState } from 'react'
import { emojiOf } from '../lib/sim/emoji'

/**
 * Схема арены с анимацией: куда и когда ставить карту.
 *
 * Геометрия - как в игре: поле 18×32 клетки целиком, по 15 рядов на сторону и река
 * в 2 ряда посередине; башни принцесс 3×3 с центром на 3,5 и 14,5 в 6,5 ряда от
 * своего края, король 4×4 по центру в 3 рядах от края, за ним 1 свободный ряд.
 * Радиус принцесс 7,5 клетки, короля 7. Как в игре, поле видно под углом: клетка
 * сплюснута по высоте (TILE_H), поэтому радиусы рисуются эллипсами.
 *
 * Юниты - жетоны со смайликом карты и кольцом стороны (свои синие, чужие красные),
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

      {TOWERS.map(tw => <TowerArt key={tw.id} x={tw.x} y={tw.y} size={tw.size} king={tw.king} enemy={tw.enemy} />)}

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
        : <Token key={a.id} a={a} x={px(p.x)} y={py(p.y)} t={t} g={g} />)}

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
      {/* Трава в шахматку, как в игре: клетка - TILE × TILE_H */}
      <pattern id={g('tiles')} width={TILE * 2} height={TILE_H * 2} patternUnits="userSpaceOnUse">
        <rect width={TILE * 2} height={TILE_H * 2} fill="#9cbf48" />
        <rect width={TILE} height={TILE_H} fill="#a8c952" />
        <rect x={TILE} y={TILE_H} width={TILE} height={TILE_H} fill="#a8c952" />
      </pattern>
      <linearGradient id={g('river')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#0d93c9" />
        <stop offset="0.5" stopColor="#2cc2f0" />
        <stop offset="1" stopColor="#0d93c9" />
      </linearGradient>
      <linearGradient id={g('shade')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#000" stopOpacity="0.18" />
        <stop offset="0.12" stopColor="#000" stopOpacity="0" />
        <stop offset="0.88" stopColor="#000" stopOpacity="0" />
        <stop offset="1" stopColor="#000" stopOpacity="0.18" />
      </linearGradient>
      <radialGradient id={g('spell')}>
        <stop offset="0" stopColor="#ffe08a" stopOpacity="0.55" />
        <stop offset="1" stopColor="#ffb347" stopOpacity="0.12" />
      </radialGradient>
      <filter id={g('glow')} x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="2.2" result="b" />
        <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
      </filter>
    </defs>
  )
}

const LANES = [3.5, 14.5]
const PATH = '#d9b06b'
const PATH_EDGE = '#b98c4f'

/**
 * Поле как в игре: трава в шахматку, дорожки от мостов к башням и петля за ними,
 * площадки под башнями, река с земляными берегами и камнями, деревянные мосты,
 * заборчики в углах за королями.
 */
export function ArenaGround({ t, g }: { t: number; g: (n: string) => string }) {
  // Дорожка - полоса в 1 клетку: вдоль линий от петли за королём до моста и поперёк за королями
  const paths: { x: number; y: number; w: number; h: number }[] = []
  for (const lx of LANES) {
    paths.push({ x: lx - 0.5, y: 3, w: 1, h: RIVER_TOP - 3 })
    paths.push({ x: lx - 0.5, y: RIVER_BOTTOM, w: 1, h: 29 - RIVER_BOTTOM })
  }
  paths.push({ x: 3, y: 3, w: 12, h: 1 }, { x: 3, y: 28, w: 12, h: 1 })
  const pads = TOWERS.map(tw => ({ x: tw.x - tw.size / 2, y: tw.y - tw.size / 2, s: tw.size }))
  return (
    <>
      <rect width={W} height={H} fill={`url(#${g('tiles')})`} />
      <rect width={W} height={H} fill={`url(#${g('shade')})`} />

      {paths.map((r, i) => (
        <rect key={`p${i}`} x={px(r.x)} y={py(r.y)} width={r.w * TILE} height={r.h * TILE_H} fill={PATH} />
      ))}
      {/* Кромка дорожек - чуть темнее, чтобы читались на траве */}
      {paths.map((r, i) => (
        <rect key={`pe${i}`} x={px(r.x) + 0.5} y={py(r.y) + 0.5} width={r.w * TILE - 1} height={r.h * TILE_H - 1}
          fill="none" stroke={PATH_EDGE} strokeOpacity="0.45" />
      ))}
      {/* Площадки под башнями - их клетки */}
      {pads.map((p, i) => (
        <rect key={`pad${i}`} x={px(p.x)} y={py(p.y)} width={p.s * TILE} height={p.s * TILE_H} rx="3"
          fill="#cfa564" stroke={PATH_EDGE} strokeOpacity="0.6" />
      ))}

      {/* Река: берега из земли, вода по центру, камни у кромки */}
      <rect y={py(RIVER_TOP)} width={W} height={(RIVER_BOTTOM - RIVER_TOP) * TILE_H} fill="#a9825c" />
      <rect y={py(RIVER_TOP + 0.3)} width={W} height={(RIVER_BOTTOM - RIVER_TOP - 0.6) * TILE_H} fill={`url(#${g('river')})`} />
      {[0, 1].map(i => (
        <path key={i} d={wave(py(RIVER_TOP + 0.75 + i * 0.5), t * 30 + i * 40)} fill="none" stroke="rgba(255,255,255,0.5)" strokeWidth="1.3" />
      ))}
      {[1.2, 7.3, 10.4, 16.9].map((sx, i) => (
        <ellipse key={`st${i}`} cx={px(sx)} cy={py(i % 2 ? RIVER_TOP + 0.22 : RIVER_BOTTOM - 0.22)} rx={9} ry={3.6}
          fill="#8d97a3" stroke="#5e6670" strokeWidth="1" />
      ))}

      {/* Мосты: доски вдоль хода, в 2 клетки шириной над каждой линией */}
      {LANES.map(lx => (
        <g key={lx}>
          <rect x={px(lx - 1)} y={py(RIVER_TOP - 0.45)} width={2 * TILE} height={(RIVER_BOTTOM - RIVER_TOP + 0.9) * TILE_H} rx="3"
            fill="#cf8f6c" stroke="#7a4a32" strokeWidth="1.6" />
          {[-0.5, 0, 0.5].map(k => (
            <line key={k} x1={px(lx + k)} x2={px(lx + k)} y1={py(RIVER_TOP - 0.4)} y2={py(RIVER_BOTTOM + 0.4)} stroke="#a8654a" strokeWidth="1.4" />
          ))}
          <rect x={px(lx - 1)} y={py(RIVER_TOP - 0.45)} width={2 * TILE} height={3} fill="#e3a684" />
        </g>
      ))}

      {/* Заборчики в углах за королями */}
      {[0.35, 31.65].map(fy => [1.5, 12.5].map(fx => <Fence key={`${fx}-${fy}`} x={fx} y={fy} />))}
    </>
  )
}

function Fence({ x, y }: { x: number; y: number }) {
  const x0 = px(x), x1 = px(x + 4), cy = py(y)
  return (
    <g opacity="0.9">
      <rect x={x0} y={cy - 3} width={x1 - x0} height={2.2} rx="1" fill="#8a5a33" />
      <rect x={x0} y={cy + 1} width={x1 - x0} height={2.2} rx="1" fill="#8a5a33" />
      {[x0, (x0 + x1) / 2, x1 - 4].map(fx => <rect key={fx} x={fx} y={cy - 5} width={4} height={10} rx="1" fill="#6e4524" />)}
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

/** Ширина картинки башни в клетках: как в игре, башня чуть уже своих клеток. */
const ART: Record<'king' | 'princess', number> = { king: 3.4, princess: 2.8 }

/** Верх картинки башни в пикселях - над ним рисуется полоска здоровья. */
export function towerTop(y: number, size: number, king: boolean): number {
  return py(y + size / 2) + 2 - ART[king ? 'king' : 'princess'] * TILE
}

export function TowerArt({ x, y, size, king, enemy = false, dead = false }: {
  x: number; y: number; size: number; king: boolean; enemy?: boolean; dead?: boolean
}) {
  // Картинка башни сбоку: низ - по нижнему краю её клеток, ширина - как у башни в игре
  const w = ART[king ? 'king' : 'princess'] * TILE
  const h = w
  const bottom = py(y + size / 2) + 2
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
  const art = `/arena/${king ? 'king' : 'princess'}${enemy ? '-red' : ''}.webp`
  return (
    <g>
      <ellipse cx={px(x)} cy={bottom - 3} rx={w * 0.46} ry={5} fill="rgba(0,0,0,0.3)" />
      <image href={art} x={px(x) - w / 2} y={bottom - h} width={w} height={h} />
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

function Token({ a, x, y, t, g }: { a: Actor; x: number; y: number; t: number; g: (n: string) => string }) {
  const r = (a.r ?? 0.7) * TILE
  const emoji = a.card ? emojiOf(a.card) : ''
  if (a.kind === 'spell') {
    return (
      <g>
        <ellipse cx={x} cy={y} rx={r} ry={r * (TILE_H / TILE)} fill={`url(#${g('spell')})`} stroke="#ffd35a" strokeWidth="2" />
        {emoji && <text x={x} y={y} textAnchor="middle" dominantBaseline="central" fontSize={Math.min(26, r)}>{emoji}</text>}
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
        <g transform={`rotate(${t * 400} ${x} ${y - 6})`}>
          <text x={x} y={y - 6} textAnchor="middle" dominantBaseline="central" fontSize="22">{emoji || '🛢️'}</text>
        </g>
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
      {box
        ? <rect x={x - r - 2} y={y - r - 2} width={r * 2 + 4} height={r * 2 + 4} rx="7" fill={ring} filter={`url(#${g('glow')})`} />
        : <circle cx={x} cy={y} r={r + 2.5} fill={ring} filter={`url(#${g('glow')})`} />}
      {box
        ? <rect x={x - r} y={y - r} width={r * 2} height={r * 2} rx="6" fill="#16204a" />
        : <circle cx={x} cy={y} r={r} fill="#16204a" />}
      {emoji
        ? <text x={x} y={y + 1} textAnchor="middle" dominantBaseline="central" fontSize={r * 1.3}>{emoji}</text>
        : <text x={x} y={y + 4} textAnchor="middle" fontSize="11" fontWeight="900" fill="#fff">{a.label.slice(0, 2)}</text>}
      {a.label && <Pill x={x} y={y + r + 11} text={a.label} tone={enemy ? 'red' : 'blue'} />}
    </g>
  )
}
