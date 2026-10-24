import type { Lang } from './i18n'
import type { TipId } from './tips'
import type { Sim } from './sim/engine'
import { COST, UNITS } from './sim/stats'
import type { Outcome, Scenario } from './sim/scenario'

/**
 * Тренажёр «Задефай атаку» на движке боя (lib/sim): соперник играет по сценарию, на
 * решающем моменте игра встаёт - игрок ставит карту, и бой досчитывается по
 * характеристикам карт из игры. Оценка - урон по башне против того же боя без защиты
 * и размен эликсира; объяснение собирается из того, что реально произошло.
 *
 * Координаты - клетки арены 18×32, y сверху: река 15-17, своя половина ниже 17.
 */

export type { Grade } from './sim/scenario'

export type CardKind = 'troop' | 'building' | 'spell'

export interface HandCard { card: string; cost: number; kind: CardKind }

const SPELL_CARDS = new Set(['Fireball', 'The Log', 'Goblin Barrel'])

export function cardInfo(card: string): HandCard {
  const kind: CardKind = SPELL_CARDS.has(card) ? 'spell' : UNITS[card]?.building ? 'building' : 'troop'
  return { card, cost: COST[card] ?? 0, kind }
}

export const CARD_NAMES: Record<Lang, Record<string, string>> = {
  ru: { 'Cannon': 'Пушка', 'Knight': 'Рыцарь', 'Skeletons': 'Скелеты', 'Fireball': 'Огн. шар', 'The Log': 'Бревно', 'Musketeer': 'Мушкетёр',
    'Mini P.E.K.K.A': 'Мини-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големит', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-рыцарь', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гоблины', 'Balloon': 'Шар', 'Giant': 'Гигант', 'X-Bow': 'Икс-лук', 'Miner': 'Шахтёр' },
  uk: { 'Cannon': 'Гармата', 'Knight': 'Лицар', 'Skeletons': 'Скелети', 'Fireball': 'Вогн. куля', 'The Log': 'Колода', 'Musketeer': 'Мушкетерка',
    'Mini P.E.K.K.A': 'Міні-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големіт', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-лицар', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гобліни', 'Balloon': 'Куля', 'Giant': 'Гігант', 'X-Bow': 'Ікс-лук', 'Miner': 'Шахтар' },
  en: { 'Cannon': 'Cannon', 'Knight': 'Knight', 'Skeletons': 'Skeletons', 'Fireball': 'Fireball', 'The Log': 'Log', 'Musketeer': 'Musketeer',
    'Mini P.E.K.K.A': 'Mini PEKKA', 'Golem': 'Golem', 'Golemite': 'Golemite', 'Hog Rider': 'Hog', 'Mega Knight': 'Mega Knight', 'Goblin Barrel': 'Barrel',
    'Goblins': 'Goblins', 'Balloon': 'Balloon', 'Giant': 'Giant', 'X-Bow': 'X-Bow', 'Miner': 'Miner' },
}

export interface Verdict { grade: 'perfect' | 'ok' | 'fail'; reason: string }

export interface Puzzle {
  id: string
  tip: TipId
  /** Чем атакует соперник - для иконки и объяснений. */
  enemyCard: string
  hand: string[]
  sc: Scenario
  /** Своя оценка по месту вместо урона по башне (задача про эликсир). */
  custom?: (card: string, x: number, y: number) => Verdict
}

/** Куда встаёт карта: здание 2×2 - на угол клеток, войско - в центр клетки, заклинание - куда тапнули. */
export function snap(card: string, x: number, y: number): { x: number; y: number } {
  const kind = cardInfo(card).kind
  if (kind === 'building') return { x: Math.min(17, Math.max(1, Math.round(x))), y: Math.min(31, Math.max(18, Math.round(y))) }
  if (kind === 'spell') return { x: Math.round(x * 2) / 2, y: Math.round(y * 2) / 2 }
  return { x: Math.min(17.5, Math.max(0.5, Math.floor(x) + 0.5)), y: Math.floor(y) + 0.5 }
}

/** Пауза, когда юнит соперника дошёл до ряда y. */
const reach = (card: string, y: number) => (sim: Sim) =>
  sim.entities.some(e => e.alive && e.team === 'enemy' && e.card === card && e.deploy <= 0 && e.y >= y)

/** Пауза, когда снаряд соперника пролетел долю пути. */
const flown = (part: number) => (sim: Sim) => sim.projectiles.some(p => {
  if (p.team !== 'enemy') return false
  const all = Math.hypot(p.tx - p.sx, p.ty - p.sy) || 1
  return 1 - Math.hypot(p.tx - p.x, p.ty - p.y) / all >= part
})

/** Пауза, когда Шахтёр вылез из-под земли. */
const surfaced = (sim: Sim) => sim.entities.some(e => e.alive && e.team === 'enemy' && e.card === 'Miner' && !e.tunnel)

export const PUZZLES: Puzzle[] = [
  {
    id: 'hog', tip: 'anti_hog', enemyCard: 'Hog Rider', hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'],
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 3.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [100, 400] },
  },
  {
    id: 'mk', tip: 'kite', enemyCard: 'Mega Knight', hand: ['Skeletons', 'Mini P.E.K.K.A', 'Musketeer', 'Fireball'],
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mega Knight', x: 3.5, y: 13.5 }], pause: reach('Mega Knight', 17.6), horizon: 16, limits: [100, 600] },
  },
  {
    id: 'barrel', tip: 'log_barrel', enemyCard: 'Goblin Barrel', hand: ['The Log', 'Fireball', 'Skeletons', 'Knight'],
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Goblin Barrel', x: 3.5, y: 23.4 }], pause: flown(0.86), horizon: 10, limits: [60, 130] },
  },
  {
    id: 'balloon', tip: 'anti_air', enemyCard: 'Balloon', hand: ['Musketeer', 'Cannon', 'Knight', 'Skeletons'],
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Balloon', x: 14.5, y: 13.5 }], pause: reach('Balloon', 18.5), horizon: 14, limits: [250, 450] },
  },
  {
    id: 'giant', tip: 'anti_tank', enemyCard: 'Giant', hand: ['Cannon', 'Mini P.E.K.K.A', 'Skeletons', 'Fireball'],
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Giant', x: 14.5, y: 13.5 }], pause: reach('Giant', 17.4), horizon: 18, limits: [100, 600] },
  },
  {
    id: 'xbow', tip: 'anti_siege', enemyCard: 'X-Bow', hand: ['Knight', 'Fireball', 'Skeletons', 'Cannon'],
    sc: { elixir: 6, enemy: [{ at: 0, card: 'X-Bow', x: 14.5, y: 13 }], pause: 0.6, horizon: 16, limits: [150, 450] },
  },
  {
    id: 'miner', tip: 'anti_miner', enemyCard: 'Miner', hand: ['Skeletons', 'Knight', 'The Log', 'Fireball'],
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Miner', x: 3.5, y: 23.3 }], pause: surfaced, horizon: 10, limits: [100, 150] },
  },
  {
    id: 'elixir', tip: 'elixir', enemyCard: 'Golem', hand: ['Golem', 'Hog Rider', 'Musketeer', 'Skeletons'],
    sc: { elixir: 10, enemy: [], pause: 0.3, horizon: 6, limits: [0, 0] },
    custom: (card, _x, y) => {
      if (card === 'Golem') return y >= 30 ? { grade: 'perfect', reason: 'elixir_perfect' } : y >= 26 ? { grade: 'ok', reason: 'elixir_golem_mid' } : { grade: 'fail', reason: 'elixir_golem_bridge' }
      if (card === 'Hog Rider') return { grade: 'ok', reason: 'elixir_hog' }
      if (card === 'Musketeer') return y >= 26 ? { grade: 'ok', reason: 'elixir_musk' } : { grade: 'fail', reason: 'elixir_musk_front' }
      return { grade: 'fail', reason: 'elixir_skel' }
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
    mk: { title: 'Мега-рыцарь у моста', task: 'Мега-рыцарь перешёл мост и идёт на левую башню. Останови его.' },
    barrel: { title: 'Гоблинская бочка', task: 'Бочка летит на левую башню. Сбей гоблинов при приземлении.' },
    balloon: { title: 'Шар над рекой', task: 'Шар летит на правую башню. Встреть его, пока он не начал бить.' },
    giant: { title: 'Гигант в правой линии', task: 'Гигант перешёл мост. Защитись с минимумом урона.' },
    xbow: { title: 'Икс-лук у реки', task: 'Соперник поставил Икс-лук у реки. Не дай ему разбить башню.' },
    miner: { title: 'Шахтёр у башни', task: 'Шахтёр вылез у левой башни. Убери его дешевле.' },
    elixir: { title: '10 эликсира', task: 'У тебя 10 эликсира, соперник молчит. Начни атаку правильно.' },
  },
  uk: {
    hog: { title: 'Хог у лівій лінії', task: 'Хог перейшов міст. Відбийся так, щоб вежа не постраждала.' },
    mk: { title: 'Мега-лицар біля мосту', task: 'Мега-лицар перейшов міст і йде на ліву вежу. Зупини його.' },
    barrel: { title: 'Гоблінська бочка', task: 'Бочка летить на ліву вежу. Збий гоблінів під час приземлення.' },
    balloon: { title: 'Куля над річкою', task: 'Куля летить на праву вежу. Зустрінь її, поки вона не почала бити.' },
    giant: { title: 'Гігант у правій лінії', task: 'Гігант перейшов міст. Захистися з мінімумом урону.' },
    xbow: { title: 'Ікс-лук біля річки', task: 'Суперник поставив Ікс-лук біля річки. Не дай йому розбити вежу.' },
    miner: { title: 'Шахтар біля вежі', task: 'Шахтар виліз біля лівої вежі. Прибери його дешевше.' },
    elixir: { title: '10 еліксиру', task: 'У тебе 10 еліксиру, суперник мовчить. Почни атаку правильно.' },
  },
  en: {
    hog: { title: 'Hog in the left lane', task: 'The Hog crossed the bridge. Defend without tower damage.' },
    mk: { title: 'Mega Knight at the bridge', task: 'The Mega Knight crossed the bridge toward your left tower. Stop it.' },
    barrel: { title: 'Goblin Barrel', task: 'A barrel flies at your left tower. Kill the goblins as they land.' },
    balloon: { title: 'Balloon over the river', task: 'A Balloon heads for your right tower. Meet it before it hits.' },
    giant: { title: 'Giant in the right lane', task: 'The Giant crossed the bridge. Defend with minimal damage.' },
    xbow: { title: 'X-Bow at the river', task: 'They placed an X-Bow at the river. Don\'t let it break your tower.' },
    miner: { title: 'Miner at the tower', task: 'A Miner popped up at your left tower. Deal with it cheaply.' },
    elixir: { title: '10 elixir', task: 'You have 10 elixir and the field is quiet. Start your push right.' },
  },
}

/** Задача про эликсир оценивается по месту, а не по урону. */
export const REASONS: Record<Lang, Record<string, string>> = {
  ru: {
    elixir_perfect: 'Голем из тыла — пока дойдёт, наберёшь эликсир на поддержку.',
    elixir_golem_mid: 'Неплохо, но из самого тыла дольше идти — успеешь собрать пуш.',
    elixir_golem_bridge: 'Голем у моста — соперник разменяет его до подхода поддержки.',
    elixir_hog: 'Тоже вариант: эликсир не сгорает. Но тяж из тыла даёт пуш сильнее.',
    elixir_musk: 'Сработало: эликсир не сгорает, а мушкетёр поддержит следующую атаку.',
    elixir_musk_front: 'Мушкетёр у моста — лёгкая цель для заклинания.',
    elixir_skel: '1 эликсир не решает: 9 так и копятся и сгорают.',
  },
  uk: {
    elixir_perfect: 'Голем із тилу — поки дійде, набереш еліксир на підтримку.',
    elixir_golem_mid: 'Непогано, але із самого тилу довше йти — встигнеш зібрати пуш.',
    elixir_golem_bridge: 'Голем біля мосту — суперник розміняє його до підходу підтримки.',
    elixir_hog: 'Теж варіант: еліксир не згорає. Але важкий із тилу дає пуш сильніший.',
    elixir_musk: 'Спрацювало: еліксир не згорає, а мушкетерка підтримає наступну атаку.',
    elixir_musk_front: 'Мушкетерка біля мосту — легка ціль для закляття.',
    elixir_skel: '1 еліксир не вирішує: 9 так і накопичуються й згорають.',
  },
  en: {
    elixir_perfect: 'Golem from the back — by the time it walks you have elixir for support.',
    elixir_golem_mid: 'Not bad, but from the very back it walks longer — more time to build the push.',
    elixir_golem_bridge: 'Golem at the bridge — they\'ll trade it before support arrives.',
    elixir_hog: 'Also an option: no elixir wasted. But a tank from the back makes a stronger push.',
    elixir_musk: 'It worked: no elixir wasted, and the Musketeer supports your next push.',
    elixir_musk_front: 'A Musketeer at the bridge is an easy spell target.',
    elixir_skel: '1 elixir doesn\'t solve it: 9 more keep piling up and burning.',
  },
}
/** Приём, который решает задачу, - показываем после боя. */
export const HINTS: Record<Lang, Record<string, string>> = {
  ru: {
    hog: 'Здание на пути Хога, лучше в центре в 3–4 клетках от реки: он свернёт к нему и погибнет под башнями.',
    mk: 'Мега-рыцаря держит Мини-ПЕККА: 720 урона за удар. Дешёвых юнитов он давит прыжком.',
    barrel: 'Бревно на свою башню: оно катится вперёд и сносит гоблинов при приземлении.',
    balloon: 'Шар летит и бьёт только здания: нужен тот, кто бьёт по воздуху, или здание в центре — увести его под обе башни.',
    giant: 'Гигант бьёт только здания: пушка в центре уводит его, а Мини-ПЕККА разбирает его до башни.',
    xbow: 'Икс-лук бьёт ближайшего: здание у реки забирает огонь на себя и само достаёт до лука.',
    miner: 'Шахтёр бьёт ближайшего: юнит вплотную забирает удары на себя, башня добивает.',
  },
  uk: {
    hog: 'Будівля на шляху Хога, краще в центрі за 3–4 клітинки від річки: він зверне до неї й загине під вежами.',
    mk: 'Мега-лицаря тримає Міні-ПЕККА: 720 урону за удар. Дешевих юнітів він тисне стрибком.',
    barrel: 'Колода на свою вежу: вона котиться вперед і зносить гоблінів під час приземлення.',
    balloon: 'Куля летить і б\'є лише будівлі: потрібен той, хто б\'є по повітрю, або будівля в центрі — відвести її під обидві вежі.',
    giant: 'Гігант б\'є лише будівлі: гармата в центрі відводить його, а Міні-ПЕККА розбирає його до вежі.',
    xbow: 'Ікс-лук б\'є найближчого: будівля біля річки забирає вогонь на себе й сама дістає до лука.',
    miner: 'Шахтар б\'є найближчого: юніт впритул забирає удари на себе, вежа добиває.',
  },
  en: {
    hog: 'A building in the Hog\'s path, best in the middle 3–4 tiles from the river: it turns to it and dies under your towers.',
    mk: 'Mini PEKKA holds the Mega Knight: 720 damage per hit. Cheap units get crushed by its jump.',
    barrel: 'Log onto your own tower: it rolls forward and sweeps the goblins as they land.',
    balloon: 'The Balloon flies and only hits buildings: use something that hits air, or a middle building to pull it under both towers.',
    giant: 'The Giant only hits buildings: a middle Cannon pulls it, and Mini PEKKA takes it down before it reaches the tower.',
    xbow: 'The X-Bow hits the closest target: a building at the river takes its fire and reaches the X-Bow itself.',
    miner: 'The Miner hits the closest target: a unit right next to it takes the hits, the tower finishes it.',
  },
}

export interface ResultText {
  tower0: string; tower: string; base: string; stopped: string; notStopped: string; trade: string
  onlyBuildings: string; noAir: string; missed: string; weak: string; lost: string
}

export const RESULT_TEXT: Record<Lang, ResultText> = {
  ru: {
    tower0: 'Башня цела', tower: 'Башня −{n}', base: 'без защиты: −{n}',
    stopped: 'Атака отбита за {s} с', notStopped: 'Атака не отбита', trade: '{me} против {en} эликсира',
    onlyBuildings: '{e} бьёт только здания и на войска не отвлекается.',
    noAir: 'Эта карта не бьёт по воздуху — {e} летит мимо.',
    missed: 'Заклинание прилетело, когда там ещё никого не было.',
    weak: 'Заклинание сняло {n} из {hp} здоровья — мало, чтобы убить.',
    lost: 'Твоя карта не пережила бой.',
  },
  uk: {
    tower0: 'Вежа ціла', tower: 'Вежа −{n}', base: 'без захисту: −{n}',
    stopped: 'Атаку відбито за {s} с', notStopped: 'Атаку не відбито', trade: '{me} проти {en} еліксиру',
    onlyBuildings: '{e} б\'є лише будівлі й на війська не відволікається.',
    noAir: 'Ця карта не б\'є по повітрю — {e} летить повз.',
    missed: 'Закляття прилетіло, коли там ще нікого не було.',
    weak: 'Закляття зняло {n} з {hp} здоров\'я — замало, щоб убити.',
    lost: 'Твоя карта не пережила бій.',
  },
  en: {
    tower0: 'Tower untouched', tower: 'Tower −{n}', base: 'undefended: −{n}',
    stopped: 'Attack stopped in {s}s', notStopped: 'Attack not stopped', trade: '{me} vs {en} elixir',
    onlyBuildings: '{e} only hits buildings and ignores troops.',
    noAir: 'This card can\'t hit air — {e} flies past.',
    missed: 'The spell landed before anyone was there.',
    weak: 'The spell dealt {n} of {hp} HP — not enough to kill.',
    lost: 'Your card didn\'t survive the fight.',
  },
}

/** Почему так вышло - из того, что произошло в бою и из свойств карт. */
export function explain(p: Puzzle, o: Outcome, card: string, dealt: number, lang: Lang): string[] {
  const tx = RESULT_TEXT[lang]
  const n = (c: string) => CARD_NAMES[lang][c] ?? c
  const foe = p.enemyCard === 'Goblin Barrel' ? 'Goblins' : p.enemyCard
  const fs = UNITS[foe]
  const kind = cardInfo(card).kind
  const lines: string[] = []
  if (o.grade !== 'perfect') {
    if (kind === 'spell' && dealt === 0) lines.push(tx.missed)
    else if (kind === 'spell' && !o.killed && fs) lines.push(tx.weak.replace('{n}', String(Math.round(dealt))).replace('{hp}', String(fs.hp)))
    else if (fs?.flying && kind !== 'spell' && !UNITS[card]?.air) lines.push(tx.noAir.replace('{e}', n(foe)))
    else if (fs?.buildingsOnly && kind === 'troop' && o.dmg > p.sc.limits[0]) lines.push(tx.onlyBuildings.replace('{e}', n(foe)))
    if (kind !== 'spell' && !o.survived && lines.length === 0) lines.push(tx.lost)
  }
  return lines
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
