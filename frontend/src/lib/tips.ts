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
  | 'elixir' | 'king_tornado' | 'tilt' | 'levels' | 'deck'

export const TIP_IDS: TipId[] = ['anti_hog', 'king_tornado', 'kite', 'log_barrel', 'anti_tank', 'anti_air', 'anti_siege', 'anti_miner', 'elixir', 'tilt', 'levels', 'deck']

/** Фишки, у которых есть схема арены. Остальные - советы без поля. */
export const ARENA_TIPS: TipId[] = ['anti_hog', 'king_tornado', 'kite', 'log_barrel', 'anti_tank', 'anti_air', 'anti_siege', 'anti_miner', 'elixir']

type Units = Record<'hog' | 'cannon' | 'mk' | 'skeletons' | 'barrel' | 'log' | 'goblins' | 'giant' | 'minipekka'
  | 'counter' | 'balloon' | 'musk' | 'xbow' | 'knight' | 'miner' | 'golem' | 'here' | 'cells4' | 'tornado' | 'kingUp', string>

export interface TipText { title: string; when: string; steps: string[] }

const UNITS: Record<Lang, Units> = {
  ru: { hog: 'Хог', cannon: 'Пушка', mk: 'Мега-рыцарь', skeletons: 'Скелеты', barrel: 'Бочка', log: 'Бревно', goblins: 'Гоблины',
    giant: 'Гигант', minipekka: 'Мини-ПЕККА', counter: 'Контратака', balloon: 'Шар', musk: 'Мушкетёр', xbow: 'Икс-лук',
    knight: 'Рыцарь', miner: 'Шахтёр', golem: 'Голем', here: 'сюда', cells4: '4 клетки', tornado: 'Торнадо', kingUp: 'Король проснулся!' },
  uk: { hog: 'Хог', cannon: 'Гармата', mk: 'Мега-лицар', skeletons: 'Скелети', barrel: 'Бочка', log: 'Колода', goblins: 'Гобліни',
    giant: 'Гігант', minipekka: 'Міні-ПЕККА', counter: 'Контратака', balloon: 'Куля', musk: 'Мушкетерка', xbow: 'Ікс-лук',
    knight: 'Лицар', miner: 'Шахтар', golem: 'Голем', here: 'сюди', cells4: '4 клітинки', tornado: 'Торнадо', kingUp: 'Король прокинувся!' },
  en: { hog: 'Hog', cannon: 'Cannon', mk: 'Mega Knight', skeletons: 'Skeletons', barrel: 'Barrel', log: 'Log', goblins: 'Goblins',
    giant: 'Giant', minipekka: 'Mini P.E.K.K.A', counter: 'Counter-push', balloon: 'Balloon', musk: 'Musketeer', xbow: 'X-Bow',
    knight: 'Knight', miner: 'Miner', golem: 'Golem', here: 'here', cells4: '4 tiles', tornado: 'Tornado', kingUp: 'King is awake!' },
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
  // Торнадо на вторую клетку перед королём, на две к башне, когда Хог уже у неё: Хога тянет в радиус короля
  king_tornado: u => ({
    duration: 6.5,
    marks: [{ x: 7.5, y: 26, from: 1.4, to: 2.3, label: u.here }],
    actors: [
      { id: 'hog', kind: 'enemy', card: 'Hog Rider', label: u.hog, r: 0.75, to: 5.2,
        keys: [{ t: 0, x: 3.5, y: 11.5 }, { t: 1.1, x: 3.5, y: 17.3 }, { t: 2.3, x: 3.5, y: 21.6 }, { t: 3.2, x: 6.6, y: 26.2 }, { t: 5.2, x: 6.6, y: 26.3 }] },
      { id: 'tn', kind: 'spell', card: 'Tornado', label: '', r: 5.5, keys: [{ t: 2.3, x: 7.5, y: 26 }, { t: 3.35, x: 7.5, y: 26 }] },
      { id: 'up', kind: 'text', label: u.kingUp, keys: [{ t: 2.9, x: 9, y: 27.2 }, { t: 4.4, x: 9, y: 27.2 }] },
    ],
  }),
  // Тяжа держит урон в лоб: Мини-ПЕККА сразу за мостом, башня помогает - как в тренажёре
  kite: u => ({
    duration: 7,
    marks: [{ x: 3.5, y: 19.5, from: 0.9, to: 1.7, label: u.here }],
    actors: [
      { id: 'mk', kind: 'enemy', card: 'Mega Knight', label: u.mk, r: 1.0, to: 5.6,
        keys: [{ t: 0, x: 3.5, y: 12.5 }, { t: 1.6, x: 3.5, y: 17.6 }, { t: 2.4, x: 3.5, y: 18.4 }, { t: 5.6, x: 3.5, y: 18.6 }] },
      { id: 'mp', kind: 'mine', card: 'Mini P.E.K.K.A', label: u.minipekka, r: 0.65, keys: [{ t: 1.7, x: 3.5, y: 19.6 }, { t: 2.6, x: 3.5, y: 19.7 }, { t: 7, x: 3.5, y: 19.7 }] },
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
  // Икс-лук бьёт ближайшее: пушка у самой реки забирает его огонь и сама достаёт до лука
  anti_siege: u => ({
    duration: 7,
    noShots: true,
    marks: [{ x: 14, y: 18, size: 2, from: 0.6, to: 1.4, label: u.here }],
    actors: [
      { id: 'xbow', kind: 'enemy', card: 'X-Bow', label: u.xbow, r: 0.9, to: 5.6, keys: [{ t: 0, x: 14.5, y: 13 }, { t: 5.6, x: 14.5, y: 13 }] },
      { id: 'cn', kind: 'building', card: 'Cannon', label: u.cannon, r: 0.95, keys: [{ t: 1.0, x: 14, y: 18 }, { t: 7, x: 14, y: 18 }] },
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
        'Хог свернёт к зданию и встанет бить его под огнём башен — до твоей башни он не дойдёт.',
        'Если Хог прыгает с самого края моста — сдвинь здание на клетку к башне («4-2»), из центра он его обойдёт.'] },
    kite: { title: 'Тяж: встречай уроном', when: 'Против Мега-рыцаря, ПЕККИ, Принца — тяжей, которые бьют ближайшего.',
      steps: ['Тяж перешёл мост — ему нужно всего 3–4 удара, чтобы снести башню.',
        'Ставь Мини-ПЕККА прямо на его пути, сразу за мостом: 720 урона за удар, и башня добивает.',
        'Скелеты в центре только уведут его на секунду — Мега-рыцарь давит их прыжком. Отвлекай, когда урон уже на поле.',
        'Против ПЕККИ — Ледяной голем у середины реки, на клетку в другую линию: она пойдёт за ним, и башни добьют её по дороге.'] },
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
        'Башня и юнит собьют его раньше, чем он начнёт бить.',
        'Шар с самого края не видит здание в центре — ставь пушку правее центра, ближе к реке.'] },
    anti_siege: { title: 'Икс-лук: здание у реки', when: 'Против Икс-лука и Мортиры у реки.',
      steps: ['Икс-лук бьёт ближайшее — и с моста достаёт до твоей башни.',
        'Поставь пушку у самой реки напротив лука: он переключится на неё, а пушка сама достанет до лука.',
        'Рыцарь на мосту тоже ломает лук, но под огнём лука и чужой башни сам теряет здоровье — здание надёжнее.',
        'Мортира не стреляет ближе 3,5 клетки: против неё отправь юнита вплотную — она его не тронет.'] },
    anti_miner: { title: 'Шахтёр: юнит рядом с башней', when: 'Против Шахтёра, Кладбища, Гоблинского бура.',
      steps: ['Шахтёр вылезает прямо у твоей башни — от него не убежать.',
        'Ставь дешёвого юнита (скелетов) вплотную к башне, как только он вылез.',
        'Ещё лучше — угадай заранее: у соперника 3 эликсира и он ждёт.'] },
    king_tornado: { title: 'Торнадо будит короля', when: 'Против Хога, Тарана, Рам, Гоблинской бочки — всех, кто идёт на башню принцессы.',
      steps: ['Дождись, пока Хог подойдёт к твоей башне принцессы — раньше Торнадо не дотянет его до короля.',
        'Кидай Торнадо на вторую клетку перед королём, на две клетки в сторону этой башни.',
        'Хога затянет вплотную к королю — король проснётся, вместе с принцессой собьёт Хога и будет стрелять до конца боя.'] },
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
        'Хог зверне до будівлі й стане бити її під вогнем веж — до твоєї вежі не дійде.',
        'Якщо Хог стрибає з самого краю мосту — зсунь будівлю на клітинку до вежі («4-2»), центр він обійде.'] },
    kite: { title: 'Важкий: зустрічай уроном', when: 'Проти Мега-лицаря, ПЕККИ, Принца — важких, що б\'ють найближчого.',
      steps: ['Важкий перейшов міст — йому треба лише 3–4 удари, щоб знести вежу.',
        'Став Міні-ПЕККА просто на його шляху, одразу за мостом: 720 урону за удар, і вежа добиває.',
        'Скелети в центрі лише відведуть його на секунду — Мега-лицар тисне їх стрибком. Відволікай, коли урон уже на полі.',
        'Проти ПЕККИ — Крижаний голем біля середини річки, на клітинку в іншу лінію: вона піде за ним, і вежі доб\'ють її дорогою.'] },
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
        'Вежа й юніт зіб\'ють її раніше, ніж вона почне бити.',
        'Куля з самого краю не бачить будівлю в центрі — став гармату правіше від центру, ближче до річки.'] },
    anti_siege: { title: 'Ікс-лук: будівля біля річки', when: 'Проти Ікс-лука й Мортири біля річки.',
      steps: ['Ікс-лук б\'є найближче — і з мосту дістає до твоєї вежі.',
        'Постав гармату біля самої річки навпроти лука: він переключиться на неї, а гармата сама дістане до лука.',
        'Лицар на мосту теж ламає лук, але під вогнем лука й чужої вежі сам втрачає здоров\'я — будівля надійніша.',
        'Мортира не стріляє ближче 3,5 клітинки: проти неї відправ юніта впритул — вона його не зачепить.'] },
    anti_miner: { title: 'Шахтар: юніт біля вежі', when: 'Проти Шахтаря, Кладовища, Гоблінського бура.',
      steps: ['Шахтар вилазить просто біля твоєї вежі — від нього не втекти.',
        'Став дешевого юніта (скелетів) впритул до вежі, щойно він виліз.',
        'Ще краще — вгадай заздалегідь: у суперника 3 еліксиру і він чекає.'] },
    king_tornado: { title: 'Торнадо будить короля', when: 'Проти Хога, Тарана, Рам, Гоблінської бочки — усіх, хто йде на вежу принцеси.',
      steps: ['Дочекайся, поки Хог підійде до твоєї вежі принцеси — раніше Торнадо не дотягне його до короля.',
        'Кидай Торнадо на другу клітинку перед королем, на дві клітинки в бік цієї вежі.',
        'Хога затягне впритул до короля — король прокинеться, разом із принцесою зіб\'є Хога й стрілятиме до кінця бою.'] },
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
        'The Hog turns to the building and stops to hit it under tower fire — it never reaches yours.',
        'If the Hog jumps from the very edge of the bridge, shift the building one tile toward the tower (4-2) — it bypasses a middle one.'] },
    kite: { title: 'Tank: meet it with damage', when: 'Against Mega Knight, P.E.K.K.A, Prince — tanks that hit the closest target.',
      steps: ['The tank crossed the bridge — it needs only 3–4 hits to take your tower.',
        'Drop Mini P.E.K.K.A right in its path just past the bridge: 720 damage per hit, the tower finishes it.',
        'Skeletons in the middle only pull it for a second — the Mega Knight crushes them with its jump. Distract once your damage is on the field.',
        'Against P.E.K.K.A — Ice Golem at the middle of the river, one tile into the other lane: it follows the Golem and the towers finish it on the way.'] },
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
        'The tower and the unit take it down before it starts hitting.',
        'An edge Balloon doesn\'t see a middle building — put the Cannon off-center toward it, closer to the river.'] },
    anti_siege: { title: 'X-Bow: building at the river', when: 'Against X-Bow and Mortar at the river.',
      steps: ['The X-Bow hits the closest target — and reaches your tower from the bridge.',
        'Place a Cannon right at the river across from it: the X-Bow switches to it, and the Cannon reaches the X-Bow.',
        'A Knight on the bridge also breaks it, but takes fire from the X-Bow and their tower — the building is safer.',
        'The Mortar can\'t shoot closer than 3.5 tiles: send a unit right up to it — it can\'t touch it.'] },
    anti_miner: { title: 'Miner: unit next to the tower', when: 'Against Miner, Graveyard, Goblin Drill.',
      steps: ['The Miner pops up right at your tower — you can\'t outrun it.',
        'Drop a cheap unit (Skeletons) right next to the tower as soon as it appears.',
        'Even better — predict it: they have 3 elixir and are waiting.'] },
    king_tornado: { title: 'Tornado wakes the King', when: 'Against Hog, Battle Ram, Ram Rider, Goblin Barrel — anything going for your Princess Tower.',
      steps: ['Wait until the Hog reaches your Princess Tower — earlier and the Tornado won\'t drag it to the King.',
        'Drop Tornado on the second tile in front of your King, two tiles toward that tower.',
        'The Hog gets pulled right up to your King — he wakes up, kills the Hog with the Princess and keeps shooting all match.'] },
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
