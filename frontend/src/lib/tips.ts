import type { Lang } from './i18n'
import type { Scene } from '../components/ArenaAnim'

/**
 * Библиотека фишек: приём, когда он нужен и как его сделать - с анимацией на схеме
 * арены. Вывод профиля «что не так» ведёт сюда же: проигрываешь Хогу - смотри, куда
 * ставить пушку.
 *
 * Клетки на схемах - ориентир, а не миллиметр: арена 18 клеток в ширину, река на
 * 15-16 ряду, своя принцесса - центр ряда 25. Точное место зависит от колоды, но
 * принцип - «в центр, 4 клетки от реки» - один.
 */

export type TipId = 'anti_hog' | 'anti_tank' | 'kite' | 'log_barrel' | 'anti_air' | 'anti_siege' | 'anti_miner'
  | 'elixir' | 'tilt' | 'levels' | 'deck'

export const TIP_IDS: TipId[] = ['anti_hog', 'kite', 'log_barrel', 'anti_tank', 'anti_air', 'anti_siege', 'anti_miner', 'elixir', 'tilt', 'levels', 'deck']

/** Фишки, у которых есть схема арены. Остальные - советы без поля. */
export const ARENA_TIPS: TipId[] = ['anti_hog', 'kite', 'log_barrel', 'anti_tank', 'anti_air', 'anti_siege', 'anti_miner', 'elixir']

type Units = Record<'hog' | 'cannon' | 'mk' | 'skeletons' | 'barrel' | 'log' | 'goblins' | 'giant' | 'minipekka'
  | 'counter' | 'balloon' | 'musk' | 'xbow' | 'knight' | 'miner' | 'golem' | 'here' | 'cells4', string>

export interface TipText { title: string; when: string; steps: string[] }

const UNITS: Record<Lang, Units> = {
  ru: { hog: 'Хог', cannon: 'Пушка', mk: 'Мега-рыцарь', skeletons: 'Скелеты', barrel: 'Бочка', log: 'Бревно', goblins: 'Гоблины',
    giant: 'Гигант', minipekka: 'Мини-ПЕККА', counter: 'Контратака', balloon: 'Шар', musk: 'Мушкетёр', xbow: 'Икс-лук',
    knight: 'Рыцарь', miner: 'Шахтёр', golem: 'Голем', here: 'сюда', cells4: '4 клетки' },
  uk: { hog: 'Хог', cannon: 'Гармата', mk: 'Мега-лицар', skeletons: 'Скелети', barrel: 'Бочка', log: 'Колода', goblins: 'Гобліни',
    giant: 'Гігант', minipekka: 'Міні-ПЕККА', counter: 'Контратака', balloon: 'Куля', musk: 'Мушкетерка', xbow: 'Ікс-лук',
    knight: 'Лицар', miner: 'Шахтар', golem: 'Голем', here: 'сюди', cells4: '4 клітинки' },
  en: { hog: 'Hog', cannon: 'Cannon', mk: 'Mega Knight', skeletons: 'Skeletons', barrel: 'Barrel', log: 'Log', goblins: 'Goblins',
    giant: 'Giant', minipekka: 'Mini P.E.K.K.A', counter: 'Counter-push', balloon: 'Balloon', musk: 'Musketeer', xbow: 'X-Bow',
    knight: 'Knight', miner: 'Miner', golem: 'Golem', here: 'here', cells4: '4 tiles' },
}

/** Сцены: (подписи юнитов на языке) → таймлайн. Клетки - как в игре, см. ArenaAnim. */
const SCENES: Partial<Record<TipId, (u: Units) => Scene>> = {
  // Пушка 2×2 в центр: верхний край в 4 клетках ниже реки (река кончается на 17)
  anti_hog: u => ({
    duration: 6.5,
    marks: [{ x: 9, y: 22, size: 2, from: 0.4, to: 1.5, label: u.here }],
    rulers: [{ x: 11.6, y1: 17, y2: 21, label: u.cells4, from: 0.4, to: 3.2 }],
    actors: [
      { id: 'hog', kind: 'enemy', card: 'Hog Rider', label: u.hog, r: 0.75, to: 4.6,
        keys: [{ t: 0, x: 3.5, y: 11.5 }, { t: 1.1, x: 3.5, y: 17.3 }, { t: 1.9, x: 5.7, y: 19.4 }, { t: 2.6, x: 7.4, y: 21 }, { t: 4.6, x: 7.4, y: 21 }] },
      { id: 'cannon', kind: 'building', card: 'Cannon', label: u.cannon, r: 0.95, keys: [{ t: 1.35, x: 9, y: 22 }, { t: 6.5, x: 9, y: 22 }] },
    ],
  }),
  // Дешёвый юнит в центр между башнями: тяж идёт к нему под огнём обеих принцесс
  kite: u => ({
    duration: 7,
    marks: [{ x: 9, y: 24.5, from: 0.6, to: 1.4, label: u.here }],
    actors: [
      { id: 'mk', kind: 'enemy', card: 'Mega Knight', label: u.mk, r: 1.0, to: 5.4,
        keys: [{ t: 0, x: 3.5, y: 12.5 }, { t: 1.1, x: 3.5, y: 18.6 }, { t: 2.8, x: 7.6, y: 23.4 }, { t: 5.4, x: 8, y: 23.6 }] },
      { id: 'sk', kind: 'mine', card: 'Skeletons', label: u.skeletons, r: 0.55, to: 3.1, keys: [{ t: 1.25, x: 9, y: 24.5 }, { t: 3.1, x: 9, y: 24.5 }] },
    ],
  }),
  // Бревно катится вперёд, к сопернику: кидаем на свою башню, и оно сметает приземлившихся
  log_barrel: u => ({
    duration: 5.5,
    noShots: true,
    marks: [{ x: 3.5, y: 26.5, from: 0.7, to: 1.25, label: u.here }],
    actors: [
      { id: 'barrel', kind: 'barrel', card: 'Goblin Barrel', label: u.barrel, keys: [{ t: 0, x: 9, y: 11.5 }, { t: 1.3, x: 3.6, y: 23.4 }] },
      { id: 'g1', kind: 'enemy', card: 'Goblins', label: '', r: 0.45, to: 1.7, keys: [{ t: 1.3, x: 2.6, y: 22.9 }, { t: 1.7, x: 2.6, y: 22.7 }] },
      { id: 'g2', kind: 'enemy', card: 'Goblins', label: u.goblins, r: 0.45, to: 1.75, keys: [{ t: 1.3, x: 3.6, y: 22.6 }, { t: 1.75, x: 3.6, y: 22.4 }] },
      { id: 'g3', kind: 'enemy', card: 'Goblins', label: '', r: 0.45, to: 1.8, keys: [{ t: 1.3, x: 4.6, y: 22.9 }, { t: 1.8, x: 4.6, y: 22.7 }] },
      { id: 'log', kind: 'log', card: 'The Log', label: u.log, r: 1.95, keys: [{ t: 1.15, x: 3.5, y: 26.5 }, { t: 2.3, x: 3.5, y: 16.5 }] },
    ],
  }),
  // Гигант бьёт только здания: пушка в центр тянет его, Мини-ПЕККА сзади, потом - в другую линию
  anti_tank: u => ({
    duration: 8.5,
    marks: [{ x: 9, y: 22, size: 2, from: 1.4, to: 2.4, label: u.here }],
    actors: [
      { id: 'giant', kind: 'enemy', card: 'Giant', label: u.giant, r: 1.05, to: 5.8,
        keys: [{ t: 0, x: 14.5, y: 11.5 }, { t: 2.3, x: 14.5, y: 17.6 }, { t: 3.8, x: 10.7, y: 21.2 }, { t: 5.8, x: 10.7, y: 21.2 }] },
      { id: 'cannon', kind: 'building', card: 'Cannon', label: u.cannon, r: 0.95, keys: [{ t: 2.2, x: 9, y: 22 }, { t: 6.2, x: 9, y: 22 }] },
      { id: 'mp', kind: 'mine', card: 'Mini P.E.K.K.A', label: u.minipekka, r: 0.65, keys: [{ t: 3.5, x: 12, y: 24 }, { t: 4.4, x: 11.6, y: 22.3 }, { t: 5.8, x: 11.6, y: 22.3 }] },
      { id: 'cp', kind: 'mine', card: 'Mini P.E.K.K.A', label: u.counter, r: 0.65, keys: [{ t: 6.0, x: 3.5, y: 21 }, { t: 8.5, x: 3.5, y: 11.5 }] },
    ],
  }),
  // Шар бьёт только здания: мушкетёр за башней достаёт его, пока он ломает башню
  anti_air: u => ({
    duration: 6.5,
    marks: [{ x: 14.5, y: 28.5, from: 0.7, to: 1.5, label: u.here }],
    actors: [
      { id: 'ball', kind: 'enemy', card: 'Balloon', label: u.balloon, r: 0.9, to: 4.6,
        keys: [{ t: 0, x: 14.5, y: 11.5 }, { t: 2.5, x: 14.5, y: 20 }, { t: 3.4, x: 14.5, y: 22.8 }, { t: 4.6, x: 14.5, y: 22.9 }] },
      { id: 'musk', kind: 'mine', card: 'Musketeer', label: u.musk, r: 0.65, keys: [{ t: 1.3, x: 14.5, y: 28.6 }, { t: 6.5, x: 14.5, y: 28.6 }] },
    ],
  }),
  // Икс-лук у реки на чужой стороне: танк на мост перед ним, сразу
  anti_siege: u => ({
    duration: 6.5,
    noShots: true,
    marks: [{ x: 14.5, y: 16, from: 0.6, to: 1.4, label: u.here }],
    actors: [
      { id: 'xbow', kind: 'enemy', card: 'X-Bow', label: u.xbow, r: 0.9, to: 4.4, keys: [{ t: 0, x: 14.5, y: 12.8 }, { t: 4.4, x: 14.5, y: 12.8 }] },
      { id: 'kn', kind: 'mine', card: 'Knight', label: u.knight, r: 0.7, keys: [{ t: 1.3, x: 14.5, y: 16 }, { t: 2.4, x: 14.5, y: 14.2 }, { t: 6.5, x: 14.5, y: 14.2 }] },
    ],
  }),
  // Шахтёр вылезает у башни: дешёвый юнит вплотную
  anti_miner: u => ({
    duration: 5.5,
    marks: [{ x: 3.5, y: 22.4, from: 0.7, to: 1.4, label: u.here }],
    actors: [
      { id: 'dig', kind: 'spell', label: '', r: 0.3, to: 1.0, keys: [{ t: 0, x: 3.5, y: 11.5 }, { t: 1.0, x: 3.5, y: 23.3 }] },
      { id: 'miner', kind: 'enemy', card: 'Miner', label: u.miner, r: 0.7, to: 3.4, keys: [{ t: 1.0, x: 3.5, y: 23.4 }, { t: 3.4, x: 3.5, y: 23.4 }] },
      { id: 'sk', kind: 'mine', card: 'Skeletons', label: u.skeletons, r: 0.5, to: 3.5, keys: [{ t: 1.3, x: 3.5, y: 22.2 }, { t: 3.5, x: 3.5, y: 22.5 }] },
    ],
  }),
  // За королём есть ряд: тяж оттуда идёт к мосту, а эликсир копится на поддержку (сбоку от короля - чтобы обойти его по своей линии)
  elixir: u => ({
    duration: 6.5,
    noShots: true,
    marks: [{ x: 12, y: 31.5, from: 0.4, to: 1.2, label: u.here }],
    actors: [
      { id: 'golem', kind: 'mine', card: 'Golem', label: u.golem, r: 0.85, keys: [{ t: 1.0, x: 12, y: 31.4 }, { t: 2.4, x: 14.5, y: 28.6 }, { t: 6.5, x: 14.5, y: 17.5 }] },
    ],
  }),
}

const TEXT: Record<Lang, Record<TipId, TipText>> = {
  ru: {
    anti_hog: { title: 'Хог: здание в центр', when: 'Против Хога, Таранов и Рамы — всех, кто бьёт только по зданиям.',
      steps: ['Дождись, пока Хог перейдёт мост — раньше не ставь, иначе его пустят в другую линию.',
        'Ставь пушку или теслу в центр, примерно в 4 клетках ниже реки.',
        'Хог свернёт к зданию и встанет бить его под огнём башен — до твоей башни он не дойдёт.'] },
    kite: { title: 'Увести тяжа в центр', when: 'Против Мега-рыцаря, ПЕККИ, Принца — тех, кто бьёт ближайшего.',
      steps: ['Тяж перешёл мост и идёт на башню.',
        'Брось дешёвого юнита (скелетов, ледяного духа) в центр между башнями.',
        'Тяж свернёт к нему и пойдёт через центр под огнём двух башен, а ты добьёшь его без потерь.'] },
    log_barrel: { title: 'Бревно по бочке', when: 'Против Гоблинской бочки и Бочки со скелетами.',
      steps: ['Бочку видно в полёте — не паникуй и не кидай раньше времени.',
        'Как только бочка подлетает к башне, кидай бревно прямо на свою башню — оно покатится вперёд.',
        'Бревно сметает гоблинов в момент приземления — башня даже не получает урон.'] },
    anti_tank: { title: 'Гигант: не защищайся у моста', when: 'Против Гиганта, Голема, Эл. гиганта — медленных танков.',
      steps: ['Не трать эликсир у моста — дай танку дойти до твоей половины.',
        'Здание в центр утянет его, а урон по нему дадут башни и юнит сзади.',
        'Пока у соперника нет эликсира — сразу контратака в другую линию.'] },
    anti_air: { title: 'Шар: встречай до башни', when: 'Против Шара, Лавы, летучих орд.',
      steps: ['Не жди, пока Шар долетит до башни — он сносит её за пару ударов.',
        'Поставь мушкетёра или стрелков за башней, как только Шар перелетел реку.',
        'Башня и юнит собьют его раньше, чем он начнёт бить.'] },
    anti_siege: { title: 'Икс-лук: дави сразу', when: 'Против Икс-лука и Мортиры у реки.',
      steps: ['Соперник поставил лук у реки — он ещё не набрал урон.',
        'Сразу ставь танка (Рыцаря, Ледяного голема) прямо на мост перед ним.',
        'Танк ломает лук, мелочь соперника добей заклинанием.'] },
    anti_miner: { title: 'Шахтёр: юнит рядом с башней', when: 'Против Шахтёра, Кладбища, Гоблинского бура.',
      steps: ['Шахтёр вылезает прямо у твоей башни — от него не убежать.',
        'Ставь дешёвого юнита (скелетов) вплотную к башне, как только он вылез.',
        'Ещё лучше — угадай заранее: у соперника 3 эликсира и он ждёт.'] },
    elixir: { title: 'Не держи 10 эликсира', when: 'Если в поражениях у тебя течёт эликсир.',
      steps: ['10 эликсира — это потеря: всё, что набирается сверх, сгорает.',
        'При 9+ и тишине на поле ставь дорогую карту в самый тыл, у задней стенки.',
        'Пока она дойдёт до моста, наберёшь эликсир на поддержку — и пуш готов.'] },
    tilt: { title: 'Два поражения — пауза', when: 'Если после поражений подряд ты играешь хуже.',
      steps: ['После двух поражений подряд закрой игру на 10 минут.',
        'Злой следующий бой почти всегда проигран — и стоит кубков.',
        'Стоп-тильт в Плюсе напишет «стоп» сам, прямо во время серии.'] },
    levels: { title: 'Качай свою колоду, а не всё', when: 'Если соперники в поражениях прокачаннее тебя.',
      steps: ['Разница в уровень — это примерно +10% урона и здоровья у соперника.',
        'Не распыляй золото: качай 8 карт основной колоды, начиная с главной.',
        'Пока колода не догнала — играй её в режимах с выровненными уровнями.'] },
    deck: { title: 'Колода проседает — сравни с топом', when: 'Если твоя колода выигрывает реже, чем у топа.',
      steps: ['Та же колода у топа выигрывает чаще — дело в 1–2 картах или в том, как ты её играешь.',
        'Открой конструктор: он покажет, против чего колода тонет и чем заменить карту.',
        'Посмотри фишки против своих самых частых соперников.'] },
  },
  uk: {
    anti_hog: { title: 'Хог: будівля в центр', when: 'Проти Хога, Таранів і Рами — усіх, хто б\'є лише по будівлях.',
      steps: ['Дочекайся, поки Хог перейде міст — раніше не став, інакше його пустять іншою лінією.',
        'Став гармату чи теслу в центр, приблизно за 4 клітинки нижче річки.',
        'Хог зверне до будівлі й стане бити її під вогнем веж — до твоєї вежі не дійде.'] },
    kite: { title: 'Відвести важкого в центр', when: 'Проти Мега-лицаря, ПЕККИ, Принца — тих, хто б\'є найближчого.',
      steps: ['Важкий перейшов міст і йде на вежу.',
        'Кинь дешевого юніта (скелетів, крижаного духа) в центр між вежами.',
        'Важкий зверне до нього й піде центром під вогнем двох веж, а ти доб\'єш його без втрат.'] },
    log_barrel: { title: 'Колода по бочці', when: 'Проти Гоблінської бочки та Бочки зі скелетами.',
      steps: ['Бочку видно в польоті — не панікуй і не кидай зарано.',
        'Щойно бочка підлітає до вежі, кидай колоду просто на свою вежу — вона покотиться вперед.',
        'Колода змітає гоблінів у момент приземлення — вежа навіть не отримує урону.'] },
    anti_tank: { title: 'Гігант: не захищайся біля мосту', when: 'Проти Гіганта, Голема, Ел. гіганта — повільних танків.',
      steps: ['Не витрачай еліксир біля мосту — дай танку дійти до твоєї половини.',
        'Будівля в центрі потягне його, а урон дадуть вежі й юніт ззаду.',
        'Поки в суперника немає еліксиру — одразу контратака іншою лінією.'] },
    anti_air: { title: 'Куля: зустрічай до вежі', when: 'Проти Кулі, Лави, летючих орд.',
      steps: ['Не чекай, поки Куля долетить до вежі — вона зносить її за кілька ударів.',
        'Постав мушкетерку чи лучниць за вежею, щойно Куля перелетіла річку.',
        'Вежа й юніт зіб\'ють її раніше, ніж вона почне бити.'] },
    anti_siege: { title: 'Ікс-лук: тисни одразу', when: 'Проти Ікс-лука й Мортири біля річки.',
      steps: ['Суперник поставив лук біля річки — він ще не набрав урону.',
        'Одразу став танка (Лицаря, Крижаного голема) просто на міст перед ним.',
        'Танк ламає лук, дрібноту суперника добий закляттям.'] },
    anti_miner: { title: 'Шахтар: юніт біля вежі', when: 'Проти Шахтаря, Кладовища, Гоблінського бура.',
      steps: ['Шахтар вилазить просто біля твоєї вежі — від нього не втекти.',
        'Став дешевого юніта (скелетів) впритул до вежі, щойно він виліз.',
        'Ще краще — вгадай заздалегідь: у суперника 3 еліксиру і він чекає.'] },
    elixir: { title: 'Не тримай 10 еліксиру', when: 'Якщо в поразках у тебе тече еліксир.',
      steps: ['10 еліксиру — це втрата: усе, що набирається понад, згорає.',
        'При 9+ і тиші на полі став дорогу карту в самий тил, біля задньої стінки.',
        'Поки вона дійде до мосту, набереш еліксир на підтримку — і пуш готовий.'] },
    tilt: { title: 'Дві поразки — пауза', when: 'Якщо після поразок поспіль граєш гірше.',
      steps: ['Після двох поразок поспіль закрий гру на 10 хвилин.',
        'Злий наступний бій майже завжди програний — і коштує кубків.',
        'Стоп-тільт у Плюсі напише «стоп» сам, просто під час серії.'] },
    levels: { title: 'Качай свою колоду, а не все', when: 'Якщо суперники в поразках прокачаніші за тебе.',
      steps: ['Різниця в рівень — це приблизно +10% урону й здоров\'я в суперника.',
        'Не розпорошуй золото: качай 8 карт основної колоди, починаючи з головної.',
        'Поки колода не наздогнала — грай нею в режимах з вирівняними рівнями.'] },
    deck: { title: 'Колода просідає — порівняй з топом', when: 'Якщо твоя колода виграє рідше, ніж у топу.',
      steps: ['Та сама колода в топу виграє частіше — справа в 1–2 картах або в тому, як ти нею граєш.',
        'Відкрий конструктор: він покаже, проти чого колода тоне і чим замінити карту.',
        'Подивись фішки проти своїх найчастіших суперників.'] },
  },
  en: {
    anti_hog: { title: 'Hog: building in the middle', when: 'Against Hog, Rams and Ram Rider — anything that only hits buildings.',
      steps: ['Wait until the Hog crosses the bridge — earlier and they just switch lanes.',
        'Drop a Cannon or Tesla in the middle, about 4 tiles below the river.',
        'The Hog turns to the building and stops to hit it under tower fire — it never reaches yours.'] },
    kite: { title: 'Pull the tank into the middle', when: 'Against Mega Knight, P.E.K.K.A, Prince — anything that hits the closest target.',
      steps: ['The tank crossed the bridge and heads to your tower.',
        'Drop a cheap unit (Skeletons, Ice Spirit) in the middle between your towers.',
        'The tank turns to it and walks through the middle under two towers — you finish it for free.'] },
    log_barrel: { title: 'Log the barrel', when: 'Against Goblin Barrel and Skeleton Barrel.',
      steps: ['You can see the barrel in the air — don\'t panic and don\'t throw early.',
        'As soon as the barrel reaches your tower, Log right onto your own tower — it rolls forward.',
        'The Log sweeps the goblins the moment they land — the tower takes no damage.'] },
    anti_tank: { title: 'Giant: don\'t defend at the bridge', when: 'Against Giant, Golem, Electro Giant — slow tanks.',
      steps: ['Don\'t spend elixir at the bridge — let the tank walk into your half.',
        'A building in the middle pulls it, the towers and a unit behind do the damage.',
        'While they\'re out of elixir — counter-push the other lane right away.'] },
    anti_air: { title: 'Balloon: meet it before the tower', when: 'Against Balloon, Lava, air swarms.',
      steps: ['Don\'t wait until the Balloon reaches your tower — it kills it in a few hits.',
        'Place a Musketeer or Archers behind the tower as soon as it crosses the river.',
        'The tower and the unit take it down before it starts hitting.'] },
    anti_siege: { title: 'X-Bow: pressure right away', when: 'Against X-Bow and Mortar at the river.',
      steps: ['They placed the X-Bow at the river — it hasn\'t dealt damage yet.',
        'Immediately drop a tank (Knight, Ice Golem) on the bridge in front of it.',
        'The tank breaks it; finish their small support with a spell.'] },
    anti_miner: { title: 'Miner: unit next to the tower', when: 'Against Miner, Graveyard, Goblin Drill.',
      steps: ['The Miner pops up right at your tower — you can\'t outrun it.',
        'Drop a cheap unit (Skeletons) right next to the tower as soon as it appears.',
        'Even better — predict it: they have 3 elixir and are waiting.'] },
    elixir: { title: 'Don\'t sit at 10 elixir', when: 'If you leak elixir in your losses.',
      steps: ['10 elixir is a loss: everything above it burns.',
        'At 9+ with a quiet field, place your expensive card at the very back.',
        'By the time it reaches the bridge you have elixir for support — the push is ready.'] },
    tilt: { title: 'Two losses — take a break', when: 'If you play worse after losses in a row.',
      steps: ['After two losses in a row, close the game for 10 minutes.',
        'An angry next match is almost always lost — and costs trophies.',
        'Stop-tilt in Plus says "stop" for you, right in the middle of a streak.'] },
    levels: { title: 'Level your deck, not everything', when: 'If opponents in your losses are higher level.',
      steps: ['One level is roughly +10% damage and HP for the opponent.',
        'Don\'t spread gold: level the 8 cards of your main deck, starting with the win condition.',
        'Until it catches up, play it in modes with capped levels.'] },
    deck: { title: 'Deck underperforms — compare with top', when: 'If your deck wins less than the same deck in the top.',
      steps: ['The same deck wins more in the top — it\'s 1–2 cards or how you play it.',
        'Open the deck builder: it shows what the deck struggles against and what to swap.',
        'Check the tips against your most common opponents.'] },
  },
}

export function tipText(lang: Lang, id: TipId): TipText {
  return TEXT[lang][id]
}

export function tipScene(lang: Lang, id: TipId): Scene | null {
  return SCENES[id]?.(UNITS[lang]) ?? null
}

export function isTipId(x: string | null | undefined): x is TipId {
  return !!x && (TIP_IDS as string[]).includes(x)
}
