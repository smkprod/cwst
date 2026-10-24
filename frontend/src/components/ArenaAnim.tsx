import { useEffect, useId, useRef, useState } from 'react'
import { emojiOf } from '../lib/sim/emoji'

/**
 * Схема арены с анимацией: куда и когда ставить карту.
 *
 * Геометрия - как в игре: поле 18×32 клетки, река на рядах 15-16 (считая сверху),
 * мосты по 3 клетки над линиями, башни принцесс 3×3 с центром на 3,5 и 14,5 и в
 * 6,5 клетки от своего края, король 4×4 по центру в 3 клетках от края. Радиус
 * принцесс 7,5 клетки, короля 7. Показываем свою половину, реку и край чужой.
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

export const TILE = 20
export const VIEW_TOP = 11
const VIEW_ROWS = 21
export const W = 18 * TILE
export const H = VIEW_ROWS * TILE
const RIVER_TOP = 15
const RIVER_BOTTOM = 17

export const TOWERS = [
  { id: 'pl', x: 3.5, y: 25.5, size: 3, range: 7.5, king: false },
  { id: 'pr', x: 14.5, y: 25.5, size: 3, range: 7.5, king: false },
  { id: 'k', x: 9, y: 29, size: 4, range: 7, king: true },
]

export const px = (x: number) => x * TILE
export const py = (y: number) => (y - VIEW_TOP) * TILE

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

/** Жетон-«призрак» там, куда игрок собирается поставить карту (тренажёр). */
export interface Ghost { x: number; y: number; card: string; kind: ActorKind; r?: number; ok?: boolean }

/**
 * @param at Показать один кадр на этой секунде (без анимации).
 * @param loop false - сыграть сцену один раз и встать на последнем кадре, позвав onEnd.
 * @param onTap Тап по полю - в координатах клеток (тренажёр).
 */
export function ArenaAnim({ scene, paused = false, at: frameAt, loop = true, onEnd, onTap, ghost }: {
  scene: Scene
  paused?: boolean
  at?: number
  loop?: boolean
  onEnd?: () => void
  onTap?: (x: number, y: number) => void
  ghost?: Ghost | null
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

  const tap = (e: React.PointerEvent<SVGSVGElement>) => {
    if (!onTap) return
    const rect = e.currentTarget.getBoundingClientRect()
    onTap(((e.clientX - rect.left) / rect.width) * (W / TILE), ((e.clientY - rect.top) / rect.height) * (H / TILE) + VIEW_TOP)
  }

  const live = scene.actors.map(a => ({ a, p: at(a, t) })).filter(x => x.p) as { a: Actor; p: { x: number; y: number } }[]
  const enemies = live.filter(x => x.a.kind === 'enemy')

  // Снаряды: башня бьёт ближайшего чужого в радиусе (до края жетона)
  const shots: { from: { x: number; y: number }; to: { x: number; y: number }; key: string }[] = []
  if (!scene.noShots) {
    for (const tw of TOWERS) {
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
    <svg className={`arena ${onTap ? 'arena-tap' : ''}`} viewBox={`0 0 ${W} ${H}`} role="img" aria-label="arena" onPointerDown={tap}>
      <ArenaDefs g={g} />
      <ArenaGround t={t} g={g} />

      {/* Радиусы башен - едва заметно, ярче у той, что стреляет */}
      {TOWERS.map(tw => (
        <circle key={`r${tw.id}`} cx={px(tw.x)} cy={py(tw.y)} r={tw.range * TILE} fill="none"
          stroke={shots.some(s => s.key === tw.id) ? 'rgba(255,211,90,0.45)' : 'rgba(150,190,255,0.14)'} strokeDasharray="3 7" />
      ))}

      {TOWERS.map(tw => <TowerArt key={tw.id} x={tw.x} y={tw.y} size={tw.size} king={tw.king} />)}

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
        const size = (m.size ?? 1) * TILE
        return (
          <g key={`m${i}`}>
            <rect x={px(m.x) - size / 2 - 3} y={py(m.y) - size / 2 - 3} width={size + 6} height={size + 6} rx="5"
              fill={`rgba(255,224,138,${0.18 + 0.3 * pulse})`} stroke="#ffe08a" strokeWidth="2.5" />
            {m.label && <Pill x={px(m.x)} y={py(m.y) - size / 2 - 14} text={m.label} tone="gold" />}
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

      {ghost && (
        <g opacity="0.75">
          <rect x={px(Math.floor(ghost.x))} y={py(Math.floor(ghost.y))} width={TILE} height={TILE}
            fill="none" stroke={ghost.ok === false ? '#ef4454' : '#ffe08a'} strokeWidth="2" strokeDasharray="3 3" />
          <Token a={{ id: 'ghost', kind: ghost.kind, label: '', card: ghost.card, r: ghost.r, keys: [{ t: 0, x: ghost.x, y: ghost.y }] }}
            x={px(ghost.x)} y={py(ghost.y)} t={t} g={g} />
        </g>
      )}

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
      <pattern id={g('tiles')} width={TILE * 2} height={TILE * 2} patternUnits="userSpaceOnUse">
        <rect width={TILE * 2} height={TILE * 2} fill="#2b3a6e" />
        <rect width={TILE} height={TILE} fill="#2f4078" />
        <rect x={TILE} y={TILE} width={TILE} height={TILE} fill="#2f4078" />
      </pattern>
      <pattern id={g('tilesE')} width={TILE * 2} height={TILE * 2} patternUnits="userSpaceOnUse">
        <rect width={TILE * 2} height={TILE * 2} fill="#3b2c5e" />
        <rect width={TILE} height={TILE} fill="#423266" />
        <rect x={TILE} y={TILE} width={TILE} height={TILE} fill="#423266" />
      </pattern>
      <linearGradient id={g('river')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#1b8fe0" />
        <stop offset="0.5" stopColor="#3fc1ff" />
        <stop offset="1" stopColor="#1b8fe0" />
      </linearGradient>
      <linearGradient id={g('stone')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#c9d3ea" />
        <stop offset="1" stopColor="#7d8bb0" />
      </linearGradient>
      <linearGradient id={g('roof')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#5aa9ff" />
        <stop offset="1" stopColor="#2c5fd6" />
      </linearGradient>
      <linearGradient id={g('gold')} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#ffe9a8" />
        <stop offset="1" stopColor="#f2a922" />
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

/** Поле: клетки, дорожки, река с волнами и мосты. */
export function ArenaGround({ t, g }: { t: number; g: (n: string) => string }) {
  return (
    <>
      {/* Поле: клетки как в игре, чужая половина в тёплом тоне */}
      <rect width={W} height={H} fill={`url(#${g('tiles')})`} />
      <rect width={W} height={py(RIVER_TOP)} fill={`url(#${g('tilesE')})`} />
      {/* Дорожки по линиям - туда идёт почти всё */}
      {[3.5, 14.5].map(lx => (
        <rect key={lx} x={px(lx - 0.5)} y={0} width={TILE} height={H} fill="rgba(255,255,255,0.035)" />
      ))}

      {/* Река с волнами */}
      <rect y={py(RIVER_TOP)} width={W} height={(RIVER_BOTTOM - RIVER_TOP) * TILE} fill={`url(#${g('river')})`} />
      {[0, 1].map(i => (
        <path key={i} d={wave(py(RIVER_TOP) + 12 + i * 16, t * 30 + i * 40)} fill="none" stroke="rgba(255,255,255,0.45)" strokeWidth="1.5" />
      ))}
      {/* Мосты: доски поперёк реки */}
      {[2, 13].map(bx => (
        <g key={bx}>
          <rect x={px(bx)} y={py(RIVER_TOP) - 4} width={3 * TILE} height={(RIVER_BOTTOM - RIVER_TOP) * TILE + 8} rx="4" fill="#8a5a33" stroke="#5c3a1f" strokeWidth="2" />
          {Array.from({ length: 6 }, (_, i) => (
            <line key={i} x1={px(bx) + 3} x2={px(bx + 3) - 3} y1={py(RIVER_TOP) + 2 + i * 7} y2={py(RIVER_TOP) + 2 + i * 7} stroke="#b07a48" strokeWidth="2" />
          ))}
        </g>
      ))}
    </>
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

/** Верх картинки башни в пикселях - над ним рисуется полоска здоровья. */
export function towerTop(y: number, size: number, king: boolean): number {
  return py(y + size / 2) + 4 - size * TILE * (king ? 1.35 : 1.4)
}

export function TowerArt({ x, y, size, king, dead = false }: { x: number; y: number; size: number; king: boolean; dead?: boolean }) {
  // Картинка башни сбоку: низ - по нижнему краю клеток башни, ширина чуть больше
  // клеток - так башня читается объёмной, как в игре, а не плоским квадратом
  const w = size * TILE * (king ? 1.35 : 1.4)
  const h = w
  const bottom = py(y + size / 2) + 4
  if (dead) {
    // Развалины: тёмная груда камней на месте башни
    return (
      <g opacity="0.85">
        <ellipse cx={px(x)} cy={bottom - 8} rx={size * TILE * 0.5} ry={size * TILE * 0.28} fill="#3a3f55" stroke="#22263a" strokeWidth="2" />
        <text x={px(x)} y={bottom - 6} textAnchor="middle" fontSize={size * 7}>🪨</text>
      </g>
    )
  }
  return (
    <g>
      <ellipse cx={px(x)} cy={bottom - 2} rx={size * TILE * 0.55} ry={6} fill="rgba(0,0,0,0.4)" />
      <image href={king ? '/arena/king.webp' : '/arena/princess.webp'} x={px(x) - w / 2} y={bottom - h} width={w} height={h} />
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
        <circle cx={x} cy={y} r={r} fill={`url(#${g('spell')})`} stroke="#ffd35a" strokeWidth="2" />
        {emoji && <text x={x} y={y} textAnchor="middle" dominantBaseline="central" fontSize={Math.min(26, r)}>{emoji}</text>}
        {a.label && <Pill x={x} y={y + r + 10} text={a.label} tone="gold" />}
      </g>
    )
  }
  if (a.kind === 'log') {
    // Бревно - широкое и катится: полоска с кольцами-годами
    const w = (a.r ?? 2) * TILE * 2
    return (
      <g>
        <rect x={x - w / 2} y={y - 9} width={w} height={18} rx="9" fill="#9b6a3a" stroke="#5c3a1f" strokeWidth="2" />
        {[-0.3, 0, 0.3].map(k => <line key={k} x1={x + k * w} x2={x + k * w} y1={y - 8} y2={y + 8} stroke="#6e4524" strokeWidth="2" />)}
        <circle cx={x + w / 2 - 6} cy={y} r={6} fill="#c79060" stroke="#5c3a1f" strokeWidth="1.5" transform={`rotate(${t * 720} ${x + w / 2 - 6} ${y})`} />
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
