import { useEffect, useState, useCallback, useRef } from 'react'
import { Icon, type IconName } from './components/ui/Icon'
import { IconTile } from './components/ui/Section'
import { api, ApiError, adminClan } from './lib/api'
import { askDmOnce, haptic, rawStartParam, startChallengeCode, startMatchId, startParam, startToMatches } from './lib/telegram'
import { CreatorChallengeScreen } from './components/ChallengeView'
import { afterPromo, OPEN_CHALLENGE } from './lib/promo'
import { usePlusSheet } from './lib/plusSheet'
import { useT, type Translations } from './lib/i18n'
import type { AppConfig, ClanStatus, ServiceIdentity } from './types'
import { WarHeader } from './components/WarHeader'
import { ForecastCard } from './components/ForecastCard'
import { InsightsCard } from './components/InsightsCard'
import { RaceCard } from './components/RaceCard'
import { WarLogCard } from './components/WarLogCard'
import { StatsStrip } from './components/StatsStrip'
import { WhatsNewCard } from './components/WhatsNewCard'
import { PlayerList } from './components/PlayerList'
import { Leaderboard } from './components/Leaderboard'
import { MyStatsView } from './components/MyStatsView'
import { NudgeButton } from './components/NudgeButton'
import { NotificationSettingsView } from './components/NotificationSettingsView'
import { ClanWorldRankCard } from './components/ClanWorldRankCard'
import { WarJournalCard } from './components/WarJournalCard'
import { OwnerPanel } from './components/OwnerPanel'
import { LinkPrompt } from './components/LinkPrompt'
import { PlayerSearchView } from './components/PlayerSearchView'
import { ClanlessView, type SoloReason } from './components/ClanlessView'
import { GuestEntry } from './components/GuestEntry'
import { GuestMyStats } from './components/GuestMyStats'
import { LeaderCtaCard } from './components/LeaderCtaCard'
import { MyActionBanner } from './components/MyActionBanner'
import { SplashScreen } from './components/SplashScreen'
import { MoreView } from './components/MoreView'
import { ConnectChatCard, InviteMomentCard, InviteUnlinkedCard } from './components/EngageCards'
import { ChannelCard } from './components/ChannelCard'
import { useShowcase } from './lib/showcase'
import { HalloweenView } from './components/HalloweenView'
import { HalloweenBanner } from './components/HalloweenBanner'
import { FeedbackCard } from './components/FeedbackCard'
import { OPEN_HALLOWEEN } from './lib/halloween'
import { OPEN_TRAINER } from './lib/trainer'
import type { MoreSection } from './components/MoreView'
import { MenuChangedNotice } from './components/MenuChangedNotice'
import { UpdateNotice } from './components/UpdateNotice'
import { ScoutCard } from './components/ScoutCard'
import { ClanHqView } from './components/ClanHqView'
import { ModeHeader, TabBar, usePlusState, type BarTab } from './components/AppChrome'
import { DecksHub, PlayHub, TrainerHub, type DecksSection, type PlaySection } from './components/PlayerHub'
import { loadMode, saveMode, type AppMode } from './lib/mode'
import { OPEN_DECKS, useQuestsSeen } from './lib/quests'
import { QuestsCard } from './components/QuestsCard'
import { WorldTopEntry } from './components/WorldTopEntry'
import { weekKing } from './lib/king'
import { canUseOwnerPanel, canUseStudio } from './lib/serviceAccess'

type State =
  | { kind: 'loading' }
  | { kind: 'link' }
  | { kind: 'guestEntry' }
  | { kind: 'notInTelegram' }
  | { kind: 'clanless'; reason: SoloReason }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: ClanStatus }
  | { kind: 'guest'; data: ClanStatus; myPlayerTag: string }

const POLL_INTERVAL_MS = 60_000
const RETRY_INTERVAL_MS = 15_000
const TRANSIENT_TOLERANCE = 3

/**
 * Вкладки двух режимов. Клан: война, штаб, состав, рейтинг. Игрок: профиль, колоды,
 * тренажёр, турниры. «Ещё» и панель владельца - в обоих. Гость смотрит чужой клан
 * со стороны и живёт в упрощённом наборе: война, его статистика, турнир, поиск.
 */
type Tab = 'war' | 'hq' | 'roster' | 'rating' | 'halloween'
  | 'profile' | 'decks' | 'trainer' | 'play'
  | 'tournament' | 'search' | 'more' | 'owner' | 'creatorChallenge'

const CLAN_TABS: Tab[] = ['war', 'hq', 'roster', 'rating', 'halloween']
const PLAYER_TABS: Tab[] = ['profile', 'decks', 'trainer', 'play']

/** Что теперь живёт в нижнем меню режима игрока - в «Ещё» это не дублируем. */
const IN_BAR: MoreSection[] = ['trainer', 'tips', 'builder', 'worldTop', 'challenge', 'duel', 'tournament']

/**
 * Внутри «Клана»: война, состав и рейтинг — разные взгляды на один и тот же клан.
 *
 * «Состав» выделен в отдельную секцию не ради симметрии. Список на 50 человек стоял
 * в конце военного экрана и физически закрывал собой всё, что под ним: журнал боёв
 * приходилось искать, пролистав полсотни карточек. Теперь аналитика войны помещается
 * на один экран, а состав открывается, когда он действительно нужен.
 */
type ClanSection = 'war' | 'roster' | 'rating'

/** Переключатель «Война / Состав / Рейтинг» внутри вкладки клана. */
function ClanSectionTabs({ value, onChange, t }: {
  value: ClanSection
  onChange: (next: ClanSection) => void
  t: Translations
}) {
  return (
    <div className="clan-sections">
      <button
        className={`clan-section ${value === 'war' ? 'clan-section-on' : ''}`}
        onClick={() => onChange('war')}
      >
        <Icon name="swords" size={15} /> {t.tabs.war}
      </button>
      <button
        className={`clan-section ${value === 'roster' ? 'clan-section-on' : ''}`}
        onClick={() => onChange('roster')}
      >
        <Icon name="users" size={15} /> {t.tabs.roster}
      </button>
      <button
        className={`clan-section ${value === 'rating' ? 'clan-section-on' : ''}`}
        onClick={() => onChange('rating')}
      >
        <Icon name="trophy" size={15} /> {t.tabs.rating}
      </button>
    </div>
  )
}

/**
 * Куда вести по параметру запуска из бота: «Открыть разбор» - в профиль игрока,
 * «/meta» и билдер - в колоды, тренажёр, челлендж и дуэли - в режим игрока,
 * ch_<код> - в челлендж блогера (отдельный экран вне бара). Без параметра - туда,
 * где человек был в прошлый раз.
 */
const START_PLAYER_TAB: Tab | null = startParam === 'review' || startToMatches || startParam === 'quests' ? 'profile'
  : startParam === 'meta' || startParam === 'builder' ? 'decks'
  : startParam === 'trainer' ? 'trainer'
  : startParam === 'challenge' || startParam === 'duel' ? 'play'
  : null
const START_DECKS: DecksSection | undefined = startParam === 'meta' ? 'meta' : startParam === 'builder' ? 'pick' : undefined
const START_PLAY: PlaySection | undefined = startParam === 'duel' ? 'duel' : startParam === 'challenge' ? 'challenge' : undefined
const START_MODE: AppMode = START_PLAYER_TAB ? 'player' : startParam === 'halloween' ? 'clan' : loadMode() ?? 'clan'
const START_TAB: Tab = startChallengeCode ? 'creatorChallenge'
  : startParam === 'halloween' ? 'halloween'
  : START_PLAYER_TAB ?? (startParam === 'invite' ? 'more' : START_MODE === 'player' ? 'profile' : 'war')

/** «Разбор» из бота — разбор за 30 дней, трекер и «Все бои» — история. */
const START_BATTLES_VIEW = startParam === 'review' ? 'review' : 'history'

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })

  // Ссылка startapp=showcase - сразу витрина Плюса и спонсорства
  const openShowcase = useShowcase()
  const showcaseOpenedRef = useRef(false)
  useEffect(() => {
    if (showcaseOpenedRef.current || state.kind === 'loading' || state.kind === 'notInTelegram') return
    if (startParam !== 'showcase' && startParam !== 'showcase_sponsor') return
    showcaseOpenedRef.current = true
    openShowcase(startParam === 'showcase_sponsor' ? 'sponsor' : 'plus')
  }, [state.kind, openShowcase])

  // Откуда открыли приложение - раз за запуск, для сводки «откуда заходят» в панели.
  // Вне Telegram (превью) не шлём: там нет ни пользователя, ни источника.
  const openTrackedRef = useRef(false)
  useEffect(() => {
    if (openTrackedRef.current || state.kind === 'loading' || state.kind === 'notInTelegram') return
    openTrackedRef.current = true
    api.trackOpen(rawStartParam).catch(() => { /* статистика не повод мешать */ })
  }, [state.kind])

  // Разрешение писать в личку — один раз, через пару секунд после загрузки: окно
  // поверх пустого экрана загрузки выглядело бы как ошибка.
  const dmAskedRef = useRef(false)
  useEffect(() => {
    if (dmAskedRef.current || state.kind === 'loading' || state.kind === 'notInTelegram') return
    const id = window.setTimeout(() => {
      // Отметка внутри таймера: если экран сменился раньше, спросим на следующем
      dmAskedRef.current = true
      afterPromo()
        .then(askDmOnce)
        .then(allowed => { if (allowed) api.dmAllowed().catch(() => { /* повторим при следующем включении */ }) })
    }, 2500)
    return () => window.clearTimeout(id)
  }, [state.kind])
  const [tab, setTab] = useState<Tab>(START_TAB)
  const [mode, setMode] = useState<AppMode>(START_MODE)
  // Где был в каждом режиме: переключатель возвращает туда же, а не на первую вкладку
  const lastTab = useRef<Record<AppMode, Tab>>({ clan: 'war', player: 'profile' })
  const goPlayer = useCallback((next: Tab) => {
    setMode('player'); saveMode('player'); setTab(next); window.scrollTo(0, 0)
  }, [])
  // С какого раздела открыть колоды и турниры: по ссылке бота или по кнопке из приложения
  const [decksInitial] = useState<DecksSection | undefined>(START_DECKS)
  const [playInitial, setPlayInitial] = useState<PlaySection | undefined>(START_PLAY)
  const [playKey, setPlayKey] = useState(0)
  // Кнопка «К челленджу» из окна-анонса
  useEffect(() => {
    const go = () => { setPlayInitial('challenge'); setPlayKey(k => k + 1); goPlayer('play') }
    window.addEventListener(OPEN_CHALLENGE, go)
    return () => window.removeEventListener(OPEN_CHALLENGE, go)
  }, [goPlayer])
  // Вывод профиля «потренируй это» - в тренажёр режима игрока
  useEffect(() => {
    const go = () => goPlayer('trainer')
    window.addEventListener(OPEN_TRAINER, go)
    return () => window.removeEventListener(OPEN_TRAINER, go)
  }, [goPlayer])
  // Задание дня «подбери колоду» - в подбор колод режима игрока
  useEffect(() => {
    const go = () => goPlayer('decks')
    window.addEventListener(OPEN_DECKS, go)
    return () => window.removeEventListener(OPEN_DECKS, go)
  }, [goPlayer])
  const questsSeen = useQuestsSeen()
  // Заставка и баннер Хэллоуина ведут во вкладку босса
  useEffect(() => {
    const go = () => { setMode('clan'); setTab('halloween') }
    window.addEventListener(OPEN_HALLOWEEN, go)
    return () => window.removeEventListener(OPEN_HALLOWEEN, go)
  }, [])
  const plus = usePlusState()
  const openPlus = usePlusSheet()

  // Кнопка «💎 Открыть Плюс» в боте: окно Плюса сразу при запуске, один раз.
  const plusOpenedRef = useRef(false)
  useEffect(() => {
    if ((startParam !== 'plus' && startParam !== 'plus_trk') || plusOpenedRef.current) return
    if (state.kind === 'loading') return
    plusOpenedRef.current = true
    openPlus()
  }, [state.kind, openPlus])
  const [clanSection, setClanSection] = useState<ClanSection>('war')
  const [settingsOpen, setSettingsOpen] = useState(false)
  // Права на сервис спрашиваем отдельно от статуса клана.
  //
  // Раньше признак владельца приезжал внутри my/status, а та отвечает 404, пока у
  // человека нет привязанного тега и клана. То есть панель пропадала ровно у того,
  // кто вышел из клана, — и вернуть её было нечем. Модератору же клан не нужен
  // вовсе, он может вообще не играть.
  const [me, setMe] = useState<ServiceIdentity>({ role: 'none', permissions: [] })
  // Состав нижних вкладок задаёт владелец из панели, поэтому он приезжает с сервера.
  // Пока не приехал — показываем набор по умолчанию, чтобы навигация была сразу.
  const [config, setConfig] = useState<AppConfig | null>(null)

  // Тема клана: спонсор выбрал фон — приложение красится под него.
  //
  // Атрибутом на <html>, а не пропсами по дереву: акцент используют десятки
  // компонентов, и протаскивать его в каждый значило бы рано или поздно забыть
  // один, который остался бы синим посреди огненной темы.
  useEffect(() => {
    const theme = state.kind === 'ready' ? state.data.clanBackgroundKey : null
    const root = document.documentElement
    if (theme) root.setAttribute('data-clan-theme', theme)
    else root.removeAttribute('data-clan-theme')
  }, [state])

  // Отдельно от остального: конфиг перечитывается после покупки фона, чтобы
  // выбранное применилось без перезапуска приложения.
  const loadConfig = useCallback(() => {
    api.getAppConfig()
      .then(setConfig)
      .catch(() => { /* останемся на наборе вкладок по умолчанию */ })
  }, [])
  const { t } = useT()

  // Ивент кончился, а приложение открыли старой ссылкой startapp=halloween - на первый экран
  useEffect(() => {
    if (config && config.event !== 'halloween' && tab === 'halloween') setTab('war')
  }, [config, tab])

  // Сколько подряд неудачных обновлений терпим, прежде чем показать ошибку.
  // Разовый сбой сети или икота CR API не должны стирать рабочий экран.
  const failuresRef = useRef(0)

  const load = useCallback(async () => {
    try {
      const data = await api.getMyClanStatus()
      failuresRef.current = 0
      setState({ kind: 'ready', data })
    } catch (e) {
      // Смысловые ответы применяем сразу: игрок вышел из клана, отвязался и т.п.
      // Всё остальное (сеть, 5xx, таймаут, лимит) — временное.
      const semantic = e instanceof ApiError &&
        ['player_not_linked', 'clan_not_found', 'no_init_data', 'bad_init_data'].includes(e.code)

      if (!semantic) {
        failuresRef.current += 1
        // Пока есть что показывать и сбоев мало — оставляем экран как есть
        if (failuresRef.current < TRANSIENT_TOLERANCE) {
          let keep = false
          setState(prev => {
            keep = prev.kind === 'ready' || prev.kind === 'guest'
            return prev
          })
          if (keep) return
        }
      }

      if (e instanceof ApiError && e.code === 'player_not_linked') {
        // Try restoring a guest session from localStorage
        const guestTag = localStorage.getItem('guestPlayerTag')
        const guestClanTag = localStorage.getItem('guestClanTag')
        if (guestTag && guestClanTag) {
          try {
            const data = await api.getClanStatus(guestClanTag)
            setState({ kind: 'guest', data, myPlayerTag: guestTag })
            return
          } catch { /* fall through to entry screen */ }
        }
        // Не привязан — первым делом привязка тега: без неё нет ни разбора боёв,
        // ни Плюса. Посмотреть клан без привязки можно с того же экрана.
        setState({ kind: 'link' })
      } else if (e instanceof ApiError && e.code === 'clan_not_found') {
        setState({ kind: 'clanless', reason: 'noClan' })
      } else if (e instanceof ApiError && e.code === 'war_not_found') {
        // У подключённого клана нет войны. Раньше это был экран ошибки с «повторить»,
        // хотя ничего не сломалось: теперь — приложение игрока, война вернётся сама.
        // Это не сбой, поэтому и опрашиваем в обычном темпе, а не каждые 15 секунд.
        failuresRef.current = 0
        setState({ kind: 'clanless', reason: 'noWar' })
      } else if (e instanceof ApiError && (e.code === 'no_init_data' || e.code === 'bad_init_data')) {
        setState({ kind: 'notInTelegram' })
      } else if (e instanceof ApiError && (e.status === 500 || e.status === 503)) {
        // 503 may carry a meaningful message (e.g. CR API token expired); 500 falls back to generic
        const msg = e.status === 503 && e.message && e.message !== e.code ? e.message : t.serverError
        setState({ kind: 'error', message: msg })
      } else {
        setState({ kind: 'error', message: e instanceof Error && e.message ? e.message : t.networkError })
      }
    }
  }, [t])

  // Один раз за сеанс: роль не меняется, пока приложение открыто. Молча падаем в
  // «никто» — панель просто не появится, а ломать из-за этого весь экран незачем.
  useEffect(() => {
    let alive = true
    api.ownerMe()
      .then(r => { if (alive) setMe(r) })
      .catch(() => { /* обычный игрок, и это нормальный ответ */ })
    loadConfig()
    return () => { alive = false }
  }, [loadConfig])

  useEffect(() => {
    load()
    // Пока всё хорошо — раз в минуту. После сбоя опрашиваем чаще, чтобы
    // вернуться в строй быстрее, чем пользователь заметит устаревшие цифры.
    let id: number = window.setInterval(function tick() {
      load()
      const next = failuresRef.current > 0 ? RETRY_INTERVAL_MS : POLL_INTERVAL_MS
      clearInterval(id)
      id = window.setInterval(tick, next)
    }, POLL_INTERVAL_MS)
    return () => clearInterval(id)
  }, [load])

  const switchTab = (next: Tab) => {
    setTab(next)
    window.scrollTo(0, 0)
  }

  const toggleMode = () => {
    const next: AppMode = mode === 'clan' ? 'player' : 'clan'
    const own = mode === 'clan' ? CLAN_TABS : PLAYER_TABS
    if (own.includes(tab)) lastTab.current[mode] = tab
    setMode(next)
    saveMode(next)
    setTab(lastTab.current[next])
    window.scrollTo(0, 0)
  }

  const exitGuest = () => {
    haptic('light')
    localStorage.removeItem('guestPlayerTag')
    localStorage.removeItem('guestClanTag')
    setState({ kind: 'guestEntry' })
  }

  switch (state.kind) {
    case 'loading':
      return <SplashScreen />
    case 'link':
      return (
        <main>
          <LinkPrompt />
          <div className="center" style={{ minHeight: 'auto', padding: '4px 0 24px' }}>
            <button className="btn-mini" onClick={() => { haptic('light'); setState({ kind: 'guestEntry' }) }}>
              {t.link.browseClan}
            </button>
          </div>
        </main>
      )
    case 'guestEntry':
      return (
        <GuestEntry
          onSuccess={(playerTag, clanTag, data) => {
            setState({ kind: 'guest', data, myPlayerTag: playerTag })
            // Гость ввёл СВОЙ тег — первым делом показываем ЕГО статистику,
            // а не клан: личная ценность цепляет сильнее общей таблицы.
            setTab('profile')
          }}
        />
      )
    case 'clanless':
      // Админ сервиса без своего клана — не тупик: панель и есть то, зачем он зашёл,
      // а заход в чужой клан из неё вернёт обычные экраны. Блогеру панель не нужна —
      // он получает обычное приложение игрока со «Студией» в «Ещё». Ссылка на
      // челлендж блогера важнее панели: по ней пришли смотреть таблицу, а не кланы.
      return canUseOwnerPanel(me) && !startChallengeCode
        ? <OwnerPanel me={me} />
        : <ClanlessView reason={state.reason}
            initialTab={startChallengeCode ? 'creatorChallenge' : START_PLAYER_TAB === 'decks' || START_PLAYER_TAB === 'trainer' || START_PLAYER_TAB === 'play' ? START_PLAYER_TAB : 'me'}
            decksInitial={START_DECKS} playInitial={START_PLAY}
            challengeCode={startChallengeCode}
            battlesView={START_BATTLES_VIEW} openMatchId={startMatchId}
            showStudio={canUseStudio(me)}
            config={config} onConfigChanged={loadConfig} />
    case 'notInTelegram':
      return (
        <div className="center">
          <IconTile name="lock" tone="violet" size={56} />
          <p><strong>{t.openViaTitle}</strong></p>
          <p className="muted small" style={{ maxWidth: 280, textAlign: 'center' }}>
            {t.openViaHint}
          </p>
        </div>
      )
    case 'error':
      return (
        <div className="center">
          <IconTile name="alert" tone="red" size={48} />
          <p className="muted">{state.message}</p>
          <button className="btn" onClick={load}><Icon name="refresh" size={16} /> {t.retry}</button>
        </div>
      )
    case 'ready': {
      const { data } = state
      const canManage = Boolean(data.isAdmin || data.isClanLeader)
      const notFinished = data.players.filter(p => p.status !== 'played').length
      const myRow = data.players.find(p => p.playerTag === data.myPlayerTag)
      // Удачный момент позвать друга: сам только что отыграл все колоды дня
      const iFinishedToday = data.periodType !== 'training' && (myRow?.decksUsedToday ?? 0) >= 4

      // Король недели считается один раз на оба экрана — иначе состав и рейтинг
      // однажды разойдутся в том, кого короновать.
      const king = weekKing(data.players, data.warLog, data.periodType)

      const hasPlus = Boolean(plus?.active || plus?.isSponsor || plus?.unlocked)
      const hasPro = Boolean(plus?.clanPro || plus?.isSponsor)
      const owner: BarTab<Tab>[] = canUseOwnerPanel(me) ? [{ id: 'owner', icon: 'dashboard', label: t.tabs.owner }] : []
      const clanTabs: BarTab<Tab>[] = [
        { id: 'war', icon: 'swords', label: t.tabs.war },
        ...(config?.event === 'halloween' ? [{ id: 'halloween' as Tab, icon: 'gift' as IconName, label: t.tabs.halloween }] : []),
        { id: 'hq', icon: 'crown', label: t.tabs.hq, badge: plus && !hasPro ? 'pro' : undefined },
        { id: 'roster', icon: 'users', label: t.tabs.roster },
        { id: 'rating', icon: 'trophy', label: t.tabs.rating },
        { id: 'more', icon: 'gear', label: t.tabs.more },
        ...owner,
      ]
      const playerTabs: BarTab<Tab>[] = [
        { id: 'profile', icon: 'user', label: t.tabs.profile, badge: questsSeen ? undefined : 'dot' },
        { id: 'decks', icon: 'cards', label: t.tabs.decks },
        { id: 'trainer', icon: 'target', label: t.tabs.trainer, badge: plus && !hasPlus ? 'plus' : undefined },
        { id: 'play', icon: 'ticket', label: t.tabs.play },
        { id: 'more', icon: 'gear', label: t.tabs.more },
        ...owner,
      ]
      // Точка на переключателе - в другом режиме тебя ждут: в клане - твои атаки
      // на сегодня, у игрока - задания дня, которые сегодня ещё не смотрели
      const modeDot = mode === 'player'
        ? data.periodType !== 'training' && Boolean(myRow) && (myRow?.decksUsedToday ?? 0) < 4
        : !questsSeen

      return (
        <>
          <main className="with-tabbar">
            {/* Плашка на всех вкладках, а не только на клановой: забыть, что смотришь
                чужой клан, проще всего именно уйдя с первого экрана. */}
            {data.viewingAsAdmin && (
              <div className="admin-banner">
                <span className="admin-banner-text">
                  <Icon name="eye" size={15} /> {data.clanName}
                  {data.adminReadOnly && <span className="muted small"> · {t.admin.readOnly}</span>}
                </span>
                <button
                  className="btn-mini"
                  onClick={() => { haptic('light'); adminClan.leave(); window.location.reload() }}
                >
                  {t.admin.exit}
                </button>
              </div>
            )}
            {tab !== 'creatorChallenge' && tab !== 'owner' && <ModeHeader mode={mode} plus={plus} />}
            {tab === 'war' && (
              <div className="fade-in">
                <MenuChangedNotice />
                <UpdateNotice />
                <WarHeader status={data} canManage={canManage} onOpenSettings={() => { haptic('light'); setSettingsOpen(true) }} />
                {config?.event === 'halloween' && <HalloweenBanner />}
                {/* Бот не в чате клана - главный канал не работает: лидеру кнопка «добавить», остальным «попросить» */}
                {data.hasChat === false && !data.viewingAsAdmin && (
                  <ConnectChatCard clanTag={data.clanTag} isLeader={Boolean(data.isClanLeader)} />
                )}
                {/* Личный призыв — первым делом: «ты не доиграл» цепляет сильнее общих цифр.
                    Остальное личное (что нового, опрос, «позови друга») живёт во вкладке «Я»:
                    здесь только клан. */}
                <MyActionBanner status={data} />
                <StatsStrip stats={data.stats} />
                {/* Действие вперёд наблюдения: пока глава читает цифры, лентяи не отыграют.
                    Кнопка первой, чтобы пнуть можно было не пролистывая экран. */}
                {canManage && data.periodType !== 'training' && (
                  <NudgeButton notPlayedCount={notFinished} />
                )}
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <RaceCard race={data.race} periodType={data.periodType} />
                {/* Разведка стоит вплотную к таблице гонки: таблица говорит, кто впереди
                    сейчас, разведка — чего эти кланы стоят вообще. Порознь они не работают. */}
                <ScoutCard />
                <WarLogCard log={data.warLog} />
                <WarJournalCard />
                <InsightsCard insights={data.insights} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                {/* Автонапоминания перенесены в ⚙️ «Уведомления» (шестерёнка в шапке) —
                    там же вкл/выкл, канал, часы и время окончания КВ. */}
              </div>
            )}
            {tab === 'hq' && <ClanHqView />}
            {tab === 'roster' && (
              <div className="fade-in">
                {canManage && (
                  <InviteUnlinkedCard unlinked={data.players.filter(p => !p.isLinked).length} hasChat={data.hasChat !== false} />
                )}
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} kingTag={king?.playerTag} canManage={canManage} />
              </div>
            )}
            {tab === 'rating' && (
              <div className="fade-in">
                <ClanWorldRankCard />
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} periodType={data.periodType} warLog={data.warLog ?? []} canManage={canManage} />
                <WorldTopEntry />
              </div>
            )}
            {tab === 'halloween' && <HalloweenView />}
            {tab === 'profile' && (
              <div className="fade-in">
                {/* Задания дня - первыми: повод зайти сегодня, а не когда-нибудь */}
                {!data.viewingAsAdmin && <QuestsCard />}
                {/* Личное, что раньше стояло среди клановых карточек войны */}
                <WhatsNewCard />
                {iFinishedToday && !data.viewingAsAdmin && <InviteMomentCard />}
                {!data.viewingAsAdmin && <FeedbackCard />}
                <MyStatsView defaultSection={startParam === 'review' || startToMatches ? 'battles' : 'clan'}
                  battlesView={START_BATTLES_VIEW} openMatchId={startMatchId} />
                {/* Мягкая реклама - под своей статистикой, а не над ней: сначала то, ради чего пришёл */}
                <div style={{ height: 10 }} />
                <ChannelCard dismissKey="me" />
              </div>
            )}
            {tab === 'decks' && <DecksHub initial={decksInitial} />}
            {tab === 'trainer' && <TrainerHub />}
            {tab === 'play' && <PlayHub key={playKey} initial={playInitial} />}
            {tab === 'more' && (
              <MoreView
                canManage={canManage}
                isLeader={Boolean(data.isClanLeader)}
                onOpenNotifications={() => { haptic('light'); setSettingsOpen(true) }}
                config={config}
                onConfigChanged={loadConfig}
                showStudio={canUseStudio(me)}
                hide={IN_BAR}
              />
            )}
            {tab === 'creatorChallenge' && startChallengeCode && (
              <CreatorChallengeScreen code={startChallengeCode} onBack={() => setTab(mode === 'player' ? 'profile' : 'war')} />
            )}
            {tab === 'owner' && canUseOwnerPanel(me) && (
              <div className="fade-in">
                <OwnerPanel me={me} />
              </div>
            )}
          </main>

          <TabBar tabs={mode === 'clan' ? clanTabs : playerTabs} active={tab} onSelect={switchTab}
            mode={mode} onToggleMode={toggleMode} modeDot={modeDot} />

          {settingsOpen && canManage && (
            <NotificationSettingsView onClose={() => setSettingsOpen(false)} />
          )}
        </>
      )
    }
    case 'guest': {
      const { data, myPlayerTag } = state
      const king = weekKing(data.players, data.warLog, data.periodType)

      const tabs: BarTab<Tab>[] = [
        { id: 'war', icon: 'castle', label: t.tabs.clan },
        { id: 'profile', icon: 'user', label: t.tabs.me },
        { id: 'play', icon: 'ticket', label: t.tabs.play },
        { id: 'search', icon: 'search', label: t.tabs.search },
        { id: 'more', icon: 'gear', label: t.tabs.more },
      ]
      // Гость со ссылкой на вкладку режима игрока, которой у него нет, - на клан
      const guestTab: Tab = tabs.some(x => x.id === tab) || tab === 'creatorChallenge' ? tab : 'war'

      return (
        <>
          <main className="with-tabbar">
            <div className="guest-banner">
              <span className="muted small">{t.guest.guestBanner}</span>
              <button className="btn-mini" onClick={exitGuest}>{t.guest.exit}</button>
            </div>

            {guestTab === 'war' && (
              <div className="fade-in">
                <UpdateNotice />
                <ClanSectionTabs value={clanSection} onChange={next => { haptic('light'); setClanSection(next) }} t={t} />
              </div>
            )}
            {guestTab === 'war' && clanSection === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} />
                {/* Тот же порядок, что и у своих, за вычетом того, чего гостю не положено:
                    кнопки пинка (он не управляет кланом) и дисциплины: она называет конкретных
                    людей, которые не доигрывают, и посторонним её показывать незачем. */}
                <StatsStrip stats={data.stats} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <WarLogCard log={data.warLog} />
                <InsightsCard insights={data.insights} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                <div style={{ height: 12 }} />
                <LeaderCtaCard />
              </div>
            )}
            {guestTab === 'war' && clanSection === 'roster' && (
              <div className="fade-in">
                <PlayerList players={data.players} myPlayerTag={myPlayerTag} kingTag={king?.playerTag} />
              </div>
            )}
            {guestTab === 'war' && clanSection === 'rating' && (
              <div className="fade-in">
                <Leaderboard players={data.players} myPlayerTag={myPlayerTag} periodType={data.periodType} warLog={data.warLog ?? []} />
                <WorldTopEntry />
              </div>
            )}
            {guestTab === 'profile' && (
              <div className="fade-in">
                <GuestMyStats data={data} myPlayerTag={myPlayerTag} />
              </div>
            )}
            {guestTab === 'play' && <PlayHub key={playKey} initial={playInitial} />}
            {guestTab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {guestTab === 'more' && (
              // Гость клан не настраивает: уведомления и биржа лидера ему недоступны
              <MoreView
                canManage={false}
                isLeader={false}
                onOpenNotifications={() => {}}
                config={config}
                onConfigChanged={loadConfig}
                showStudio={canUseStudio(me)}
              />
            )}
            {guestTab === 'creatorChallenge' && startChallengeCode && (
              <CreatorChallengeScreen code={startChallengeCode} onBack={() => setTab('war')} />
            )}
          </main>

          <TabBar tabs={tabs} active={guestTab} onSelect={switchTab} />
        </>
      )
    }
  }
}
