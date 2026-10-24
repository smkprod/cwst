import { COST, SPELLS, TOWER_STATS, UNITS, type UnitStats } from './stats'

/**
 * Движок боя для тренажёра: свой маленький Clash Royale на характеристиках из игры
 * (stats.ts, 11-й уровень). Шаг 1/30 секунды, всё детерминировано.
 *
 * Что повторяет игру: цель - ближайшая подходящая в радиусе обзора (кто бьёт только
 * здания, войска не видит; кто не бьёт воздух, летающих не видит); без цели - к
 * ближайшей башне; по земле - только через мосты (Хог реку перепрыгивает, летающие
 * летят); первый удар через время замаха, дальше - в темпе карты; дальние бьют
 * снарядом со своей скоростью; урон по площади; по башням - со своим процентом;
 * король просыпается от урона или падения принцессы; здания разрушаются со временем;
 * Мега-рыцарь бьёт при появлении и прыгает; Шахтёр едет туннелем; Шар роняет бомбу;
 * Голем распадается на големитов; бревно катится и отбрасывает; огненный шар летит
 * от короля и бьёт по площади; бочка летит и выпускает гоблинов; торнадо тянет
 * чужие войска к центру и будит своего короля, если затянуть врага в его радиус.
 *
 * Чего нет: точного обхода препятствий, разворотов, анимаций замаха из игры - юниты
 * считаются кругами со своим радиусом.
 */

export type Team = 'me' | 'enemy'
export type TowerSlot = 'L' | 'R' | 'K'

export interface Entity {
  id: number
  team: Team
  card: string
  s: UnitStats
  kind: 'troop' | 'building' | 'tower'
  slot?: TowerSlot
  x: number
  y: number
  hp: number
  maxHp: number
  deploy: number
  /** Сколько длилось появление - для кольца-таймера. */
  deploy0: number
  /** Время на поле, когда появился. */
  born: number
  /** Двигался на этом шаге - для покачивания при ходьбе. */
  moving: boolean
  cooldown: number
  target: Entity | null
  locked: boolean
  alive: boolean
  active: boolean
  /** Секунды с последнего удара - для анимации выпада. */
  sinceHit: number
  /** Секунды с последнего полученного урона - вспышка. */
  sinceHurt: number
  dash?: { fx: number; fy: number; tx: number; ty: number; t: number; dur: number }
  dashCd: number
  tunnel?: { fx: number; fy: number; t: number; dur: number }
}

export type ProjectileKind = 'arrow' | 'bullet' | 'cannonball' | 'bolt' | 'fireball' | 'barrel' | 'king'

export interface Projectile {
  id: number
  team: Team
  /** Чья атака - для счёта урона по картам. */
  card: string
  kind: ProjectileKind
  x: number
  y: number
  sx: number
  sy: number
  target: Entity | null
  tx: number
  ty: number
  speed: number
  dmg: number
  splash: number
  crownPct: number
  air: boolean
  ground: boolean
  spawn?: { card: string; count: number; deploy: number }
  alive: boolean
}

export interface Roller { id: number; team: Team; x: number; y: number; y0: number; dir: number; left: number; hit: Set<number>; alive: boolean }

/** Торнадо на поле: тянет чужие войска к центру и бьёт раз в hitEvery секунд. */
export interface Vortex { id: number; team: Team; x: number; y: number; t0: number; nextHit: number; alive: boolean }

export interface Effect { id: number; kind: 'boom' | 'dmg' | 'poof' | 'slam' | 'heal'; x: number; y: number; t: number; dur: number; text?: string; r?: number; team?: Team }

export interface Pending { at: number; team: Team; card: string; x: number; y: number }

export const DT = 1 / 30
const RIVER_TOP = 15
const RIVER_BOTTOM = 17
const BRIDGES = [3.5, 14.5]

export const TOWER_POS: Record<Team, Record<TowerSlot, { x: number; y: number }>> = {
  me: { L: { x: 3.5, y: 25.5 }, R: { x: 14.5, y: 25.5 }, K: { x: 9, y: 29 } },
  enemy: { L: { x: 3.5, y: 6.5 }, R: { x: 14.5, y: 6.5 }, K: { x: 9, y: 3 } },
}

/** Своя половина: ниже реки для «me», выше - для «enemy». */
export const sideOf = (y: number): Team | 'river' => (y > RIVER_BOTTOM ? 'me' : y < RIVER_TOP ? 'enemy' : 'river')

export class Sim {
  t = 0
  entities: Entity[] = []
  projectiles: Projectile[] = []
  rollers: Roller[] = []
  vortices: Vortex[] = []
  effects: Effect[] = []
  pending: Pending[] = []
  elixir: Record<Team, number>
  spent: Record<Team, number> = { me: 0, enemy: 0 }
  /** Урон, полученный башнями стороны. */
  towerDamage: Record<Team, number> = { me: 0, enemy: 0 }
  /** Урон по картам: «me:Cannon» - сколько нанесла своя пушка (по юнитам и башням). */
  dealt: Record<string, number> = {}
  /** Падающие бомбы Шара - для отрисовки. */
  bombs: { id: number; x: number; y: number; t: number; at: number; r: number }[] = []
  private nextId = 1
  private later: { at: number; team: Team; fn: () => void }[] = []

  constructor(elixir = 5) {
    this.elixir = { me: elixir, enemy: 10 }
    for (const team of ['me', 'enemy'] as Team[]) {
      for (const slot of ['L', 'R', 'K'] as TowerSlot[]) {
        const s = slot === 'K' ? TOWER_STATS.King : TOWER_STATS.Princess
        const p = TOWER_POS[team][slot]
        this.entities.push(this.make(team, slot === 'K' ? 'King' : 'Princess', s, 'tower', p.x, p.y, 0, slot))
      }
    }
  }

  private make(team: Team, card: string, s: UnitStats, kind: Entity['kind'], x: number, y: number, deploy: number, slot?: TowerSlot): Entity {
    return {
      id: this.nextId++, team, card, s, kind, slot, x, y, hp: s.hp, maxHp: s.hp, deploy, deploy0: deploy, born: this.t,
      moving: false, cooldown: 0, target: null, locked: false, alive: true, active: slot !== 'K', sinceHit: 9, sinceHurt: 9, dashCd: 0,
    }
  }

  tower(team: Team, slot: TowerSlot): Entity {
    return this.entities.find(e => e.kind === 'tower' && e.team === team && e.slot === slot)!
  }

  /** Запланировать постановку карты (сценарий соперника). */
  schedule(p: Pending) { this.pending.push(p) }

  /** Поставить карту сейчас. Возвращает false, если сюда её не поставить. */
  play(team: Team, card: string, x: number, y: number): boolean {
    if (!canDeploy(team, card, x, y)) return false
    this.spent[team] += COST[card] ?? 0
    this.elixir[team] = Math.max(0, this.elixir[team] - (COST[card] ?? 0))
    const king = TOWER_POS[team].K
    if (card === 'Fireball') {
      const sp = SPELLS.Fireball
      this.projectiles.push(this.proj(team, card, 'fireball', king.x, king.y, null, x, y, sp.speed, sp.dmg, sp.radius, sp.crownPct, true, true))
      return true
    }
    if (card === 'The Log') {
      // Бревно катится вперёд - к сопернику
      this.rollers.push({ id: this.nextId++, team, x, y, y0: y, dir: team === 'me' ? -1 : 1, left: SPELLS['The Log'].range, hit: new Set(), alive: true })
      return true
    }
    if (card === 'Tornado') {
      this.vortices.push({ id: this.nextId++, team, x, y, t0: this.t, nextHit: this.t, alive: true })
      return true
    }
    if (card === 'Goblin Barrel') {
      const sp = SPELLS['Goblin Barrel']
      const p = this.proj(team, card, 'barrel', king.x, king.y, null, x, y, sp.speed, 0, 0, 0, false, false)
      p.spawn = { card: 'Goblins', count: sp.count, deploy: sp.spawnDeploy }
      this.projectiles.push(p)
      return true
    }
    const s = UNITS[card]
    if (!s) return false
    if (card === 'Miner') {
      const e = this.make(team, card, s, 'troop', x, y, s.deploy)
      const d = Math.hypot(x - king.x, y - king.y)
      e.tunnel = { fx: king.x, fy: king.y, t: 0, dur: d / (s.tunnelSpeed ?? 10) }
      this.entities.push(e)
      return true
    }
    const n = s.count ?? 1
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2 + 0.6
      const off = n > 1 ? 0.55 : 0
      this.entities.push(this.make(team, card, s, s.building ? 'building' : 'troop', x + Math.cos(a) * off, y + Math.sin(a) * off, s.deploy))
    }
    return true
  }

  private proj(team: Team, card: string, kind: ProjectileKind, x: number, y: number, target: Entity | null, tx: number, ty: number,
    speed: number, dmg: number, splash: number, crownPct: number, air: boolean, ground: boolean): Projectile {
    return { id: this.nextId++, team, card, kind, x, y, sx: x, sy: y, target, tx, ty, speed, dmg, splash, crownPct, air, ground, alive: true }
  }

  /** Есть ли у стороны что-то, что ещё может нанести урон: юниты, летящая бочка, туннель, бомба. */
  threat(team: Team): boolean {
    return this.entities.some(e => e.alive && e.team === team && e.kind !== 'tower')
      || this.projectiles.some(p => p.alive && p.team === team && !!p.spawn)
      || this.pending.some(p => p.team === team)
      || this.later.some(l => l.team === team)
  }

  private step() {
    const dt = DT
    this.t += dt
    for (const p of [...this.pending]) {
      if (p.at <= this.t) {
        this.pending.splice(this.pending.indexOf(p), 1)
        this.play(p.team, p.card, p.x, p.y)
      }
    }
    this.elixir.me = Math.min(10, this.elixir.me + dt / 2.8)

    for (const e of this.entities) {
      if (!e.alive) continue
      e.moving = false
      e.sinceHit += dt
      e.sinceHurt += dt
      if (e.tunnel) {
        e.tunnel.t += dt
        if (e.tunnel.t >= e.tunnel.dur) { e.tunnel = undefined } else continue
      }
      if (e.deploy > 0) {
        e.deploy -= dt
        if (e.deploy <= 0 && e.s.dashDmg) this.areaDamage(e, e.x, e.y, e.s.dashRadius ?? 2, e.s.dashDmg, true, false, 'slam')
        continue
      }
      if (e.kind === 'building' && e.s.lifetime > 0) {
        e.hp -= (e.maxHp / e.s.lifetime) * dt
        if (e.hp <= 0) { this.kill(e); continue }
      }
      if (e.kind === 'tower' && !e.active) continue
      this.think(e, dt)
    }

    this.separate()
    this.moveProjectiles(dt)
    this.moveRollers(dt)
    this.moveVortices(dt)
    this.effects = this.effects.filter(f => this.t - f.t < f.dur)
    this.bombs = this.bombs.filter(b => b.at > this.t)
    this.entities = this.entities.filter(e => e.alive || e.kind === 'tower')
  }

  private canHit(a: Entity, b: Entity): boolean {
    if (!b.alive || b.team === a.team || b.tunnel) return false
    if (a.s.buildingsOnly && b.kind === 'troop') return false
    return b.s.flying ? a.s.air : a.s.ground
  }

  private gap(a: Entity, b: Entity) {
    return Math.hypot(a.x - b.x, a.y - b.y) - a.s.radius - b.s.radius
  }

  private think(e: Entity, dt: number) {
    if (e.dash) {
      e.dash.t += dt
      const k = Math.min(1, e.dash.t / e.dash.dur)
      e.x = e.dash.fx + (e.dash.tx - e.dash.fx) * k
      e.y = e.dash.fy + (e.dash.ty - e.dash.fy) * k
      if (k >= 1) {
        e.dash = undefined
        this.areaDamage(e, e.x, e.y, e.s.dashRadius ?? 2.2, e.s.dashDmg ?? e.s.dmg, true, false, 'slam')
        e.dashCd = 0.9
        e.cooldown = e.s.load
      }
      return
    }
    e.dashCd = Math.max(0, e.dashCd - dt)

    // Цель: пока не начал бить - ближайшая в обзоре (так работают отвлечения)
    if (!e.target || !e.target.alive || (!e.locked && e.kind !== 'tower')) {
      const prev = e.target
      e.target = this.acquire(e)
      if (e.target !== prev) e.locked = false
    } else if (e.kind === 'tower' && (!this.canHit(e, e.target) || this.gap(e, e.target) > e.s.range)) {
      e.target = this.acquire(e)
      e.locked = false
    }
    const t = e.target
    if (!t) return

    const inRange = this.gap(e, t) <= e.s.range
    if (!inRange) {
      e.locked = false
      if (e.kind !== 'troop') return
      // Мега-рыцарь прыгает на цель на расстоянии 3,5-5 клеток
      const d = Math.hypot(t.x - e.x, t.y - e.y)
      if (e.s.dashDmg && e.dashCd <= 0 && d >= (e.s.dashMin ?? 3.5) && d <= (e.s.dashMax ?? 5) && t.kind !== 'tower'
          && sideOf(t.y) === sideOf(e.y)) {
        e.dash = { fx: e.x, fy: e.y, tx: t.x, ty: t.y, t: 0, dur: 0.8 }
        return
      }
      const w = this.waypoint(e, t.x, t.y)
      const dx = w.x - e.x, dy = w.y - e.y
      const len = Math.hypot(dx, dy) || 1
      const step = Math.min(len, e.s.speed * dt)
      e.x += (dx / len) * step
      e.y += (dy / len) * step
      e.moving = true
      return
    }

    if (!e.locked) { e.locked = true; e.cooldown = e.s.load }
    e.cooldown -= dt
    if (e.cooldown > 0) return
    e.cooldown = e.s.hitSpeed
    e.sinceHit = 0
    if (e.s.projSpeed > 0 || e.kind === 'tower') {
      const kind: ProjectileKind = e.kind === 'tower' ? (e.slot === 'K' ? 'king' : 'arrow')
        : e.card === 'Cannon' ? 'cannonball' : e.card === 'X-Bow' ? 'bolt' : 'bullet'
      this.projectiles.push(this.proj(e.team, e.card, kind, e.x, e.y - (e.kind === 'tower' ? 0.6 : 0), t, t.x, t.y,
        e.s.projSpeed || 10, e.s.dmg, e.s.splash, e.s.crownPct, e.s.air, e.s.ground))
    } else if (e.s.splash > 0) {
      this.areaDamage(e, t.x, t.y, e.s.splash, e.s.dmg, e.s.ground, e.s.air)
    } else {
      this.damage(t, e.s.dmg, e.s.crownPct, e.team, e.card)
    }
  }

  private acquire(e: Entity): Entity | null {
    const sight = e.kind === 'tower' ? e.s.range : e.s.sight
    let best: Entity | null = null
    let bestD = Infinity
    for (const c of this.entities) {
      if (!this.canHit(e, c)) continue
      const d = this.gap(e, c)
      if (d <= sight && d < bestD) { best = c; bestD = d }
    }
    if (best || e.kind !== 'troop') return best
    // Без цели в обзоре - к башне своей линии, пала - к королю
    const foe: Team = e.team === 'me' ? 'enemy' : 'me'
    const lane: TowerSlot = e.x < 9 ? 'L' : 'R'
    const tw = this.tower(foe, lane)
    return tw.alive ? tw : this.tower(foe, 'K')
  }

  /** Куда идти по земле: через мост своей линии, если цель за рекой. */
  private waypoint(e: Entity, tx: number, ty: number): { x: number; y: number } {
    if (e.s.flying || e.s.jumpsRiver) return { x: tx, y: ty }
    const from = sideOf(e.y), to = sideOf(ty)
    if (from === to && from !== 'river') return { x: tx, y: ty }
    const bx = BRIDGES.reduce((a, b) => (Math.abs(b - e.x) < Math.abs(a - e.x) ? b : a))
    if (Math.abs(e.x - bx) > 0.6 && from !== 'river') return { x: bx, y: from === 'me' ? RIVER_BOTTOM + 0.3 : RIVER_TOP - 0.3 }
    if (from === 'river') return { x: bx, y: to === 'me' ? RIVER_BOTTOM + 0.4 : RIVER_TOP - 0.4 }
    return { x: bx, y: from === 'me' ? RIVER_TOP - 0.4 : RIVER_BOTTOM + 0.4 }
  }

  private separate() {
    const ts = this.entities.filter(e => e.alive && e.kind === 'troop' && !e.tunnel && !e.dash)
    for (let i = 0; i < ts.length; i++) {
      for (let j = i + 1; j < ts.length; j++) {
        const a = ts[i], b = ts[j]
        if (a.s.flying !== b.s.flying) continue
        const dx = b.x - a.x, dy = b.y - a.y
        const d = Math.hypot(dx, dy)
        const min = (a.s.radius + b.s.radius) * 0.85
        if (d >= min) continue
        // Тяжёлый толкает лёгкого: Гигант (масса 18) раздвигает скелетов (1), а не встаёт
        const ma = Math.max(1, a.s.mass), mb = Math.max(1, b.s.mass)
        const ka = mb / (ma + mb), kb = ma / (ma + mb)
        const nx = d > 0.01 ? dx / d : Math.cos(a.id), ny = d > 0.01 ? dy / d : Math.sin(a.id)
        const push = min - d
        a.x -= nx * push * ka; a.y -= ny * push * ka
        b.x += nx * push * kb; b.y += ny * push * kb
        // Кто идёт не к этому юниту - обходит его боком, а не упирается лбом
        if (a.team !== b.team) {
          let tx = -ny, ty = nx
          if (tx * (9 - a.x) < 0) { tx = -tx; ty = -ty }
          if (a.target !== b) { a.x += tx * push * ka * 0.8; a.y += ty * push * ka * 0.8 }
          if (b.target !== a) { b.x -= tx * push * kb * 0.8; b.y -= ty * push * kb * 0.8 }
        }
      }
    }
    // Башни и здания не проходят насквозь - обходят по краю
    const walls = this.entities.filter(e => e.alive && e.kind !== 'troop')
    for (const a of ts) {
      if (a.s.flying) continue
      for (const w of walls) {
        let dx = a.x - w.x
        const dy = a.y - w.y
        const min = a.s.radius + w.s.radius
        const d = Math.hypot(dx, dy)
        if (d >= min) continue
        // Ровно по оси - сдвигаем к центру поля, чтобы обойти, а не упереться
        if (Math.abs(dx) < 0.05) dx = w.x > 9 ? -0.05 : 0.05
        const k = min / (Math.hypot(dx, dy) || 1)
        a.x = w.x + dx * k
        a.y = w.y + dy * k
      }
    }
  }

  private moveProjectiles(dt: number) {
    for (const p of this.projectiles) {
      if (!p.alive) continue
      if (p.target && p.target.alive) { p.tx = p.target.x; p.ty = p.target.y }
      const dx = p.tx - p.x, dy = p.ty - p.y
      const d = Math.hypot(dx, dy)
      const step = p.speed * dt
      if (d > step) { p.x += (dx / d) * step; p.y += (dy / d) * step; continue }
      p.x = p.tx; p.y = p.ty; p.alive = false
      if (p.spawn) {
        const s = UNITS[p.spawn.card]
        for (let i = 0; i < p.spawn.count; i++) {
          const a = (i / p.spawn.count) * Math.PI * 2 + 0.3
          const g = this.make(p.team, p.spawn.card, s, 'troop', p.x + Math.cos(a) * 0.9, p.y + Math.sin(a) * 0.6, p.spawn.deploy)
          this.entities.push(g)
        }
        continue
      }
      if (p.splash > 0) {
        this.areaDamage(null, p.x, p.y, p.splash, p.dmg, p.ground, p.air, p.kind === 'fireball' ? 'boom' : undefined, p.team, p.crownPct, p.card)
      } else if (p.target && p.target.alive) {
        this.damage(p.target, p.dmg, p.crownPct, p.team, p.card)
      }
    }
    this.projectiles = this.projectiles.filter(p => p.alive)
  }

  /**
   * Торнадо: войска соперника в круге тянет к центру со скоростью из игры, урон -
   * раз в полсекунды (войскам полный, башням со своим процентом, зданиям никакого).
   * Король своей стороны просыпается, если затянутый враг попал в его радиус.
   */
  private moveVortices(dt: number) {
    const sp = SPELLS.Tornado
    for (const v of this.vortices) {
      if (!v.alive) continue
      if (this.t - v.t0 > sp.duration) { v.alive = false; continue }
      const hit = this.t >= v.nextHit - 1e-6
      if (hit) v.nextHit += sp.hitEvery
      const king = this.tower(v.team, 'K')
      for (const e of this.entities) {
        if (!e.alive || e.team === v.team || e.tunnel) continue
        const dx = v.x - e.x, dy = v.y - e.y
        const d = Math.hypot(dx, dy)
        if (d - e.s.radius > sp.radius) continue
        if (e.kind === 'troop') {
          const step = Math.min(d, sp.pull * dt)
          if (d > 0.01) { e.x += (dx / d) * step; e.y += (dy / d) * step }
          if (king.alive && !king.active && kingEdge(e, king) <= KING_PULL) king.active = true
        }
        if (hit && e.kind !== 'building') this.damage(e, sp.dps * sp.hitEvery, sp.crownPct, v.team, 'Tornado')
      }
    }
    this.vortices = this.vortices.filter(v => v.alive)
  }

  private moveRollers(dt: number) {
    const sp = SPELLS['The Log']
    for (const r of this.rollers) {
      if (!r.alive) continue
      const step = sp.speed * dt
      r.y += r.dir * step
      r.left -= step
      for (const e of this.entities) {
        if (!e.alive || e.team === r.team || e.s.flying || e.tunnel || r.hit.has(e.id)) continue
        if (Math.abs(e.x - r.x) <= sp.width + e.s.radius * 0.5 && Math.abs(e.y - r.y) <= sp.depth + e.s.radius * 0.5) {
          r.hit.add(e.id)
          this.damage(e, sp.dmg, sp.crownPct, r.team, 'The Log')
          if (e.kind === 'troop' && e.s.speed > 0) e.y += r.dir * sp.pushback
        }
      }
      if (r.left <= 0) r.alive = false
    }
    this.rollers = this.rollers.filter(r => r.alive)
  }

  private areaDamage(src: Entity | null, x: number, y: number, radius: number, dmg: number, ground: boolean, air: boolean,
    fx?: Effect['kind'], team?: Team, crownPct = 0, card?: string) {
    const side = team ?? src!.team
    if (fx) this.effects.push({ id: this.nextId++, kind: fx, x, y, t: this.t, dur: 0.6, r: radius })
    for (const e of this.entities) {
      if (!e.alive || e.team === side || e.tunnel) continue
      if (e.s.flying ? !air : !ground) continue
      if (Math.hypot(e.x - x, e.y - y) - e.s.radius > radius) continue
      this.damage(e, dmg, src ? src.s.crownPct : crownPct, side, card ?? src?.card ?? '')
    }
  }

  private damage(e: Entity, dmg: number, crownPct: number, from: Team, card: string) {
    if (!e.alive) return
    const real = Math.round(e.kind === 'tower' && crownPct ? dmg * (1 + crownPct / 100) : dmg)
    const key = `${from}:${card}`
    this.dealt[key] = (this.dealt[key] ?? 0) + Math.min(real, e.hp)
    e.hp -= real
    e.sinceHurt = 0
    if (e.kind === 'tower') {
      this.towerDamage[e.team] += Math.min(real, real + Math.min(0, e.hp))
      if (e.slot === 'K') e.active = true
      this.effects.push({ id: this.nextId++, kind: 'dmg', x: e.x, y: e.y - 1.6, t: this.t, dur: 0.9, text: `−${real}`, team: e.team })
    }
    if (e.hp <= 0) this.kill(e)
  }

  private kill(e: Entity) {
    e.alive = false
    e.hp = 0
    this.effects.push({ id: this.nextId++, kind: 'poof', x: e.x, y: e.y, t: this.t, dur: 0.5 })
    if (e.kind === 'tower' && e.slot !== 'K') this.tower(e.team, 'K').active = true
    if (e.s.deathDmg) {
      // Бомба Шара падает с задержкой (как в игре); урон при смерти Голема - сразу
      const delay = e.s.deathDelay ?? 0
      const x = e.x, y = e.y, team = e.team, dmg = e.s.deathDmg, r = e.s.deathRadius ?? 2, card = e.card
      const hit = () => this.areaDamage(null, x, y, r, dmg, true, false, 'boom', team, 0, card)
      if (delay > 0) {
        this.later.push({ at: this.t + delay, team, fn: hit })
        this.bombs.push({ id: this.nextId++, x, y, t: this.t, at: this.t + delay, r })
      } else hit()
    }
    if (e.s.deathSpawn) {
      const s = UNITS[e.s.deathSpawn]
      for (let i = 0; i < (e.s.deathSpawnCount ?? 1); i++) {
        this.entities.push(this.make(e.team, e.s.deathSpawn, s, 'troop', e.x + (i ? 0.8 : -0.8), e.y, 0.5))
      }
    }
  }

  /** Шаг 1/30 секунды вместе с отложенными событиями (бомба Шара). */
  tick() {
    this.step()
    for (const l of [...this.later]) {
      if (l.at <= this.t) { this.later.splice(this.later.indexOf(l), 1); l.fn() }
    }
  }

  advance(seconds: number) {
    const end = this.t + seconds
    while (this.t < end - 1e-6) this.tick()
  }
}

/** Можно ли поставить карту сюда: войска и здания - на свою половину и не в башню; заклинания - куда угодно. */
export function canDeploy(team: Team, card: string, x: number, y: number): boolean {
  if (x < 0 || x > 18 || y < 0 || y > 32) return false
  if (card === 'Fireball' || card === 'The Log' || card === 'Goblin Barrel' || card === 'Tornado') return true
  if (card === 'Miner') return sideOf(y) !== 'river' && !onTower(x, y)
  const side = sideOf(y)
  if (side !== team) return false
  return !onTower(x, y)
}

function onTower(x: number, y: number): boolean {
  for (const team of ['me', 'enemy'] as Team[]) {
    for (const slot of ['L', 'R', 'K'] as TowerSlot[]) {
      const p = TOWER_POS[team][slot]
      const half = slot === 'K' ? 2 : 1.5
      if (Math.abs(x - p.x) < half && Math.abs(y - p.y) < half) return true
    }
  }
  return false
}

export const TOWER_SIZE: Record<TowerSlot, number> = { L: 3, R: 3, K: 4 }

/**
 * Король просыпается от Торнадо, если врага затянуло вплотную к королевской башне:
 * не дальше стольких клеток от края её клеток (до края самого юнита). Подобрано так,
 * что срабатывают описанные игроками постановки (Хог - Торнадо на 2-ю клетку перед
 * королём со сдвигом на 2 к башне; бочка - Торнадо между башней и королём), а
 * вихрь, уводящий врага от короля, - нет.
 */
const KING_PULL = 1.0

/** Расстояние от юнита до края клеток королевской башни (0 - касается). */
function kingEdge(e: Entity, king: Entity): number {
  const half = TOWER_SIZE.K / 2
  const dx = Math.max(0, Math.abs(e.x - king.x) - half)
  const dy = Math.max(0, Math.abs(e.y - king.y) - half)
  return Math.max(0, Math.hypot(dx, dy) - e.s.radius)
}
