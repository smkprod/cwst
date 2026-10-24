import { useEffect, useId, useRef, useState } from 'react'
import { DT, TOWER_SIZE, type Entity, type Projectile, type Sim } from '../lib/sim/engine'
import { SPELLS, UNITS } from '../lib/sim/stats'
import { emojiOf } from '../lib/sim/emoji'
import { ArenaDefs, ArenaGround, H, TILE, TowerArt, VIEW_TOP, W, px, py, towerTop } from './ArenaAnim'

/**
 * Живая арена тренажёра: рисует бой из движка (lib/sim) кадр за кадром. Юниты -
 * смайлики карт в кольце своей стороны, с полоской здоровья, выпадом при ударе,
 * вспышкой от урона, кольцом-таймером появления; летающие - над тенью. Снаряды
 * летят с той скоростью, что в игре; огненный шар и бочка - по дуге.
 */

export interface SimGhost { card: string; x: number; y: number; ok: boolean }

const ME = '#3d8bff'
const FOE = '#ef4454'

export function ArenaSim({ sim, running, speed = 1, onTick, onTap, ghost, zone }: {
  sim: Sim
  running: boolean
  speed?: number
  /** После каждого шага боя; true - встать (пауза или конец). */
  onTick?: (sim: Sim) => boolean
  onTap?: (x: number, y: number) => void
  ghost?: SimGhost | null
  /** Подсветить, куда нельзя: войска - только своя половина, Шахтёр - везде, кроме реки. */
  zone?: 'troop' | 'spell' | 'miner' | null
}) {
  const [clock, setClock] = useState(0)
  const uid = useId().replace(/:/g, '')
  const g = (name: string) => `${name}-${uid}`
  const live = useRef({ running, speed, onTick, halted: false })
  live.current.running = running
  live.current.speed = speed
  live.current.onTick = onTick

  // Новый запуск снимает «встал»: родитель сам решает, когда бой идёт
  useEffect(() => { live.current.halted = false }, [running, sim])

  useEffect(() => {
    let raf = 0
    let last = performance.now()
    let painted = 0
    let acc = 0
    const loop = (now: number) => {
      const dt = Math.min(0.1, (now - last) / 1000)
      last = now
      const l = live.current
      const playing = l.running && !l.halted
      // На паузе бой стоит - хватит и 20 кадров в секунду на волны и пульс «призрака»
      if (!playing && now - painted < 50) { raf = requestAnimationFrame(loop); return }
      painted = now
      if (playing) {
        acc += dt * l.speed
        let steps = 0
        while (acc >= DT && steps < 20) {
          sim.tick()
          acc -= DT
          steps++
          if (l.onTick?.(sim)) { l.halted = true; acc = 0; break }
        }
      } else acc = 0
      // Часы для волн и покачивания; заодно перерисовывают кадр боя
      setClock(now / 1000)
      raf = requestAnimationFrame(loop)
    }
    raf = requestAnimationFrame(loop)
    return () => cancelAnimationFrame(raf)
  }, [sim])

  const tap = (e: React.PointerEvent<SVGSVGElement>) => {
    if (!onTap) return
    const rect = e.currentTarget.getBoundingClientRect()
    onTap(((e.clientX - rect.left) / rect.width) * (W / TILE), ((e.clientY - rect.top) / rect.height) * (H / TILE) + VIEW_TOP)
  }

  const towers = sim.entities.filter(e => e.kind === 'tower' && e.y + TOWER_SIZE[e.slot!] / 2 > VIEW_TOP)
  const units = sim.entities.filter(e => e.alive && e.kind !== 'tower').sort((a, b) => a.y - b.y)
  const ground = units.filter(e => !e.s.flying)
  const air = units.filter(e => e.s.flying)

  return (
    <svg className={`arena ${onTap ? 'arena-tap' : ''}`} viewBox={`0 0 ${W} ${H}`} role="img" aria-label="arena" onPointerDown={tap}>
      <ArenaDefs g={g} />
      <ArenaGround t={clock} g={g} />

      {zone && <Zone kind={zone} />}

      {/* Радиус башни, которая сейчас стреляет */}
      {towers.filter(t => t.alive && t.team === 'me' && t.target && t.active).map(t => (
        <circle key={`r${t.id}`} cx={px(t.x)} cy={py(t.y)} r={t.s.range * TILE + t.s.radius * TILE} fill="none"
          stroke="rgba(255,211,90,0.4)" strokeDasharray="3 7" />
      ))}

      {sim.bombs.map(b => <Bomb key={b.id} b={b} now={sim.t} />)}

      {towers.map(t => <TowerSim key={t.id} e={t} now={sim.t} />)}

      {ground.map(e => <Unit key={e.id} e={e} clock={clock} />)}
      {sim.rollers.map(r => <Log key={r.id} x={px(r.x)} y={py(r.y)} clock={clock} fade={0.4 + Math.abs(r.y0 - r.y) / 0.6} />)}
      {sim.projectiles.filter(p => p.kind !== 'fireball' && p.kind !== 'barrel').map(p => <Shot key={p.id} p={p} />)}
      {air.map(e => <Unit key={e.id} e={e} clock={clock} />)}
      {sim.projectiles.filter(p => p.kind === 'fireball' || p.kind === 'barrel').map(p => <Lob key={p.id} p={p} clock={clock} />)}

      {sim.effects.map(f => {
        const k = Math.min(1, (sim.t - f.t) / f.dur)
        const x = px(f.x), y = py(f.y)
        if (f.kind === 'boom') {
          const r = (f.r ?? 1.5) * TILE * (0.55 + 0.45 * k)
          return (
            <g key={f.id} opacity={1 - k}>
              <circle cx={x} cy={y} r={r} fill="rgba(255,140,40,0.35)" stroke="#ffb347" strokeWidth={3 * (1 - k) + 1} />
              <text x={x} y={y} textAnchor="middle" dominantBaseline="central" fontSize={26 + 14 * k}>💥</text>
            </g>
          )
        }
        if (f.kind === 'slam') {
          return <circle key={f.id} cx={x} cy={y} r={(f.r ?? 2) * TILE * k} fill="rgba(255,230,190,0.15)" stroke="#ffe6be" strokeWidth={4 * (1 - k)} opacity={1 - k} />
        }
        if (f.kind === 'poof') {
          return (
            <g key={f.id} opacity={1 - k}>
              <circle cx={x} cy={y} r={8 + 16 * k} fill="none" stroke="#fff" strokeWidth={3 * (1 - k)} />
              {[0, 1, 2, 3, 4].map(i => {
                const a = i * 1.26
                return <circle key={i} cx={x + Math.cos(a) * 18 * k} cy={y + Math.sin(a) * 18 * k} r={2.5 * (1 - k)} fill="#fff" />
              })}
            </g>
          )
        }
        if (f.kind === 'dmg') {
          return (
            <text key={f.id} x={x} y={y - 18 * k} textAnchor="middle" fontSize="14" fontWeight="900"
              fill={f.team === 'me' ? '#ff6b78' : '#ffe08a'} stroke="#2a0610" strokeWidth="3" paintOrder="stroke" opacity={Math.min(1, 2 * (1 - k))}>{f.text}</text>
          )
        }
        return null
      })}

      {ghost && <GhostMark ghost={ghost} clock={clock} />}
    </svg>
  )
}

/** Красная зона, куда карту не поставить: чужая половина и река, а под башнями - клетки башни. */
function Zone({ kind }: { kind: 'troop' | 'spell' | 'miner' }) {
  if (kind === 'spell') return null
  const boxes = [{ x: 3.5, y: 25.5, s: 3 }, { x: 14.5, y: 25.5, s: 3 }, { x: 9, y: 29, s: 4 }]
  return (
    <g pointerEvents="none">
      {kind === 'troop'
        ? <rect x={0} y={0} width={W} height={py(17)} fill="rgba(239,68,84,0.3)" />
        : <rect x={0} y={py(15)} width={W} height={2 * TILE} fill="rgba(239,68,84,0.3)" />}
      {kind === 'troop' && <line x1={0} x2={W} y1={py(17)} y2={py(17)} stroke="#ff7a85" strokeWidth="2" strokeDasharray="6 5" />}
      {boxes.map((b, i) => (
        <rect key={i} x={px(b.x - b.s / 2)} y={py(b.y - b.s / 2)} width={b.s * TILE} height={b.s * TILE} fill="rgba(239,68,84,0.18)" />
      ))}
    </g>
  )
}

function TowerSim({ e, now }: { e: Entity; now: number }) {
  const size = TOWER_SIZE[e.slot!]
  const king = e.slot === 'K'
  const top = towerTop(e.y, size, king)
  const hurt = e.alive && e.sinceHurt < 0.12
  const w = size * TILE * 0.8
  const color = e.team === 'me' ? ME : FOE
  return (
    <g>
      <g transform={hurt ? `translate(${Math.sin(now * 90) * 1.5} 0)` : undefined}>
        <TowerArt x={e.x} y={e.y} size={size} king={king} dead={!e.alive} />
      </g>
      {e.alive && (
        <g>
          <rect x={px(e.x) - w / 2} y={top - 4} width={w} height={10} rx="5" fill="rgba(10,14,32,0.85)" stroke="rgba(255,255,255,0.35)" strokeWidth="1" />
          <rect x={px(e.x) - w / 2 + 1.5} y={top - 2.5} width={(w - 3) * Math.max(0, e.hp / e.maxHp)} height={7} rx="3.5" fill={color} />
          <text x={px(e.x)} y={top + 4} textAnchor="middle" fontSize="8" fontWeight="900" fill="#fff">{Math.ceil(e.hp)}</text>
        </g>
      )}
      {king && e.alive && !e.active && (
        <text x={px(e.x) + w / 2 + 4} y={top + 2} fontSize="11" opacity={0.6 + 0.4 * Math.sin(now * 3)}>💤</text>
      )}
    </g>
  )
}

/** Размер жетона в пикселях: по радиусу юнита из игры, мелочь в группе чуть меньше. */
function tokenR(e: Entity): number {
  const base = e.s.radius * TILE * 1.2
  return Math.max(9, (e.s.count ?? 1) > 1 || e.card === 'Goblins' ? base * 0.8 : base)
}

function Unit({ e, clock }: { e: Entity; clock: number }) {
  const r = tokenR(e)
  let x = px(e.x), y = py(e.y)
  const color = e.team === 'me' ? ME : FOE
  const box = e.kind === 'building'

  // Шахтёр под землёй: бугорок земли едет к месту выхода
  if (e.tunnel) {
    const k = e.tunnel.t / e.tunnel.dur
    const tx = px(e.tunnel.fx + (e.x - e.tunnel.fx) * k), ty = py(e.tunnel.fy + (e.y - e.tunnel.fy) * k)
    const dx = px(e.x) - px(e.tunnel.fx), dy = py(e.y) - py(e.tunnel.fy)
    const len = Math.hypot(dx, dy) || 1
    return (
      <g>
        {[1, 2, 3].map(i => <circle key={i} cx={tx - (dx / len) * i * 7} cy={ty - (dy / len) * i * 7} r={3.5 - i * 0.8} fill="#6e4524" opacity={0.7 - i * 0.18} />)}
        <ellipse cx={tx} cy={ty} rx={9} ry={5.5} fill="#7a5230" stroke="#4a2e16" strokeWidth="1.5" />
      </g>
    )
  }

  // Выпад к цели при ударе (ближний бой) или отдача (дальний)
  if (e.target && e.sinceHit < 0.2 && !box) {
    const k = 1 - e.sinceHit / 0.2
    const dx = e.target.x - e.x, dy = e.target.y - e.y
    const len = Math.hypot(dx, dy) || 1
    const lunge = e.s.projSpeed > 0 ? -2 * k : 5 * k
    x += (dx / len) * lunge
    y += (dy / len) * lunge
  }

  // Прыжок Мега-рыцаря: дуга вверх и тень на земле
  let lift = 0
  let scale = 1
  if (e.dash) {
    const k = Math.min(1, e.dash.t / e.dash.dur)
    lift = Math.sin(Math.PI * k) * 30
    scale = 1 + Math.sin(Math.PI * k) * 0.2
  }
  if (e.s.flying) lift = 16 + Math.sin(clock * 3 + e.id) * 2
  const bob = e.moving && !e.s.flying && !e.dash ? Math.abs(Math.sin(clock * 11 + e.id)) * -2.2 : 0

  const deploying = e.deploy > 0
  const pop = deploying ? 0.7 + 0.3 * Math.min(1, (1 - e.deploy / (e.deploy0 || 1)) * 4) : 1
  const s = scale * pop
  const cy = y - lift + bob
  const hurt = e.sinceHurt < 0.14 ? 1 - e.sinceHurt / 0.14 : 0

  return (
    <g opacity={deploying ? 0.72 : 1}>
      <ellipse cx={x} cy={y + r * 0.85} rx={r * (e.s.flying ? 0.7 : 0.95)} ry={r * 0.3} fill="rgba(0,0,0,0.38)" />
      <g transform={`translate(${x} ${cy}) scale(${s})`}>
        {box
          ? <rect x={-r - 2.5} y={-r - 2.5} width={2 * r + 5} height={2 * r + 5} rx="7" fill={color} />
          : <circle r={r + 2.5} fill={color} />}
        {box
          ? <rect x={-r} y={-r} width={2 * r} height={2 * r} rx="5.5" fill="#16204a" />
          : <circle r={r} fill="#16204a" />}
        <text y={1} textAnchor="middle" dominantBaseline="central" fontSize={r * 1.35}>{emojiOf(e.card)}</text>
        {hurt > 0 && (box
          ? <rect x={-r} y={-r} width={2 * r} height={2 * r} rx="5.5" fill="#fff" opacity={0.55 * hurt} />
          : <circle r={r} fill="#fff" opacity={0.55 * hurt} />)}
        {deploying && (
          <circle r={r + 5} fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round"
            strokeDasharray={`${2 * Math.PI * (r + 5) * (1 - e.deploy / (e.deploy0 || 1))} 999`} transform="rotate(-90)" />
        )}
      </g>
      {!deploying && e.hp < e.maxHp && (
        <g>
          <rect x={x - r} y={cy - r * s - 9} width={2 * r} height={5} rx="2.5" fill="rgba(10,14,32,0.85)" />
          <rect x={x - r + 1} y={cy - r * s - 8} width={(2 * r - 2) * Math.max(0, e.hp / e.maxHp)} height={3} rx="1.5" fill={color} />
        </g>
      )}
    </g>
  )
}

function dir(p: Projectile) {
  const dx = p.tx - p.x, dy = p.ty - p.y
  const len = Math.hypot(dx, dy) || 1
  return { x: dx / len, y: dy / len }
}

/** Снаряды по прямой: стрела башни, пуля мушкетёра, ядро пушки, болт Икс-лука. */
function Shot({ p }: { p: Projectile }) {
  const x = px(p.x), y = py(p.y)
  const d = dir(p)
  if (p.kind === 'arrow' || p.kind === 'bolt') {
    const len = p.kind === 'bolt' ? 11 : 9
    return (
      <g>
        <line x1={x - d.x * len} y1={y - d.y * len} x2={x} y2={y} stroke={p.kind === 'bolt' ? '#e8f1ff' : '#ffe9a8'} strokeWidth="2.4" strokeLinecap="round" />
        <circle cx={x} cy={y} r="2" fill="#fff" />
      </g>
    )
  }
  if (p.kind === 'cannonball' || p.kind === 'king') {
    return <circle cx={x} cy={y} r={p.kind === 'king' ? 4.2 : 4} fill={p.kind === 'king' ? '#ffd35a' : '#2a2a33'} stroke={p.kind === 'king' ? '#a8740f' : '#000'} strokeWidth="1.5" />
  }
  return (
    <g>
      <line x1={x - d.x * 7} y1={y - d.y * 7} x2={x} y2={y} stroke="rgba(255,246,194,0.6)" strokeWidth="2" />
      <circle cx={x} cy={y} r="2.6" fill="#fff6c2" />
    </g>
  )
}

/** Огненный шар и бочка летят по дуге: высота - от доли пройденного пути, тень - на земле. */
function Lob({ p, clock }: { p: Projectile; clock: number }) {
  const all = Math.hypot(p.tx - p.sx, p.ty - p.sy) || 1
  const k = Math.min(1, 1 - Math.hypot(p.tx - p.x, p.ty - p.y) / all)
  const x = px(p.x), y = py(p.y)
  const h = Math.sin(Math.PI * k) * (p.kind === 'barrel' ? 70 : 50)
  const d = dir(p)
  const fire = p.kind === 'fireball'
  const angle = (Math.atan2(d.y, d.x) * 180) / Math.PI
  return (
    <g>
      <ellipse cx={x} cy={y} rx={9 - h / 14} ry={3.5 - h / 40} fill="rgba(0,0,0,0.35)" />
      {fire && <circle cx={x} cy={y - h} r={12} fill="rgba(255,140,40,0.35)" />}
      <text x={x} y={y - h} textAnchor="middle" dominantBaseline="central" fontSize={fire ? 24 : 22}
        transform={fire ? `rotate(${angle - 45} ${x} ${y - h})` : `rotate(${clock * 400} ${x} ${y - h})`}>{fire ? '☄️' : '🛢️'}</text>
      {/* Куда упадёт - чтобы видеть угрозу заранее */}
      <circle cx={px(p.tx)} cy={py(p.ty)} r={(fire ? SPELLS.Fireball.radius : SPELLS['Goblin Barrel'].radius) * TILE} fill="none"
        stroke={p.team === 'me' ? 'rgba(127,181,255,0.6)' : 'rgba(255,122,133,0.7)'} strokeWidth="1.5" strokeDasharray="4 4" />
    </g>
  )
}

function Log({ x, y, clock, fade }: { x: number; y: number; clock: number; fade: number }) {
  const w = SPELLS['The Log'].width * 2 * TILE
  return (
    <g opacity={Math.min(1, fade)}>
      <ellipse cx={x} cy={y + 10} rx={w / 2} ry={4} fill="rgba(0,0,0,0.35)" />
      <rect x={x - w / 2} y={y - 9} width={w} height={18} rx="9" fill="#9b6a3a" stroke="#5c3a1f" strokeWidth="2" />
      {[-0.3, -0.1, 0.1, 0.3].map(k => (
        <line key={k} x1={x + k * w} x2={x + k * w} y1={y - 8} y2={y + 8} stroke="#6e4524" strokeWidth="2"
          transform={`translate(0 ${Math.sin(clock * 20 + k * 9) * 1.5})`} />
      ))}
      <circle cx={x + w / 2 - 7} cy={y} r={7} fill="#c79060" stroke="#5c3a1f" strokeWidth="1.5" />
      <circle cx={x + w / 2 - 7} cy={y} r={3} fill="none" stroke="#8a5a33" strokeWidth="1.2" />
      <circle cx={x - w / 2 + 7} cy={y} r={7} fill="#c79060" stroke="#5c3a1f" strokeWidth="1.5" />
    </g>
  )
}

/** Бомба Шара: падает и лежит с кругом урона, пока не взорвётся. */
function Bomb({ b, now }: { b: Sim['bombs'][number]; now: number }) {
  const k = Math.min(1, (now - b.t) / 0.5)
  const x = px(b.x), y = py(b.y)
  const blink = 0.35 + 0.35 * Math.abs(Math.sin(now * (4 + 10 * (1 - (b.at - now) / (b.at - b.t)))))
  return (
    <g>
      <circle cx={x} cy={y} r={b.r * TILE} fill={`rgba(239,68,84,${blink * 0.25})`} stroke="#ff7a85" strokeWidth="1.5" strokeDasharray="5 4" />
      <text x={x} y={y - 18 * (1 - k)} textAnchor="middle" dominantBaseline="central" fontSize="18">💣</text>
    </g>
  )
}

/** «Призрак» карты, куда игрок собирается её поставить: с радиусом атаки, путём бревна, зоной шара. */
function GhostMark({ ghost, clock }: { ghost: SimGhost; clock: number }) {
  const x = px(ghost.x), y = py(ghost.y)
  const bad = !ghost.ok
  const stroke = bad ? '#ef4454' : '#ffe08a'
  const pulse = 0.6 + 0.4 * Math.sin(clock * 8)
  if (ghost.card === 'Fireball') {
    return (
      <g opacity={pulse} pointerEvents="none">
        <circle cx={x} cy={y} r={SPELLS.Fireball.radius * TILE} fill="rgba(255,140,40,0.18)" stroke={stroke} strokeWidth="2" strokeDasharray="5 4" />
        <text x={x} y={y} textAnchor="middle" dominantBaseline="central" fontSize="22">☄️</text>
      </g>
    )
  }
  if (ghost.card === 'The Log') {
    const lg = SPELLS['The Log']
    const w = lg.width * 2 * TILE
    return (
      <g pointerEvents="none">
        <rect x={x - w / 2} y={y - lg.range * TILE} width={w} height={lg.range * TILE} fill="rgba(255,224,138,0.12)" stroke={stroke} strokeWidth="1.5" strokeDasharray="5 4" />
        <g opacity={pulse}><Log x={x} y={y} clock={0} fade={1} /></g>
      </g>
    )
  }
  const s = UNITS[ghost.card]
  const building = !!s?.building
  const r = Math.max(9, (s?.radius ?? 0.5) * TILE * 1.2)
  const range = s && s.range >= 2 ? (s.range + s.radius) * TILE : 0
  return (
    <g pointerEvents="none">
      {range > 0 && <circle cx={x} cy={y} r={range} fill="rgba(127,181,255,0.08)" stroke={bad ? stroke : '#7fb5ff'} strokeWidth="1.5" strokeDasharray="5 5" />}
      {building
        ? <rect x={x - TILE} y={y - TILE} width={2 * TILE} height={2 * TILE} rx="4" fill="rgba(255,224,138,0.15)" stroke={stroke} strokeWidth="2" strokeDasharray="4 3" />
        : <rect x={x - TILE / 2} y={y - TILE / 2} width={TILE} height={TILE} rx="3" fill="rgba(255,224,138,0.15)" stroke={stroke} strokeWidth="2" strokeDasharray="4 3" />}
      <g opacity={0.55 + 0.25 * pulse} transform={`translate(${x} ${y})`}>
        {building
          ? <rect x={-r - 2.5} y={-r - 2.5} width={2 * r + 5} height={2 * r + 5} rx="7" fill={bad ? FOE : ME} />
          : <circle r={r + 2.5} fill={bad ? FOE : ME} />}
        <text y={1} textAnchor="middle" dominantBaseline="central" fontSize={r * 1.35}>{emojiOf(ghost.card)}</text>
      </g>
    </g>
  )
}
