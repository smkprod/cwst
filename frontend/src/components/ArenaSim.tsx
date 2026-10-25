import { useEffect, useId, useRef, useState } from 'react'
import { DT, TOWER_SIZE, type Entity, type Projectile, type Sim } from '../lib/sim/engine'
import { SPELLS, UNITS } from '../lib/sim/stats'
import { HEROES, type Taunt } from '../lib/sim/heroes'
import { useCardArt } from '../lib/cardArt'
import {
  ArenaDefs, ArenaGround, BarrelIcon, BombIcon, Burst, FireballIcon, H, Medallion, Pill, TILE, TILE_H, TornadoSwirl, TowerArt, UnitArt,
  VIEW_TOP, W, artFor, keeperArt, px, py, towerTop,
} from './ArenaAnim'

/**
 * Живая арена тренажёра: рисует бой из движка (lib/sim) кадр за кадром. Юниты -
 * жетоны с артом карты в кольце своей стороны, с полоской здоровья, выпадом при
 * ударе, вспышкой от урона, кольцом-таймером появления и подписью имени, пока
 * появляются; летающие - над тенью. Снаряды летят с той скоростью, что в игре;
 * огненный шар и бочка - по дуге.
 */

export interface SimGhost { card: string; x: number; y: number; ok: boolean }

/** Подписи тренировочного лагеря, как в игре: соперник-тренер, его клан, «Ты» на своей половине. */
export interface Camp { name: string; clan: string; you: string }

/** Что летит по дуге: заклинания от короля и снаряд Мортиры. */
const LOBS = new Set(['fireball', 'barrel', 'shell'])

const ME = '#3d8bff'
const FOE = '#ef4454'

export function ArenaSim({ sim, running, speed = 1, onTick, onTap, ghost, ghostLabel, zone, names, camp, details = false }: {
  sim: Sim
  running: boolean
  speed?: number
  /** Имя карты на языке игрока - подпись под юнитом, пока он появляется. */
  names?: (card: string) => string
  camp?: Camp
  /** Подробности боя: радиусы атаки и обзора, цели, прыжки, полёт снарядов, слепые зоны. */
  details?: boolean
  /** После каждого шага боя; true - встать (пауза или конец). */
  onTick?: (sim: Sim) => boolean
  onTap?: (x: number, y: number) => void
  ghost?: SimGhost | null
  /** Подпись над «призраком» - на показе решения: «Сюда - Пушка». */
  ghostLabel?: string
  /** Подсветить, куда нельзя: войска - только своя половина, Шахтёр - везде, кроме реки. */
  zone?: 'troop' | 'spell' | 'miner' | null
}) {
  const [clock, setClock] = useState(0)
  const art = useCardArt()
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
    // Поле вписано в рамку с сохранением пропорций - считаем от самой картинки, а не от рамки
    const rect = e.currentTarget.getBoundingClientRect()
    const k = Math.min(rect.width / W, rect.height / H)
    const left = rect.left + (rect.width - W * k) / 2
    const top = rect.top + (rect.height - H * k) / 2
    onTap((e.clientX - left) / k / TILE, (e.clientY - top) / k / TILE_H + VIEW_TOP)
  }

  const towers = sim.entities.filter(e => e.kind === 'tower' && e.y + TOWER_SIZE[e.slot!] / 2 > VIEW_TOP)
  const units = sim.entities.filter(e => e.alive && e.kind !== 'tower').sort((a, b) => a.y - b.y)
  const ground = units.filter(e => !e.s.flying)
  const air = units.filter(e => e.s.flying)
  // Подпись - одна на группу (три скелета появились разом - одно имя), первые 1,6 секунды
  const labeled = new Set<number>()
  if (names) {
    const seen = new Set<string>()
    for (const e of [...units].sort((a, b) => a.id - b.id)) {
      const key = `${e.team}:${e.card}:${e.born.toFixed(2)}`
      if (sim.t - e.born < 1.6 && !e.tunnel && !seen.has(key)) { seen.add(key); labeled.add(e.id) }
    }
  }
  const unit = (e: Entity) => (
    <Unit key={e.id} e={e} clock={clock} now={sim.t} g={g} art={artFor(art, e.card)} label={labeled.has(e.id) ? names?.(e.card) : undefined} />
  )

  return (
    <svg className={`arena ${onTap ? 'arena-tap' : ''}`} viewBox={`0 0 ${W} ${H}`} role="img" aria-label="arena" onPointerDown={tap}>
      <ArenaDefs g={g} />
      <ArenaGround t={clock} g={g} />
      {camp && (
        <text x={px(9)} y={py(21.6)} textAnchor="middle" fontSize="26" fontWeight="900" fill="#fff" opacity="0.28"
          stroke="#2b4a12" strokeWidth="2" paintOrder="stroke" letterSpacing="1">{camp.you}</text>
      )}

      {zone && <Zone kind={zone} />}
      {details && <Details sim={sim} names={names} />}

      {/* Радиус башни, которая сейчас стреляет */}
      {towers.filter(t => t.alive && t.team === 'me' && t.target && t.active).map(t => (
        <ellipse key={`r${t.id}`} cx={px(t.x)} cy={py(t.y)} rx={(t.s.range + t.s.radius) * TILE} ry={(t.s.range + t.s.radius) * TILE_H} fill="none"
          stroke="rgba(255,236,150,0.7)" strokeDasharray="3 7" />
      ))}

      {sim.bombs.map(b => <Bomb key={b.id} b={b} now={sim.t} clock={clock} />)}
      {/* Насмешка Героя Рыцаря: золотой круг, пока враги рядом обязаны бить его */}
      {units.filter(e => e.card === 'Hero Knight' && sim.t - e.abilityAt < TAUNT_DUR).map(e => {
        const k = (sim.t - e.abilityAt) / TAUNT_DUR
        return (
          <ellipse key={`ta${e.id}`} cx={px(e.x)} cy={py(e.y)} rx={TAUNT_R * TILE} ry={TAUNT_R * TILE_H}
            fill="rgba(255,211,77,0.07)" stroke="#ffd34d" strokeWidth={2} strokeDasharray="6 6"
            strokeDashoffset={clock * 30} opacity={1 - k * 0.6} />
        )
      })}
      {sim.vortices.map(v => (
        <g key={v.id} transform={`translate(${px(v.x)} ${py(v.y)})`}>
          <TornadoSwirl rx={SPELLS.Tornado.radius * TILE} ry={SPELLS.Tornado.radius * TILE_H} t={clock}
            k={1 - (sim.t - v.t0) / SPELLS.Tornado.duration} />
        </g>
      ))}

      {towers.map(t => <TowerSim key={t.id} e={t} now={sim.t} g={g} />)}

      {ground.map(unit)}
      {sim.rollers.map(r => <Log key={r.id} x={px(r.x)} y={py(r.y)} clock={clock} fade={0.4 + Math.abs(r.y0 - r.y) / 0.6} />)}
      {sim.projectiles.filter(p => !LOBS.has(p.kind)).map(p => <Shot key={p.id} p={p} />)}
      {air.map(unit)}
      {sim.projectiles.filter(p => LOBS.has(p.kind)).map(p => <Lob key={p.id} p={p} clock={clock} g={g} />)}

      {sim.effects.map(f => {
        const k = Math.min(1, (sim.t - f.t) / f.dur)
        const x = px(f.x), y = py(f.y)
        if (f.kind === 'boom') {
          const r = (f.r ?? 1.5) * TILE * (0.55 + 0.45 * k)
          return (
            <g key={f.id}>
              <ellipse cx={x} cy={y} rx={r} ry={r * (TILE_H / TILE)} fill="rgba(255,140,40,0.3)" stroke="#ffb347" strokeWidth={3 * (1 - k) + 1} opacity={1 - k} />
              <g transform={`translate(${x} ${y})`}><Burst r={16} k={k} /></g>
            </g>
          )
        }
        if (f.kind === 'slam') {
          return <ellipse key={f.id} cx={x} cy={y} rx={(f.r ?? 2) * TILE * k} ry={(f.r ?? 2) * TILE_H * k} fill="rgba(255,230,190,0.2)" stroke="#fff3dc" strokeWidth={4 * (1 - k)} opacity={1 - k} />
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
        if (f.kind === 'heal') {
          return (
            <text key={f.id} x={x} y={y - 22 - 22 * k} textAnchor="middle" fontSize="15" fontWeight="900"
              fill="#7dffb0" stroke="#0b3a1e" strokeWidth="3" paintOrder="stroke" opacity={Math.min(1, 2 * (1 - k))}>{`🥞 ${f.text ?? ''}`}</text>
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

      {ghost && <GhostMark ghost={ghost} clock={clock} g={g} art={artFor(art, ghost.card)} />}
      {ghost && ghostLabel && <Pill x={Math.min(W - 60, Math.max(60, px(ghost.x)))} y={py(ghost.y) - 30} text={ghostLabel} tone="gold" />}

      {camp && <CampHud camp={camp} sim={sim} g={g} />}
    </svg>
  )
}

/**
 * Как в тренировочном лагере игры: слева сверху соперник - портрет короля, имя
 * тренера и клан; справа у реки - короны: сверху его, снизу твои (по сломанным башням).
 */
function CampHud({ camp, sim, g }: { camp: Camp; sim: Sim; g: (n: string) => string }) {
  const broken = (team: 'me' | 'enemy') => sim.entities.filter(e => e.kind === 'tower' && e.team === team && !e.alive).length
  return (
    <g pointerEvents="none">
      <g transform={`translate(${px(0.95)} ${py(1.45)})`}><Medallion src={keeperArt(true, true)} r={11} color={FOE} g={g} /></g>
      <text x={px(1.95)} y={py(1.05)} fontSize="12" fontWeight="900" fill="#ff8fb1" stroke="#3a0b1c" strokeWidth="3" paintOrder="stroke">{camp.name}</text>
      <text x={px(1.95)} y={py(2.05)} fontSize="9.5" fontWeight="800" fill="#fff" stroke="#1b2a10" strokeWidth="2.6" paintOrder="stroke">{camp.clan}</text>
      <Crowns x={px(17.25)} y={py(12.6)} n={broken('me')} color={FOE} />
      <Crowns x={px(17.25)} y={py(19.4)} n={broken('enemy')} color={ME} />
    </g>
  )
}

function Crowns({ x, y, n, color }: { x: number; y: number; n: number; color: string }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <rect x={-11} y={-15} width={22} height={30} rx="5" fill="rgba(13,20,48,0.78)" stroke="rgba(255,255,255,0.35)" />
      <path d="M-7 -2 L-8 -10 L-3.5 -6 L0 -12 L3.5 -6 L8 -10 L7 -2 Z" fill={color} stroke="#ffd34d" strokeWidth="1.3" />
      <text y={11} textAnchor="middle" fontSize="12" fontWeight="900" fill="#fff" stroke="#0d1430" strokeWidth="2.4" paintOrder="stroke">{n}</text>
    </g>
  )
}

const ell = (x: number, y: number, r: number) => ({ cx: px(x), cy: py(y), rx: r * TILE, ry: r * TILE_H })

/**
 * Подробности боя поверх поля - всё по тем же числам, что считает движок:
 * радиус атаки (до края цели), обзор (кого замечает), линия к цели (пунктир - идёт,
 * сплошная - бьёт), зона прыжка Мега-рыцаря, слепая зона Мортиры, радиусы башен
 * (спящий король - серым), куда летит снаряд и какую площадь накроет, путь бревна.
 */
function Details({ sim, names }: { sim: Sim; names?: (card: string) => string }) {
  const units = sim.entities.filter(e => e.alive && e.kind !== 'tower' && !e.tunnel)
  const towers = sim.entities.filter(e => e.alive && e.kind === 'tower' && e.y + TOWER_SIZE[e.slot!] / 2 > VIEW_TOP)
  // Подпись радиуса - одна на группу одинаковых карт стороны
  const labeled = new Set<string>()
  return (
    <g pointerEvents="none">
      {towers.map(t => {
        const sleeping = t.slot === 'K' && !t.active
        const c = t.team === 'me' ? '#7fb5ff' : '#ff8fa3'
        return <ellipse key={`tr${t.id}`} {...ell(t.x, t.y, t.s.range + t.s.radius)} fill="none" stroke={sleeping ? '#9aa3b8' : c}
          strokeOpacity={sleeping ? 0.45 : 0.55} strokeWidth="1.2" strokeDasharray={sleeping ? '2 6' : '6 4'} />
      })}
      {units.map(e => {
        const c = e.team === 'me' ? '#7fb5ff' : '#ff8fa3'
        const reach = e.s.range + e.s.radius
        const key = `${e.team}:${e.card}`
        const label = !labeled.has(key)
        labeled.add(key)
        const t = e.target && e.target.alive ? e.target : null
        return (
          <g key={`d${e.id}`}>
            {e.s.sight > 0 && <ellipse {...ell(e.x, e.y, e.s.sight)} fill="none" stroke={c} strokeOpacity="0.18" strokeDasharray="2 5" />}
            {reach > 0.2 && <ellipse {...ell(e.x, e.y, reach)} fill={c} fillOpacity="0.07" stroke={c} strokeOpacity="0.75" strokeWidth="1.3" />}
            {e.s.minRange && <ellipse {...ell(e.x, e.y, e.s.minRange + e.s.radius)} fill="rgba(239,68,84,0.14)" stroke="#ff7a85" strokeDasharray="4 3" />}
            {e.s.dashMin && e.s.dashMax && (
              <g>
                <ellipse {...ell(e.x, e.y, e.s.dashMin)} fill="none" stroke="#c9a6ff" strokeOpacity="0.8" strokeDasharray="5 4" />
                <ellipse {...ell(e.x, e.y, e.s.dashMax)} fill="none" stroke="#c9a6ff" strokeOpacity="0.8" strokeDasharray="5 4" />
              </g>
            )}
            {e.dash && <line x1={px(e.dash.fx)} y1={py(e.dash.fy)} x2={px(e.dash.tx)} y2={py(e.dash.ty)} stroke="#c9a6ff" strokeWidth="2" strokeDasharray="3 3" />}
            {t && (
              <g>
                <line x1={px(e.x)} y1={py(e.y)} x2={px(t.x)} y2={py(t.y)} stroke={c} strokeWidth={e.locked ? 2 : 1.3}
                  strokeOpacity="0.85" strokeDasharray={e.locked ? undefined : '4 4'} />
                <circle cx={px(t.x)} cy={py(t.y)} r={3} fill={c} />
              </g>
            )}
            {label && reach > 0.2 && (
              <text x={px(e.x)} y={py(e.y) - reach * TILE_H - 3} textAnchor="middle" fontSize="8.5" fontWeight="800" fill="#fff"
                stroke="#0d1430" strokeWidth="2.4" paintOrder="stroke">
                {(names?.(e.card) ?? e.card)} · {e.s.range.toFixed(1)}{e.s.buildingsOnly ? ' 🏰' : e.s.air ? ' 🛩' : ''}
              </text>
            )}
          </g>
        )
      })}
      {sim.projectiles.map(p => (
        <g key={`pp${p.id}`}>
          <line x1={px(p.x)} y1={py(p.y)} x2={px(p.tx)} y2={py(p.ty)} stroke="#ffe08a" strokeOpacity="0.55" strokeDasharray="2 3" />
          {p.splash > 0 && <ellipse {...ell(p.tx, p.ty, p.splash)} fill="rgba(255,180,60,0.12)" stroke="#ffb347" strokeOpacity="0.8" strokeDasharray="4 3" />}
        </g>
      ))}
      {sim.rollers.map(r => (
        <rect key={`rl${r.id}`} x={px(r.x - SPELLS['The Log'].width)} y={py(r.dir < 0 ? r.y - r.left : r.y)} width={SPELLS['The Log'].width * 2 * TILE}
          height={r.left * TILE_H} fill="rgba(255,224,138,0.1)" stroke="#ffe08a" strokeOpacity="0.6" strokeDasharray="5 4" />
      ))}
    </g>
  )
}

/** Красная зона, куда карту не поставить: чужая половина и река, а под башнями - клетки башни. */
function Zone({ kind }: { kind: 'troop' | 'spell' | 'miner' }) {
  if (kind === 'spell') return null
  const boxes = [{ x: 3.5, y: 25.5, s: 3 }, { x: 14.5, y: 25.5, s: 3 }, { x: 9, y: 29, s: 4 }]
  if (kind === 'miner') boxes.push({ x: 3.5, y: 6.5, s: 3 }, { x: 14.5, y: 6.5, s: 3 }, { x: 9, y: 3, s: 4 })
  return (
    <g pointerEvents="none">
      {kind === 'troop'
        ? <rect x={0} y={0} width={W} height={py(17)} fill="rgba(239,68,84,0.3)" />
        : <rect x={0} y={py(15)} width={W} height={2 * TILE_H} fill="rgba(239,68,84,0.3)" />}
      {kind === 'troop' && <line x1={0} x2={W} y1={py(17)} y2={py(17)} stroke="#ff7a85" strokeWidth="2" strokeDasharray="6 5" />}
      {boxes.map((b, i) => (
        <rect key={i} x={px(b.x - b.s / 2)} y={py(b.y - b.s / 2)} width={b.s * TILE} height={b.s * TILE_H} fill="rgba(239,68,84,0.25)" />
      ))}
    </g>
  )
}

function TowerSim({ e, now, g }: { e: Entity; now: number; g: (n: string) => string }) {
  const size = TOWER_SIZE[e.slot!]
  const king = e.slot === 'K'
  const top = towerTop(e.y, size, king)
  const hurt = e.alive && e.sinceHurt < 0.12
  const w = (king ? 3.2 : 2.4) * TILE
  const color = e.team === 'me' ? ME : FOE
  return (
    <g>
      <g transform={hurt ? `translate(${Math.sin(now * 90) * 1.5} 0)` : undefined}>
        <TowerArt x={e.x} y={e.y} size={size} king={king} enemy={e.team === 'enemy'} dead={!e.alive} g={g} />
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
/** Насмешка Героя Рыцаря - для круга на поле. */
const TAUNT_R = (HEROES['Hero Knight'] as Taunt).radius
const TAUNT_DUR = (HEROES['Hero Knight'] as Taunt).duration

function tokenR(e: Entity): number {
  const base = Math.min(16, e.s.radius * TILE * 1.25)
  return Math.max(9, (e.s.count ?? 1) > 1 || e.card === 'Goblins' ? base * 0.8 : base)
}

function Unit({ e, clock, now, g, art, label }: { e: Entity; clock: number; now: number; g: (n: string) => string; art?: string; label?: string }) {
  const r = tokenR(e)
  const hero = e.ability !== undefined
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
  // Брошен Героем Гигантом: высокая дуга поперёк поля
  if (e.thrown) {
    const k = Math.min(1, e.thrown.t / e.thrown.dur)
    lift = Math.sin(Math.PI * k) * 46
    scale = 1 + Math.sin(Math.PI * k) * 0.15
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
        {/* Герой - золотой ободок; после блинов Мини-ПЕККА светится */}
        {hero && <circle r={r + 6} fill="none" stroke="#ffd34d" strokeWidth="2.5" opacity={0.9} />}
        {e.mult > 1 && <circle r={r + 9 + Math.sin(clock * 8) * 1.5} fill="none" stroke="#ff9f43" strokeWidth="2" opacity={0.7} />}
        {e.ability === 'casting' && (
          <circle r={r + 10} fill="none" stroke="#fff" strokeWidth="2" strokeDasharray="4 4" transform={`rotate(${clock * 200})`} />
        )}
        {box
          ? <rect x={-r - 3} y={-r - 3} width={2 * r + 6} height={2 * r + 6} rx="7" fill={color} stroke="#0d1430" strokeWidth="1.2" />
          : <circle r={r + 3} fill={color} stroke="#0d1430" strokeWidth="1.2" />}
        {box
          ? <rect x={-r} y={-r} width={2 * r} height={2 * r} rx="5.5" fill="#16204a" />
          : <circle r={r} fill="#16204a" />}
        <UnitArt card={e.card} r={r} art={art} square={box} g={g} />
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
          <rect x={x - r} y={cy - r * s - 10} width={2 * r} height={5} rx="2.5" fill="rgba(10,14,32,0.85)" />
          <rect x={x - r + 1} y={cy - r * s - 9} width={(2 * r - 2) * Math.max(0, e.hp / e.maxHp)} height={3} rx="1.5" fill={color} />
        </g>
      )}
      {/* Щит Героя Рыцаря - голубая полоска над здоровьем */}
      {e.shield > 0 && (
        <g>
          <rect x={x - r} y={cy - r * s - 17} width={2 * r} height={5} rx="2.5" fill="rgba(10,14,32,0.85)" />
          <rect x={x - r + 1} y={cy - r * s - 16} width={(2 * r - 2) * (e.shield / (e.shieldMax || 1))} height={3} rx="1.5" fill="#7fd8ff" />
        </g>
      )}
      {/* Блины Героя Мини-ПЕККА: три полосы под жетоном, пока способность не нажата */}
      {e.card === 'Hero Mini P.E.K.K.A' && e.ability === 'ready' && !deploying && (
        <g>
          {[0, 1, 2].map(i => (
            <g key={i}>
              <rect x={x - r + i * ((2 * r) / 3)} y={y + r + 4} width={(2 * r) / 3 - 2} height={4} rx="2" fill="rgba(10,14,32,0.85)" />
              <rect x={x - r + i * ((2 * r) / 3)} y={y + r + 4} width={((2 * r) / 3 - 2) * Math.max(0, Math.min(1, e.pancake - i))} height={4} rx="2" fill="#ffb347" />
            </g>
          ))}
        </g>
      )}
      {now - e.abilityAt < 0.8 && e.card !== 'Hero Knight' && (
        <circle cx={x} cy={cy} r={r + 6 + (now - e.abilityAt) * 30} fill="none" stroke="#ffd34d" strokeWidth={3 * (1 - (now - e.abilityAt) / 0.8)} />
      )}
      {label && <Pill x={x} y={cy + r * s + 12} text={label} tone={e.team === 'me' ? 'blue' : 'red'} />}
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

/** Огненный шар, бочка и снаряд Мортиры летят по дуге: высота - от доли пройденного пути, тень - на земле. */
function Lob({ p, clock, g }: { p: Projectile; clock: number; g: (n: string) => string }) {
  const all = Math.hypot(p.tx - p.sx, p.ty - p.sy) || 1
  const k = Math.min(1, 1 - Math.hypot(p.tx - p.x, p.ty - p.y) / all)
  const x = px(p.x), y = py(p.y)
  const h = Math.sin(Math.PI * k) * (p.kind === 'fireball' ? 50 : 70)
  const d = dir(p)
  const fire = p.kind === 'fireball'
  const angle = (Math.atan2(d.y, d.x) * 180) / Math.PI
  return (
    <g>
      <ellipse cx={x} cy={y} rx={9 - h / 14} ry={3.5 - h / 40} fill="rgba(0,0,0,0.35)" />
      {fire && <circle cx={x} cy={y - h} r={13} fill="rgba(255,140,40,0.3)" />}
      <g transform={`translate(${x} ${y - h})`}>
        {fire ? <FireballIcon size={14} angle={angle} g={g} />
          : p.kind === 'shell' ? <circle r={5} fill="#2b2f3a" stroke="#0c0d10" strokeWidth="1.4" />
          : <g transform={`rotate(${clock * 400})`}><BarrelIcon size={22} /></g>}
      </g>
      {/* Куда упадёт - чтобы видеть угрозу заранее */}
      <ellipse cx={px(p.tx)} cy={py(p.ty)} rx={(fire ? SPELLS.Fireball.radius : p.kind === 'shell' ? p.splash : SPELLS['Goblin Barrel'].radius) * TILE}
        ry={(fire ? SPELLS.Fireball.radius : p.kind === 'shell' ? p.splash : SPELLS['Goblin Barrel'].radius) * TILE_H} fill="none"
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
function Bomb({ b, now, clock }: { b: Sim['bombs'][number]; now: number; clock: number }) {
  const k = Math.min(1, (now - b.t) / 0.5)
  const x = px(b.x), y = py(b.y)
  const blink = 0.35 + 0.35 * Math.abs(Math.sin(now * (4 + 10 * (1 - (b.at - now) / (b.at - b.t)))))
  return (
    <g>
      <ellipse cx={x} cy={y} rx={b.r * TILE} ry={b.r * TILE_H} fill={`rgba(239,68,84,${blink * 0.25})`} stroke="#ff7a85" strokeWidth="1.5" strokeDasharray="5 4" />
      <g transform={`translate(${x} ${y - 18 * (1 - k)})`}><BombIcon size={16} spark={Math.abs(Math.sin(clock * 14))} /></g>
    </g>
  )
}

/** «Призрак» карты, куда игрок собирается её поставить: с радиусом атаки, путём бревна, зоной шара. */
function GhostMark({ ghost, clock, g, art }: { ghost: SimGhost; clock: number; g: (n: string) => string; art?: string }) {
  const x = px(ghost.x), y = py(ghost.y)
  const bad = !ghost.ok
  const stroke = bad ? '#ef4454' : '#fff'
  const pulse = 0.6 + 0.4 * Math.sin(clock * 8)
  if (ghost.card === 'Fireball') {
    return (
      <g opacity={pulse} pointerEvents="none">
        <ellipse cx={x} cy={y} rx={SPELLS.Fireball.radius * TILE} ry={SPELLS.Fireball.radius * TILE_H} fill="rgba(255,140,40,0.18)" stroke={stroke} strokeWidth="2" strokeDasharray="5 4" />
        <g transform={`translate(${x} ${y})`}><FireballIcon size={16} g={g} /></g>
      </g>
    )
  }
  if (ghost.card === 'Tornado') {
    return (
      <g pointerEvents="none" transform={`translate(${x} ${y})`} opacity={0.55 + 0.35 * pulse}>
        <TornadoSwirl rx={SPELLS.Tornado.radius * TILE} ry={SPELLS.Tornado.radius * TILE_H} t={clock} />
        {bad && <ellipse rx={SPELLS.Tornado.radius * TILE} ry={SPELLS.Tornado.radius * TILE_H} fill="none" stroke={stroke} strokeWidth="2" />}
      </g>
    )
  }
  if (ghost.card === 'The Log') {
    const lg = SPELLS['The Log']
    const w = lg.width * 2 * TILE
    return (
      <g pointerEvents="none">
        <rect x={x - w / 2} y={y - lg.range * TILE_H} width={w} height={lg.range * TILE_H} fill="rgba(255,224,138,0.18)" stroke={stroke} strokeWidth="1.5" strokeDasharray="5 4" />
        <g opacity={pulse}><Log x={x} y={y} clock={0} fade={1} /></g>
      </g>
    )
  }
  const s = UNITS[ghost.card]
  const building = !!s?.building
  const r = Math.max(9, Math.min(16, (s?.radius ?? 0.5) * TILE * 1.25))
  const range = s && s.range >= 2 ? s.range + s.radius : 0
  return (
    <g pointerEvents="none">
      {range > 0 && <ellipse cx={x} cy={y} rx={range * TILE} ry={range * TILE_H} fill="rgba(61,139,255,0.12)" stroke={bad ? stroke : '#1f5fd1'} strokeWidth="1.5" strokeDasharray="5 5" />}
      {building
        ? <rect x={x - TILE} y={y - TILE_H} width={2 * TILE} height={2 * TILE_H} rx="3" fill="rgba(255,255,255,0.35)" stroke={stroke} strokeWidth="2" strokeDasharray="4 3" />
        : <rect x={x - TILE / 2} y={y - TILE_H / 2} width={TILE} height={TILE_H} rx="2" fill="rgba(255,255,255,0.35)" stroke={stroke} strokeWidth="2" strokeDasharray="4 3" />}
      <g opacity={0.55 + 0.25 * pulse} transform={`translate(${x} ${y})`}>
        {building
          ? <rect x={-r - 3} y={-r - 3} width={2 * r + 6} height={2 * r + 6} rx="7" fill={bad ? FOE : ME} />
          : <circle r={r + 3} fill={bad ? FOE : ME} />}
        <UnitArt card={ghost.card} r={r} art={art} square={building} g={g} />
      </g>
    </g>
  )
}
