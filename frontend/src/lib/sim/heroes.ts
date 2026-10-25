import { UNITS, type UnitStats } from './stats'

/**
 * Герои: тело то же, что у обычной карты, а разница - способность, которую игрок
 * включает сам за эликсир, когда Герой уже на поле. Одна способность за выход.
 *
 * Цифр способностей нет в справочнике из файлов игры (он старше Героев), поэтому они
 * собраны вручную по Liquipedia, Fandom и разборам RoyaleAPI на октябрь 2026 - 11-й
 * уровень. Где источники спорили, взято последнее изменение баланса; так и подписано
 * у каждой цифры. Если Supercell поменяет баланс - править здесь.
 */

export type HeroKind = 'taunt' | 'hurl' | 'turret' | 'boost'

interface Base { kind: HeroKind; cost: number; cast: number }

/**
 * «Насмешка» Героя Рыцаря: щит поверх здоровья и все враги рядом бьют только его.
 * Liquipedia: 2 эликсира, каст 1,2 с, радиус 6,5 (срезан с 7,5 в марте 2026), 5 с.
 * Щит: 299 на 1-м уровне, на 11-м - тем же множителем, что здоровье Рыцаря (690 → 1766).
 * Не действует на тех, кто бьёт только здания (Хог, Гигант), - действует на башни.
 */
export interface Taunt extends Base { kind: 'taunt'; radius: number; duration: number; shield: number }

/**
 * «Бросок» Героя Гиганта: хватает самого живучего врага рядом и бросает в другую линию.
 * Liquipedia/Fandom: 2 эликсира, задержка 1 с, 9 клеток поперёк поля, 135 урона при
 * падении (летающих не ранит), оглушение 2 с. Радиус: 2,5 (RoyaleAPI) против 2 (Fandom) - 2,5.
 */
export interface Hurl extends Base { kind: 'hurl'; radius: number; distance: number; dmg: number; stun: number; flight: number }

/**
 * «Турель» Героя Мушкетёра: ставит турель на 3 клетки вперёд, при установке бьёт по земле.
 * Liquipedia: 3 эликсира, задержка 0,2 с, турель 217 урона раз в 0,5 с, дальность 4, живёт 10 с,
 * по земле и воздуху. Урон при установке 204 в радиусе 2 (RoyaleAPI). Здоровье турели -
 * 1536 (единственная найденная цифра, из разбора тестовой версии).
 */
export interface Turret extends Base { kind: 'turret'; forward: number; deployDmg: number; deployRadius: number }

/**
 * «Блинный завтрак» Героя Мини-ПЕККА: блины копятся со временем и от ударов, способность
 * поднимает уровень на столько, сколько полос набралось, и лечит.
 * RoyaleAPI: 1 эликсир, +1/+2/+3/+5 уровней за 0/1/2/3 полосы, удар даёт 40% полосы.
 * Полоса сама набирается за 22 с (с февраля 2026, было 20), лечит 30% (с января 2026, было 40%).
 * Уровень в игре - около +10% к здоровью и урону.
 */
export interface Boost extends Base { kind: 'boost'; cook: number; perHit: number; levels: number[]; heal: number }

export type HeroAbility = Taunt | Hurl | Turret | Boost

export const HEROES: Record<string, HeroAbility> = {
  'Hero Knight': { kind: 'taunt', cost: 2, cast: 1.2, radius: 6.5, duration: 5, shield: Math.round(299 * 1766 / 690) },
  'Hero Giant': { kind: 'hurl', cost: 2, cast: 1.0, radius: 2.5, distance: 9, dmg: 135, stun: 2, flight: 0.7 },
  'Hero Musketeer': { kind: 'turret', cost: 3, cast: 0.2, forward: 3, deployDmg: 204, deployRadius: 2 },
  'Hero Mini P.E.K.K.A': { kind: 'boost', cost: 1, cast: 0.3, cook: 22, perHit: 0.4, levels: [1, 2, 3, 5], heal: 0.3 },
}

/** Турель Героя Мушкетёра - здание на 10 секунд; снаряд как у Мушкетёра. */
export const TURRET: UnitStats = {
  hp: 1536, dmg: 217, hitSpeed: 0.5, load: 0.2, speed: 0, range: 4, sight: 4, radius: 0.6, deploy: 0.3,
  air: true, ground: true, buildingsOnly: false, flying: false, splash: 0, projSpeed: UNITS.Musketeer.projSpeed,
  jumpsRiver: false, crownPct: 0, lifetime: 10, mass: 0, building: true,
}

export const isHero = (card: string) => card in HEROES

/** Обычная карта под Героем - для смайлика и запасного арта. */
export const heroBase = (card: string) => (isHero(card) ? card.slice('Hero '.length) : card)

/** Уровень в игре - примерно +10% к здоровью и урону. */
export const LEVEL_STEP = 1.1
