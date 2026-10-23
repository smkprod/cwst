import { useEffect, useRef, useState } from 'react'

/**
 * Схема арены с анимацией: куда и когда ставить карту.
 *
 * Своя графика, без артов игры: клетки, река, мосты, башни - геометрия; юниты -
 * кружки с подписью (свои синие, чужие красные), здания - квадраты, заклинания -
 * круги зоны. Арена 18 клеток в ширину; показываем свою половину с рекой и краем
 * чужой - там и решается защита.
 *
 * Сцена - таймлайн: у каждого актёра ключевые точки (секунда, клетка), позиция между
 * ними - линейно. Башни «стреляют» по чужим юнитам в радиусе. Сцена крутится по кругу.
 */

export type ActorKind = 'enemy' | 'mine' | 'building' | 'spell' | 'barrel'

export interface Key { t: number; x: number; y: number }

export interface Actor {
  id: string
  kind: ActorKind
  label: string
  keys: Key[]
  /** Появляется / исчезает (секунды). По умолчанию - первая и последняя точка. */
  from?: number
  to?: number
  /** Радиус в клетках (заклинание, крупный юнит). */
  r?: number
}

/** Подсветка клетки «сюда» с подписью. */
export interface Mark { x: number; y: number; from: number; to: number; label?: string }

export interface Scene { duration: number; actors: Actor[]; marks?: Mark[]; noShots?: boolean }

const TILE = 20
const VIEW_TOP = 11
const VIEW_ROWS = 21
const W = 18 * TILE
const H = VIEW_ROWS * TILE
const RIVER_TOP = 15
const RIVER_BOTTOM = 17

const TOWERS = [
  { id: 'pl', x: 3.5, y: 25.5, size: 3, range: 7.5 },
  { id: 'pr', x: 14.5, y: 25.5, size: 3, range: 7.5 },
  { id: 'k', x: 9, y: 29.5, size: 4, range: 7 },
]

const px = (x: number) => x * TILE
const py = (y: number) => (y - VIEW_TOP) * TILE

function at(a: Actor, t: number): { x: number; y: number } | null {
  const from = a.from ?? a.keys[0].t
  const to = a.to ?? a.keys[a.keys.length - 1].t
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

export function ArenaAnim({ scene, paused = false }: { scene: Scene; paused?: boolean }) {
  const [t, setT] = useState(0)
  const startRef = useRef<number | null>(null)

  useEffect(() => {
    if (paused) return
    let raf = 0
    startRef.current = null
    const tick = (now: number) => {
      if (startRef.current === null) startRef.current = now
      setT(((now - startRef.current) / 1000) % scene.duration)
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [scene, paused])

  const live = scene.actors.map(a => ({ a, p: at(a, t) })).filter(x => x.p) as { a: Actor; p: { x: number; y: number } }[]
  const enemies = live.filter(x => x.a.kind === 'enemy')
  const shots: { from: { x: number; y: number }; to: { x: number; y: number }; key: string }[] = []
  if (!scene.noShots) {
    for (const tw of TOWERS) {
      const target = enemies
        .map(e => ({ e, d: Math.hypot(e.p.x - tw.x, e.p.y - tw.y) }))
        .filter(x => x.d <= tw.range)
        .sort((a, b) => a.d - b.d)[0]
      if (target && Math.floor(t * 6) % 2 === 0) shots.push({ from: tw, to: target.e.p, key: tw.id })
    }
  }

  return (
    <svg className="arena" viewBox={`0 0 ${W} ${H}`} role="img" aria-label="arena">
      <defs>
        <pattern id="ar-grid" width={TILE} height={TILE} patternUnits="userSpaceOnUse">
          <rect width={TILE} height={TILE} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="1" />
        </pattern>
        <linearGradient id="ar-ground" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#3b2f52" />
          <stop offset="1" stopColor="#2a2340" />
        </linearGradient>
        <linearGradient id="ar-river" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#2f7fd8" />
          <stop offset="1" stopColor="#47a6ff" />
        </linearGradient>
        <radialGradient id="ar-glow">
          <stop offset="0" stopColor="#ffe08a" stopOpacity="0.9" />
          <stop offset="1" stopColor="#ffe08a" stopOpacity="0" />
        </radialGradient>
      </defs>

      <rect width={W} height={H} fill="url(#ar-ground)" />
      {/* Чужая сторона чуть темнее - чтобы читалось, где чья половина */}
      <rect width={W} height={py(RIVER_TOP)} fill="rgba(229,57,53,0.07)" />
      <rect width={W} height={H} fill="url(#ar-grid)" />
      <rect y={py(RIVER_TOP)} width={W} height={(RIVER_BOTTOM - RIVER_TOP) * TILE} fill="url(#ar-river)" opacity="0.85" />
      {[2, 13].map(bx => (
        <rect key={bx} x={px(bx)} y={py(RIVER_TOP) - 3} width={3 * TILE} height={(RIVER_BOTTOM - RIVER_TOP) * TILE + 6}
          rx="3" fill="#a07a4f" stroke="#c9a06c" strokeWidth="2" />
      ))}

      {TOWERS.map(tw => (
        <g key={tw.id}>
          <circle cx={px(tw.x)} cy={py(tw.y)} r={tw.range * TILE} fill="rgba(90,169,255,0.05)" stroke="rgba(90,169,255,0.18)" strokeDasharray="4 6" />
          <rect x={px(tw.x - tw.size / 2)} y={py(tw.y - tw.size / 2)} width={tw.size * TILE} height={tw.size * TILE}
            rx="6" fill={tw.id === 'k' ? '#3d6fd6' : '#4c86e8'} stroke="#cfe3ff" strokeWidth="2" />
          <text x={px(tw.x)} y={py(tw.y) + 5} textAnchor="middle" fontSize="13" fontWeight="800" fill="#fff">{tw.id === 'k' ? '♛' : '♜'}</text>
        </g>
      ))}

      {(scene.marks ?? []).filter(m => t >= m.from && t <= m.to).map((m, i) => {
        const pulse = 0.5 + 0.5 * Math.sin(t * 8)
        return (
          <g key={i}>
            <rect x={px(m.x - 0.5) - 2} y={py(m.y - 0.5) - 2} width={TILE + 4} height={TILE + 4} rx="4"
              fill={`rgba(255,224,138,${0.25 + 0.35 * pulse})`} stroke="#ffe08a" strokeWidth="2" />
            {m.label && (
              <text x={px(m.x)} y={py(m.y) - 16} textAnchor="middle" fontSize="12" fontWeight="800" fill="#ffe08a">{m.label}</text>
            )}
          </g>
        )
      })}

      {shots.map(s => (
        <line key={s.key} x1={px(s.from.x)} y1={py(s.from.y)} x2={px(s.to.x)} y2={py(s.to.y)}
          stroke="#ffd35a" strokeWidth="2" strokeDasharray="5 5" opacity="0.85" />
      ))}

      {live.map(({ a, p }) => <ActorShape key={a.id} a={a} x={px(p.x)} y={py(p.y)} t={t} />)}
    </svg>
  )
}

function ActorShape({ a, x, y, t }: { a: Actor; x: number; y: number; t: number }) {
  const r = (a.r ?? 0.75) * TILE
  if (a.kind === 'spell') {
    return (
      <g>
        <circle cx={x} cy={y} r={r} fill="rgba(255,211,90,0.28)" stroke="#ffd35a" strokeWidth="2" />
        <text x={x} y={y + 4} textAnchor="middle" fontSize="11" fontWeight="800" fill="#fff">{a.label}</text>
      </g>
    )
  }
  if (a.kind === 'barrel') {
    // Тень на земле и бочка над ней - видно, куда она упадёт
    return (
      <g>
        <ellipse cx={x} cy={y + 14} rx={10} ry={4} fill="rgba(0,0,0,0.45)" />
        <circle cx={x} cy={y - 6} r={9} fill="#e5533a" stroke="#fff" strokeWidth="2" />
        <text x={x} y={y - 24} textAnchor="middle" fontSize="11" fontWeight="800" fill="#ffb0b0">{a.label}</text>
      </g>
    )
  }
  if (a.kind === 'building') {
    return (
      <g>
        <rect x={x - r} y={y - r} width={r * 2} height={r * 2} rx="5" fill="#2e9c57" stroke="#bff0cf" strokeWidth="2" />
        <text x={x} y={y + r + 13} textAnchor="middle" fontSize="11" fontWeight="800" fill="#bff0cf">{a.label}</text>
      </g>
    )
  }
  const enemy = a.kind === 'enemy'
  const bob = Math.sin(t * 10 + x) * 1.2
  return (
    <g transform={`translate(0 ${bob})`}>
      <circle cx={x} cy={y} r={r} fill={enemy ? '#e0283a' : '#2f8cff'} stroke="#fff" strokeWidth="2" />
      <text x={x} y={y + r + 13} textAnchor="middle" fontSize="11" fontWeight="800" fill={enemy ? '#ffb0b0' : '#cfe3ff'}>{a.label}</text>
    </g>
  )
}
