/**
 * Эволюции: та же карта, но с особым свойством. В игре карта становится эволюцией
 * после нескольких кругов колоды; в тренажёре эво-карта просто лежит в руке.
 *
 * Цифр эволюций нет в справочнике из файлов игры (он старше большинства эволюций),
 * поэтому они собраны вручную по Liquipedia, Fandom и RoyaleAPI на октябрь 2026,
 * 11-й уровень. Где источники спорили, взято последнее изменение баланса - подписано.
 * Мушкетёр и Пушка пока не добавлены: источники расходятся в самой механике.
 */

export type EvoKind = 'knight' | 'skeletons' | 'valkyrie' | 'iceSpirit' | 'tesla' | 'megaKnight'
  | 'pekka' | 'bats' | 'mortar' | 'archers' | 'barrel'

/** Эво-карта → обычная карта и её свойство. */
export const EVOS: Record<string, { base: string; kind: EvoKind }> = {
  'Evo Knight': { base: 'Knight', kind: 'knight' },
  'Evo Skeletons': { base: 'Skeletons', kind: 'skeletons' },
  'Evo Valkyrie': { base: 'Valkyrie', kind: 'valkyrie' },
  'Evo Ice Spirit': { base: 'Ice Spirit', kind: 'iceSpirit' },
  'Evo Tesla': { base: 'Tesla', kind: 'tesla' },
  'Evo Mega Knight': { base: 'Mega Knight', kind: 'megaKnight' },
  'Evo P.E.K.K.A': { base: 'P.E.K.K.A', kind: 'pekka' },
  'Evo Bats': { base: 'Bats', kind: 'bats' },
  'Evo Mortar': { base: 'Mortar', kind: 'mortar' },
  'Evo Archers': { base: 'Archers', kind: 'archers' },
  'Evo Goblin Barrel': { base: 'Goblin Barrel', kind: 'barrel' },
}

export const isEvo = (card: string) => card in EVOS

/** Обычная карта под эволюцией - для смайлика, арта и подписей. */
export const evoBase = (card: string) => EVOS[card]?.base ?? card

/** Рыцарь: пока идёт (не бьёт), получает на 60% меньше урона (было 80%, срезали в августе 2023). */
export const EVO_KNIGHT_REDUCTION = 0.6

/** Скелеты: после удара каждый делает копию, всего не больше 8 (снова 8 с октября 2023). */
export const EVO_SKELETONS_MAX = 8

/** Валькирия: каждый удар - вихрь радиусом 5 (срезан с 5,5 в декабре 2025) на 0,5 с, 84 урона. */
export const EVO_VALK = { radius: 5, duration: 0.5, dmg: 84, pull: 6 }

/** Ледяной дух: два ледяных удара подряд, каждый замораживает на 1,1 с (с августа 2025). */
export const EVO_ICE = { freeze: 1.1, blasts: 2 }

/**
 * Тесла: при появлении - импульс радиусом 6, оглушение 0,5 с. Урон: 58 на 1-м уровне,
 * на 11-й - обычным множителем, и минус 15% (май 2026).
 */
export const EVO_TESLA = { radius: 6, stun: 0.5, dmg: Math.round(58 * 2.594 * 0.85) }

/** Мега-рыцарь: каждый удар - апперкот, цель отлетает на 4 клетки (тяжёлые - на 2,5). */
export const EVO_MK = { knock: 4, knockHeavy: 2.5, heavyMass: 10 }

/**
 * ПЕККА: за каждое убийство лечится на 3,5–15% здоровья - чем живучее убитый, тем больше,
 * и может «перелечиться» до +50%. Как именно процент зависит от убитого, источники не
 * пишут - здесь он растёт с его здоровьем: скелет 3,5%, рыцарь около 9%, танк 15%.
 */
export const EVO_PEKKA = { min: 0.035, max: 0.15, per: 1 / 20000, overheal: 1.5 }

/**
 * Мыши: на 50% больше здоровья; после удара лечатся (2 импульса в секунду, 15 на 1-м
 * уровне - на 11-м около 39) и могут подняться до двойного здоровья.
 */
export const EVO_BATS = { hp: 1.5, heal: 39, overheal: 2 }

/** Мортира: стреляет раз в 4 с (а не 5), и каждый выстрел выпускает гоблина. */
export const EVO_MORTAR = { hitSpeed: 4 }

/** Лучницы: по целям дальше 4 клеток - усиленный выстрел ×1,5 (Liquipedia), достают до 6 клеток. */
export const EVO_ARCHERS = { from: 4, range: 6, mult: 1.5 }

/** Бочка: вторая бочка-приманка летит в другую линию, её гоблины - 40% здоровья обычных. */
export const EVO_BARREL = { decoyHp: 0.4 }
