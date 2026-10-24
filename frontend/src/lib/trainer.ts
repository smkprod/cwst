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

const SPELL_CARDS = new Set(['Fireball', 'The Log', 'Goblin Barrel', 'Tornado'])

export function cardInfo(card: string): HandCard {
  const kind: CardKind = SPELL_CARDS.has(card) ? 'spell' : UNITS[card]?.building ? 'building' : 'troop'
  return { card, cost: COST[card] ?? 0, kind }
}

export const CARD_NAMES: Record<Lang, Record<string, string>> = {
  ru: { 'Cannon': 'Пушка', 'Knight': 'Рыцарь', 'Skeletons': 'Скелеты', 'Fireball': 'Огн. шар', 'The Log': 'Бревно', 'Musketeer': 'Мушкетёр',
    'Mini P.E.K.K.A': 'Мини-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големит', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-рыцарь', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гоблины', 'Balloon': 'Шар', 'Giant': 'Гигант', 'X-Bow': 'Икс-лук', 'Miner': 'Шахтёр', 'Tornado': 'Торнадо', 'Mortar': 'Мортира', 'P.E.K.K.A': 'ПЕККА', 'Ice Golem': 'Лед. голем', 'Ice Spirit': 'Лед. дух' },
  uk: { 'Cannon': 'Гармата', 'Knight': 'Лицар', 'Skeletons': 'Скелети', 'Fireball': 'Вогн. куля', 'The Log': 'Колода', 'Musketeer': 'Мушкетерка',
    'Mini P.E.K.K.A': 'Міні-ПЕККА', 'Golem': 'Голем', 'Golemite': 'Големіт', 'Hog Rider': 'Хог', 'Mega Knight': 'Мега-лицар', 'Goblin Barrel': 'Бочка',
    'Goblins': 'Гобліни', 'Balloon': 'Куля', 'Giant': 'Гігант', 'X-Bow': 'Ікс-лук', 'Miner': 'Шахтар', 'Tornado': 'Торнадо', 'Mortar': 'Мортира', 'P.E.K.K.A': 'ПЕККА', 'Ice Golem': 'Криж. голем', 'Ice Spirit': 'Криж. дух' },
  en: { 'Cannon': 'Cannon', 'Knight': 'Knight', 'Skeletons': 'Skeletons', 'Fireball': 'Fireball', 'The Log': 'Log', 'Musketeer': 'Musketeer',
    'Mini P.E.K.K.A': 'Mini PEKKA', 'Golem': 'Golem', 'Golemite': 'Golemite', 'Hog Rider': 'Hog', 'Mega Knight': 'Mega Knight', 'Goblin Barrel': 'Barrel',
    'Goblins': 'Goblins', 'Balloon': 'Balloon', 'Giant': 'Giant', 'X-Bow': 'X-Bow', 'Miner': 'Miner', 'Tornado': 'Tornado', 'Mortar': 'Mortar', 'P.E.K.K.A': 'P.E.K.K.A', 'Ice Golem': 'Ice Golem', 'Ice Spirit': 'Ice Spirit' },
}

export interface Puzzle {
  id: string
  tip: TipId
  /** Чем атакует соперник - для иконки и объяснений. */
  enemyCard: string
  /** Карта на плитке задачи, если не карта соперника (задача про приём). */
  icon?: string
  hand: string[]
  sc: Scenario
  /** Эталонный ход на три звезды - его показываем перед попыткой (проверен прогоном боя). */
  solution: { card: string; x: number; y: number }
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
    solution: { card: 'Cannon', x: 9, y: 22 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 3.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [100, 400] },
  },
  {
    // Торнадо на вторую клетку перед королём со сдвигом к башне принцессы, когда Хог подходит к ней:
    // Хога затягивает в радиус короля - король просыпается и вместе с принцессой сбивает его
    id: 'tornado', tip: 'king_tornado', enemyCard: 'Hog Rider', icon: 'Tornado', hand: ['Tornado', 'Cannon', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 7.5, y: 26 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 3.5, y: 14 }], pause: reach('Hog Rider', 21.5), horizon: 14, limits: [350, 700], goal: 'king' },
  },
  {
    id: 'mk', tip: 'kite', enemyCard: 'Mega Knight', hand: ['Skeletons', 'Mini P.E.K.K.A', 'Musketeer', 'Fireball'],
    solution: { card: 'Mini P.E.K.K.A', x: 3.5, y: 18.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mega Knight', x: 3.5, y: 13.5 }], pause: reach('Mega Knight', 17.6), horizon: 16, limits: [100, 600] },
  },
  {
    // Хог с самого края моста обходит пушку в «4-3» (центр); пушка на клетку ближе к башне - «4-2» - его тянет
    id: 'hog_edge', tip: 'anti_hog', enemyCard: 'Hog Rider', hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Cannon', x: 7, y: 21 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'Hog Rider', x: 0.5, y: 14 }], pause: reach('Hog Rider', 17.2), horizon: 14, limits: [100, 400] },
  },
  {
    // Шар с края обходит центральное здание; пушка правее центра у реки перехватывает его
    id: 'balloon_edge', tip: 'anti_air', enemyCard: 'Balloon', hand: ['Cannon', 'Knight', 'Skeletons', 'Fireball'],
    solution: { card: 'Cannon', x: 10, y: 21 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Balloon', x: 17.5, y: 13.5 }], pause: reach('Balloon', 18.5), horizon: 14, limits: [100, 450] },
  },
  {
    // ПЕККА сносит башню целиком; Ледяной голем у середины реки, когда она перешла мост, уводит её в другую линию
    id: 'pekka_kite', tip: 'kite', enemyCard: 'P.E.K.K.A', icon: 'Ice Golem', hand: ['Ice Golem', 'Knight', 'Skeletons', 'Mini P.E.K.K.A'],
    solution: { card: 'Ice Golem', x: 9.5, y: 18.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'P.E.K.K.A', x: 3.5, y: 13.5 }], pause: reach('P.E.K.K.A', 17.5), horizon: 30, limits: [100, 1200] },
  },
  {
    id: 'giant', tip: 'anti_tank', enemyCard: 'Giant', hand: ['Cannon', 'Mini P.E.K.K.A', 'Skeletons', 'Fireball'],
    solution: { card: 'Cannon', x: 9, y: 22 },
    sc: { elixir: 7, enemy: [{ at: 0, card: 'Giant', x: 14.5, y: 13.5 }], pause: reach('Giant', 17.4), horizon: 18, limits: [100, 600] },
  },
  {
    id: 'xbow', tip: 'anti_siege', enemyCard: 'X-Bow', hand: ['Knight', 'Fireball', 'Skeletons', 'Cannon'],
    solution: { card: 'Cannon', x: 14, y: 18 },
    sc: { elixir: 6, enemy: [{ at: 0, card: 'X-Bow', x: 14.5, y: 13 }], pause: 0.6, horizon: 16, limits: [150, 450] },
  },
  {
    // Мортира не стреляет ближе 3,5 клетки: юнит вплотную ломает её, а она бьёт только по башне
    id: 'mortar', tip: 'anti_siege', enemyCard: 'Mortar', hand: ['Knight', 'Skeletons', 'Fireball', 'Cannon'],
    solution: { card: 'Knight', x: 14.5, y: 17.5 },
    sc: { elixir: 5, enemy: [{ at: 0, card: 'Mortar', x: 14.5, y: 12.5 }], pause: 0.6, horizon: 18, limits: [300, 600] },
  },
  {
    // Бочка в центр правой башни; Торнадо между башней и королём за полсекунды до приземления
    // затягивает гоблинов в радиус короля: король просыпается, башня цела
    id: 'barrel_tornado', tip: 'king_tornado', enemyCard: 'Goblin Barrel', hand: ['Tornado', 'The Log', 'Skeletons', 'Fireball'],
    solution: { card: 'Tornado', x: 11.5, y: 26.5 },
    sc: { elixir: 4, enemy: [{ at: 0, card: 'Goblin Barrel', x: 14.5, y: 25.5 }], pause: flown(0.86), horizon: 10, limits: [60, 130], goal: 'king' },
  },
]

/** Сколько задач открыто целиком без Плюса (плюс задача дня); в остальных бесплатно только показ решения. */
export const FREE_PUZZLES = 1

const SANDBOX_KEY = 'cwst_sandbox_day'

/** Бесплатная песочница - раз в день: был ли уже сегодня запуск. */
export function sandboxUsedToday(now = new Date()): boolean {
  try { return localStorage.getItem(SANDBOX_KEY) === now.toISOString().slice(0, 10) } catch { return false }
}

export function markSandboxUsed(now = new Date()) {
  try { localStorage.setItem(SANDBOX_KEY, now.toISOString().slice(0, 10)) } catch { /* без памяти - не считаем */ }
}

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
    hog_edge: { title: 'Хог с края моста', task: 'Хог прыгает с самого края. Пушка в центре его не дотянет — найди клетку, которая дотянет.' },
    balloon_edge: { title: 'Шар с края', task: 'Шар летит по самому краю. Перехвати его зданием, пока он не долетел до башни.' },
    pekka_kite: { title: 'ПЕККА: уведи в другую линию', task: 'ПЕККА перешла мост — без защиты она снесёт башню. Уведи её туда, где её добьют.' },
    mortar: { title: 'Мортира у моста', task: 'Соперник поставил Мортиру. Сломай её, не подставляя свои карты под её выстрелы.' },
    giant: { title: 'Гигант в правой линии', task: 'Гигант перешёл мост. Защитись с минимумом урона.' },
    xbow: { title: 'Икс-лук у реки', task: 'Соперник поставил Икс-лук у реки. Не дай ему разбить башню.' },
    tornado: { title: 'Торнадо будит короля', task: 'Хог подходит к левой башне. Разбуди короля Торнадо — пусть башни вдвоём собьют Хога.' },
    barrel_tornado: { title: 'Бочка: Торнадо к королю', task: 'Бочка летит на правую башню. Затяни гоблинов к королю Торнадо — разбуди его.' },
  },
  uk: {
    hog: { title: 'Хог у лівій лінії', task: 'Хог перейшов міст. Відбийся так, щоб вежа не постраждала.' },
    mk: { title: 'Мега-лицар біля мосту', task: 'Мега-лицар перейшов міст і йде на ліву вежу. Зупини його.' },
    hog_edge: { title: 'Хог з краю мосту', task: 'Хог стрибає з самого краю. Гармата в центрі його не дотягне — знайди клітинку, яка дотягне.' },
    balloon_edge: { title: 'Куля з краю', task: 'Куля летить самим краєм. Перехопи її будівлею, поки вона не долетіла до вежі.' },
    pekka_kite: { title: 'ПЕККА: відведи в іншу лінію', task: 'ПЕККА перейшла міст — без захисту вона знесе вежу. Відведи її туди, де її доб\'ють.' },
    mortar: { title: 'Мортира біля мосту', task: 'Суперник поставив Мортиру. Злам її, не підставляючи свої карти під її постріли.' },
    giant: { title: 'Гігант у правій лінії', task: 'Гігант перейшов міст. Захистися з мінімумом урону.' },
    xbow: { title: 'Ікс-лук біля річки', task: 'Суперник поставив Ікс-лук біля річки. Не дай йому розбити вежу.' },
    tornado: { title: 'Торнадо будить короля', task: 'Хог підходить до лівої вежі. Розбуди короля Торнадо — хай вежі вдвох зіб\'ють Хога.' },
    barrel_tornado: { title: 'Бочка: Торнадо до короля', task: 'Бочка летить на праву вежу. Затягни гоблінів до короля Торнадо — розбуди його.' },
  },
  en: {
    hog: { title: 'Hog in the left lane', task: 'The Hog crossed the bridge. Defend without tower damage.' },
    mk: { title: 'Mega Knight at the bridge', task: 'The Mega Knight crossed the bridge toward your left tower. Stop it.' },
    hog_edge: { title: 'Hog from the edge', task: 'The Hog jumps from the very edge. A middle Cannon won\'t pull it — find the tile that will.' },
    balloon_edge: { title: 'Balloon on the edge', task: 'The Balloon flies along the edge. Catch it with a building before it reaches your tower.' },
    pekka_kite: { title: 'P.E.K.K.A: pull to the other lane', task: 'P.E.K.K.A crossed the bridge — undefended it takes your tower. Pull it where it gets finished.' },
    mortar: { title: 'Mortar at the bridge', task: 'They placed a Mortar. Break it without putting your cards under its shots.' },
    giant: { title: 'Giant in the right lane', task: 'The Giant crossed the bridge. Defend with minimal damage.' },
    xbow: { title: 'X-Bow at the river', task: 'They placed an X-Bow at the river. Don\'t let it break your tower.' },
    tornado: { title: 'Tornado wakes the King', task: 'The Hog nears your left tower. Wake your King with Tornado — let both towers take the Hog down.' },
    barrel_tornado: { title: 'Barrel: Tornado to the King', task: 'A barrel flies at your right tower. Pull the goblins to your King with Tornado — wake him up.' },
  },
}

/** Приём, который решает задачу, - показываем после боя. */
export const HINTS: Record<Lang, Record<string, string>> = {
  ru: {
    hog: 'Здание на пути Хога, лучше в центре в 3–4 клетках от реки: он свернёт к нему и погибнет под башнями.',
    mk: 'Мега-рыцаря держит Мини-ПЕККА: 720 урона за удар. Дешёвых юнитов он давит прыжком.',
    hog_edge: 'Хог с края обходит пушку в «4-3» (центр, 4 клетки от реки). Сдвинь её на клетку к башне — «4-2»: так она в обзоре Хога с любого края.',
    balloon_edge: 'Шар с края не видит здание в центре. Ставь пушку ближе к его краю и к реке — правее центра на 3–4 клетки ниже реки.',
    pekka_kite: 'Ледяной голем бьёт только здания и идёт по своей линии, а ПЕККА бьёт ближайшего и идёт за ним. Ставь его у середины реки, на клетку в другую линию, ровно когда ПЕККА перешла мост.',
    mortar: 'Мортира не стреляет ближе 3,5 клетки. Юнит, дошедший до неё вплотную, ломает её без потерь — она бьёт только по башне.',
    giant: 'Гигант бьёт только здания: пушка в центре уводит его, а Мини-ПЕККА разбирает его до башни.',
    xbow: 'Икс-лук бьёт ближайшего: здание у реки забирает огонь на себя и само достаёт до лука.',
    tornado: 'Когда Хог подходит к башне, кидай Торнадо на вторую клетку перед королём, на две клетки к этой башне: Хога затянет вплотную к королю, и король проснётся до конца боя.',
    barrel_tornado: 'Торнадо между башней и королём — за полсекунды до приземления бочки: гоблинов затянет вплотную к королю, король проснётся, а башня останется целой. Раньше нельзя — вихрь кончится до приземления.',
  },
  uk: {
    hog: 'Будівля на шляху Хога, краще в центрі за 3–4 клітинки від річки: він зверне до неї й загине під вежами.',
    mk: 'Мега-лицаря тримає Міні-ПЕККА: 720 урону за удар. Дешевих юнітів він тисне стрибком.',
    hog_edge: 'Хог з краю обходить гармату в «4-3» (центр, 4 клітинки від річки). Зсунь її на клітинку до вежі — «4-2»: так вона в огляді Хога з будь-якого краю.',
    balloon_edge: 'Куля з краю не бачить будівлю в центрі. Став гармату ближче до її краю й до річки — правіше від центру на 3–4 клітинки нижче річки.',
    pekka_kite: 'Крижаний голем б\'є лише будівлі й іде своєю лінією, а ПЕККА б\'є найближчого й іде за ним. Став його біля середини річки, на клітинку в іншу лінію, саме коли ПЕККА перейшла міст.',
    mortar: 'Мортира не стріляє ближче 3,5 клітинки. Юніт, що дійшов до неї впритул, ламає її без втрат — вона б\'є лише по вежі.',
    giant: 'Гігант б\'є лише будівлі: гармата в центрі відводить його, а Міні-ПЕККА розбирає його до вежі.',
    xbow: 'Ікс-лук б\'є найближчого: будівля біля річки забирає вогонь на себе й сама дістає до лука.',
    tornado: 'Коли Хог підходить до вежі, кидай Торнадо на другу клітинку перед королем, на дві клітинки до цієї вежі: Хога затягне впритул до короля, і король прокинеться до кінця бою.',
    barrel_tornado: 'Торнадо між вежею й королем — за пів секунди до приземлення бочки: гоблінів затягне впритул до короля, король прокинеться, а вежа залишиться цілою. Раніше не можна — вихор скінчиться до приземлення.',
  },
  en: {
    hog: 'A building in the Hog\'s path, best in the middle 3–4 tiles from the river: it turns to it and dies under your towers.',
    mk: 'Mini PEKKA holds the Mega Knight: 720 damage per hit. Cheap units get crushed by its jump.',
    hog_edge: 'An edge Hog bypasses a 4-3 Cannon (middle, 4 tiles from the river). Shift it one tile toward the tower — 4-2: then it\'s in the Hog\'s sight from any edge.',
    balloon_edge: 'An edge Balloon doesn\'t see a middle building. Place the Cannon closer to its side and to the river — right of center, 3–4 tiles below the river.',
    pekka_kite: 'Ice Golem only hits buildings and walks its own lane, while P.E.K.K.A hits the closest target and follows it. Drop it at the middle of the river, one tile into the other lane, right as P.E.K.K.A crosses.',
    mortar: 'The Mortar can\'t shoot closer than 3.5 tiles. A unit that walks right up to it breaks it for free — it only hits your tower.',
    giant: 'The Giant only hits buildings: a middle Cannon pulls it, and Mini PEKKA takes it down before it reaches the tower.',
    xbow: 'The X-Bow hits the closest target: a building at the river takes its fire and reaches the X-Bow itself.',
    tornado: 'As the Hog nears your tower, drop Tornado on the second tile in front of your King, two tiles toward that tower: the Hog gets pulled right up to your King and he wakes for the rest of the match.',
    barrel_tornado: 'Tornado between the tower and your King, half a second before the barrel lands: the goblins get pulled right up to your King, he wakes up and the tower stays clean. Not earlier — the Tornado ends before they land.',
  },
}

export interface ResultText {
  tower0: string; tower: string; base: string; stopped: string; notStopped: string; trade: string
  onlyBuildings: string; noAir: string; missed: string; weak: string; lost: string
  kingAwake: string; kingAsleep: string; kingHint: string
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
    kingAwake: 'Король проснулся', kingAsleep: 'Король спит',
    kingHint: 'Король так и не проснулся: врага нужно затянуть вплотную к королевской башне — Торнадо ближе к королю и в момент, когда враг уже у башни.',
  },
  uk: {
    tower0: 'Вежа ціла', tower: 'Вежа −{n}', base: 'без захисту: −{n}',
    stopped: 'Атаку відбито за {s} с', notStopped: 'Атаку не відбито', trade: '{me} проти {en} еліксиру',
    onlyBuildings: '{e} б\'є лише будівлі й на війська не відволікається.',
    noAir: 'Ця карта не б\'є по повітрю — {e} летить повз.',
    missed: 'Закляття прилетіло, коли там ще нікого не було.',
    weak: 'Закляття зняло {n} з {hp} здоров\'я — замало, щоб убити.',
    lost: 'Твоя карта не пережила бій.',
    kingAwake: 'Король прокинувся', kingAsleep: 'Король спить',
    kingHint: 'Король так і не прокинувся: ворога треба затягнути впритул до королівської вежі — Торнадо ближче до короля й у момент, коли ворог уже біля вежі.',
  },
  en: {
    tower0: 'Tower untouched', tower: 'Tower −{n}', base: 'undefended: −{n}',
    stopped: 'Attack stopped in {s}s', notStopped: 'Attack not stopped', trade: '{me} vs {en} elixir',
    onlyBuildings: '{e} only hits buildings and ignores troops.',
    noAir: 'This card can\'t hit air — {e} flies past.',
    missed: 'The spell landed before anyone was there.',
    weak: 'The spell dealt {n} of {hp} HP — not enough to kill.',
    lost: 'Your card didn\'t survive the fight.',
    kingAwake: 'King is awake', kingAsleep: 'King is asleep',
    kingHint: 'Your King never woke up: pull the attacker right up to your King Tower — Tornado closer to the King, right when they reach your tower.',
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
  if (p.sc.goal === 'king' && !o.kingAwake) lines.push(tx.kingHint)
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
