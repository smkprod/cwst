import { useEffect, useState, useCallback, useRef } from 'react'
import { Icon, type IconName } from './components/ui/Icon'
import { IconTile } from './components/ui/Section'
import { api, ApiError, adminClan } from './lib/api'
import { askDmOnce, haptic, startChallengeCode, startMatchId, startParam, startToMatches } from './lib/telegram'
import { ChallengeView, CreatorChallengeScreen } from './components/ChallengeView'
import { DuelView } from './components/DuelView'
import { afterPromo, OPEN_CHALLENGE } from './lib/promo'
import { usePlusSheet } from './lib/plusSheet'
import { useT, type Translations } from './lib/i18n'
import type { AppConfig, AppTab, ClanStatus, ServiceIdentity } from './types'
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
import { HallOfFame } from './components/HallOfFame'
import { PlayerSearchView } from './components/PlayerSearchView'
import { TournamentView } from './components/TournamentView'
import { ClanlessView, type SoloReason } from './components/ClanlessView'
import { GuestEntry } from './components/GuestEntry'
import { GuestMyStats } from './components/GuestMyStats'
import { LeaderCtaCard } from './components/LeaderCtaCard'
import { MyActionBanner } from './components/MyActionBanner'
import { SplashScreen } from './components/SplashScreen'
import { MoreView } from './components/MoreView'
import { MenuChangedNotice } from './components/MenuChangedNotice'
import { UpdateNotice } from './components/UpdateNotice'
import { DisciplineCard } from './components/DisciplineCard'
import { ScoutCard } from './components/ScoutCard'
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
 * Вкладок было до семи: война, рейтинг, я, поиск, турнир, биржа, панель. Ежедневный
 * экран стоял в одном ряду с тем, что открывают раз в месяц. Осталось четыре, а всё
 * редкое собрано в «Ещё». Панель владельца — пятая и только у владельца: она невидима
 * для остальных, так что места в баре ни у кого не занимает.
 */
type Tab = 'clan' | 'me' | 'hall' | 'tournament' | 'search' | 'more' | 'challenge' | 'duel' | 'owner' | 'creatorChallenge'

/** Набор вкладок, пока сервер не ответил. Совпадает с умолчанием на сервере. */
const DEFAULT_TABS: AppTab[] = ['clan', 'me', 'hall', 'search', 'more']

/** Как выглядит каждая вкладка. Ключи совпадают с теми, что присылает сервер. */
const TAB_LOOKS = (t: Translations): Record<AppTab, { icon: IconName; label: string }> => ({
  clan: { icon: 'castle', label: t.tabs.clan },
  me: { icon: 'user', label: t.tabs.me },
  hall: { icon: 'columns', label: t.tabs.hall },
  tournament: { icon: 'trophy', label: t.tabs.tournament },
  search: { icon: 'search', label: t.tabs.search },
  more: { icon: 'gear', label: t.tabs.more },
  challenge: { icon: 'ticket', label: t.tabs.challenge },
  duel: { icon: 'swords', label: t.tabs.duel },
})

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
 * Куда вести по параметру запуска из бота: «Открыть разбор» — во вкладку «Я» на
 * разбор, «/meta» — в мировой топ, «/plus» — в окно Плюса, ch_<код> — в челлендж
 * блогера (отдельный экран вне бара). Без параметра — как раньше.
 */
const START_TAB: Tab = startChallengeCode ? 'creatorChallenge' : startParam === 'challenge' ? 'challenge' : startParam === 'duel' ? 'duel' : startParam === 'review' || startToMatches ? 'me' : startParam === 'meta' ? 'more' : 'clan'

/** «Разбор» из бота — разбор за 30 дней, трекер и «Все бои» — история. */
const START_BATTLES_VIEW = startParam === 'review' ? 'review' : 'history'

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })

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
  // Кнопка «К челленджу» из окна-анонса
  useEffect(() => {
    const go = () => setTab('challenge')
    window.addEventListener(OPEN_CHALLENGE, go)
    return () => window.removeEventListener(OPEN_CHALLENGE, go)
  }, [])
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
    haptic('light')
    setTab(next)
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
            setTab('me')
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
            initialTab={startChallengeCode ? 'creatorChallenge' : startParam === 'meta' ? 'meta' : startParam === 'challenge' ? 'challenge' : startParam === 'duel' ? 'duel' : 'me'}
            challengeCode={startChallengeCode}
            battlesView={START_BATTLES_VIEW} openMatchId={startMatchId}
            showChallenge={Boolean(config?.tabs.includes('challenge')) || startParam === 'challenge'}
            showDuel={Boolean(config?.tabs.includes('duel')) || startParam === 'duel'}
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


      // Король недели считается один раз на оба экрана — иначе состав и рейтинг
      // однажды разойдутся в том, кого короновать.
      const king = weekKing(data.players, data.warLog, data.periodType)

      // Панель владельца всегда последней и всегда вне настраиваемого набора:
      // выключить её из панели значило бы потерять доступ к самой панели. Студии в
      // баре больше нет: шесть-семь пунктов не помещались, и она переехала в «Ещё».
      const tabs = [
        ...(config?.tabs ?? DEFAULT_TABS).map(id => ({ id: id as Tab, ...TAB_LOOKS(t)[id] })),
        ...(canUseOwnerPanel(me) ? [{ id: 'owner' as Tab, icon: 'dashboard' as IconName, label: t.tabs.owner }] : []),
      ]

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
            {tab === 'clan' && (
              <div className="fade-in">
                <MenuChangedNotice />
                <UpdateNotice />
                <ClanSectionTabs value={clanSection} onChange={next => { haptic('light'); setClanSection(next) }} t={t} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} canManage={canManage} onOpenSettings={() => { haptic('light'); setSettingsOpen(true) }} />
                {/* Личный призыв — первым делом: «ты не доиграл» цепляет сильнее общих цифр */}
                <MyActionBanner status={data} />
                {/* Что изменилось лично у тебя с прошлого захода (сама решает, показываться ли) */}
                <WhatsNewCard />
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
                <DisciplineCard />
                <WarLogCard log={data.warLog} />
                <WarJournalCard />
                <InsightsCard insights={data.insights} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                {/* Автонапоминания перенесены в ⚙️ «Уведомления» (шестерёнка в шапке) —
                    там же вкл/выкл, канал, часы и время окончания КВ. */}
              </div>
            )}
            {tab === 'clan' && clanSection === 'roster' && (
              <div className="fade-in">
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} kingTag={king?.playerTag} canManage={canManage} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'rating' && (
              <div className="fade-in">
                <ClanWorldRankCard />
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} periodType={data.periodType} warLog={data.warLog ?? []} canManage={canManage} />
                <WorldTopEntry />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <MyStatsView defaultSection={startParam === 'review' || startToMatches ? 'battles' : 'clan'}
                  battlesView={START_BATTLES_VIEW} openMatchId={startMatchId} />
              </div>
            )}
            {tab === 'tournament' && (
              <div className="fade-in">
                <TournamentView />
              </div>
            )}
            {tab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {tab === 'more' && (
              <MoreView
                canManage={canManage}
                isLeader={Boolean(data.isClanLeader)}
                initialSection={startParam === 'meta' ? 'worldTop' : null}
                onOpenNotifications={() => { haptic('light'); setSettingsOpen(true) }}
                config={config}
                onConfigChanged={loadConfig}
                showStudio={canUseStudio(me)}
              />
            )}
            {tab === 'hall' && (
              <HallOfFame config={config} onConfigChanged={loadConfig} />
            )}
            {tab === 'challenge' && <ChallengeView />}
            {tab === 'duel' && <DuelView />}
            {tab === 'creatorChallenge' && startChallengeCode && (
              <CreatorChallengeScreen code={startChallengeCode} onBack={() => setTab(tabs[0].id)} />
            )}
            {tab === 'owner' && canUseOwnerPanel(me) && (
              <div className="fade-in">
                <OwnerPanel me={me} />
              </div>
            )}
          </main>

          <nav className="tabbar" role="tablist">
            {tabs.map(tb => (
              <button
                key={tb.id}
                role="tab"
                aria-selected={tab === tb.id}
                className={`tab ${tab === tb.id ? 'tab-active' : ''}`}
                onClick={() => switchTab(tb.id)}
              >
                <span className="tab-icon"><Icon name={tb.icon} size={20} /></span>
                <span className="tab-label">{tb.label}</span>
              </button>
            ))}
          </nav>

          {settingsOpen && canManage && (
            <NotificationSettingsView onClose={() => setSettingsOpen(false)} />
          )}
        </>
      )
    }
    case 'guest': {
      const { data, myPlayerTag } = state
      const king = weekKing(data.players, data.warLog, data.periodType)

      const tabs: { id: Tab; icon: IconName; label: string }[] = [
        { id: 'clan', icon: 'castle', label: t.tabs.clan },
        { id: 'me', icon: 'user', label: t.tabs.me },
        { id: 'tournament', icon: 'trophy', label: t.tabs.tournament },
        { id: 'search', icon: 'search', label: t.tabs.search },
        { id: 'more', icon: 'gear', label: t.tabs.more },
      ]

      return (
        <>
          <main className="with-tabbar">
            <div className="guest-banner">
              <span className="muted small">{t.guest.guestBanner}</span>
              <button className="btn-mini" onClick={exitGuest}>{t.guest.exit}</button>
            </div>

            {tab === 'clan' && (
              <div className="fade-in">
                <MenuChangedNotice />
                <UpdateNotice />
                <ClanSectionTabs value={clanSection} onChange={next => { haptic('light'); setClanSection(next) }} t={t} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'war' && (
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
            {tab === 'clan' && clanSection === 'roster' && (
              <div className="fade-in">
                <PlayerList players={data.players} myPlayerTag={myPlayerTag} kingTag={king?.playerTag} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'rating' && (
              <div className="fade-in">
                <Leaderboard players={data.players} myPlayerTag={myPlayerTag} periodType={data.periodType} warLog={data.warLog ?? []} />
                <WorldTopEntry />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <GuestMyStats data={data} myPlayerTag={myPlayerTag} />
              </div>
            )}
            {tab === 'tournament' && (
              <div className="fade-in">
                <TournamentView />
              </div>
            )}
            {tab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {tab === 'more' && (
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
            {tab === 'creatorChallenge' && startChallengeCode && (
              <CreatorChallengeScreen code={startChallengeCode} onBack={() => setTab('clan')} />
            )}
          </main>

          <nav className="tabbar" role="tablist">
            {tabs.map(tb => (
              <button
                key={tb.id}
                role="tab"
                aria-selected={tab === tb.id}
                className={`tab ${tab === tb.id ? 'tab-active' : ''}`}
                onClick={() => switchTab(tb.id)}
              >
                <span className="tab-icon"><Icon name={tb.icon} size={20} /></span>
                <span className="tab-label">{tb.label}</span>
              </button>
            ))}
          </nav>
        </>
      )
    }
  }
}
