import { useEffect, useState, useCallback, useRef } from 'react'
import { api, ApiError } from './lib/api'
import { haptic } from './lib/telegram'
import { useT, type Translations } from './lib/i18n'
import type { ClanStatus } from './types'
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
import { ClanlessView } from './components/ClanlessView'
import { GuestEntry } from './components/GuestEntry'
import { GuestMyStats } from './components/GuestMyStats'
import { LeaderCtaCard } from './components/LeaderCtaCard'
import { MyActionBanner } from './components/MyActionBanner'
import { SplashScreen } from './components/SplashScreen'
import { MoreView } from './components/MoreView'
import { MenuChangedNotice } from './components/MenuChangedNotice'
import { DisciplineCard } from './components/DisciplineCard'
import { ScoutCard } from './components/ScoutCard'
import { weekKing } from './lib/king'

type State =
  | { kind: 'loading' }
  | { kind: 'guestEntry' }
  | { kind: 'notInTelegram' }
  | { kind: 'clanless' }
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
type Tab = 'clan' | 'me' | 'search' | 'more' | 'owner'

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
        ⚔️ {t.tabs.war}
      </button>
      <button
        className={`clan-section ${value === 'roster' ? 'clan-section-on' : ''}`}
        onClick={() => onChange('roster')}
      >
        👥 {t.tabs.roster}
      </button>
      <button
        className={`clan-section ${value === 'rating' ? 'clan-section-on' : ''}`}
        onClick={() => onChange('rating')}
      >
        🏆 {t.tabs.rating}
      </button>
    </div>
  )
}

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [tab, setTab] = useState<Tab>('clan')
  const [clanSection, setClanSection] = useState<ClanSection>('war')
  const [settingsOpen, setSettingsOpen] = useState(false)
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
        setState({ kind: 'guestEntry' })
      } else if (e instanceof ApiError && e.code === 'clan_not_found') {
        setState({ kind: 'clanless' })
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
      return <ClanlessView />
    case 'notInTelegram':
      return (
        <div className="center">
          <p style={{ fontSize: 40, margin: 0 }}>🔒</p>
          <p><strong>{t.openViaTitle}</strong></p>
          <p className="muted small" style={{ maxWidth: 280, textAlign: 'center' }}>
            {t.openViaHint}
          </p>
        </div>
      )
    case 'error':
      return (
        <div className="center">
          <p className="muted">{state.message}</p>
          <button className="btn" onClick={load}>{t.retry}</button>
        </div>
      )
    case 'ready': {
      const { data } = state
      const isPro = data.plan === 'pro'
      const canManage = Boolean(data.isAdmin || data.isClanLeader)
      const notFinished = data.players.filter(p => p.status !== 'played').length

      const isProLeader = Boolean(data.isClanLeader) && data.plan === 'pro'

      // Король недели считается один раз на оба экрана — иначе состав и рейтинг
      // однажды разойдутся в том, кого короновать.
      const king = weekKing(data.players, data.warLog, data.periodType)

      const tabs: { id: Tab; icon: string; label: string }[] = [
        { id: 'clan', icon: '🏰', label: t.tabs.clan },
        { id: 'me', icon: '👤', label: t.tabs.me },
        { id: 'search', icon: '🔍', label: t.tabs.search },
        { id: 'more', icon: '⚙️', label: t.tabs.more },
        ...(data.isOwner ? [{ id: 'owner' as Tab, icon: '📊', label: t.tabs.owner }] : []),
      ]

      return (
        <>
          <main className="with-tabbar">
            {tab === 'clan' && (
              <div className="fade-in">
                <MenuChangedNotice />
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
                  <NudgeButton notPlayedCount={notFinished} isPro={isPro} />
                )}
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <RaceCard race={data.race} periodType={data.periodType} />
                {/* Разведка стоит вплотную к таблице гонки: таблица говорит, кто впереди
                    сейчас, разведка — чего эти кланы стоят вообще. Порознь они не работают. */}
                <ScoutCard plan={data.plan} />
                <DisciplineCard plan={data.plan} />
                <WarLogCard log={data.warLog} />
                <WarJournalCard />
                <InsightsCard insights={data.insights} plan={data.plan} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                {/* Автонапоминания перенесены в ⚙️ «Уведомления» (шестерёнка в шапке) —
                    там же вкл/выкл, канал, часы и время окончания КВ. */}
              </div>
            )}
            {tab === 'clan' && clanSection === 'roster' && (
              <div className="fade-in">
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} kingTag={king?.playerTag} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'rating' && (
              <div className="fade-in">
                <ClanWorldRankCard />
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} plan={data.plan} periodType={data.periodType} warLog={data.warLog ?? []} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <MyStatsView />
              </div>
            )}
            {tab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {tab === 'more' && (
              <MoreView
                plan={data.plan}
                canManage={canManage}
                isProLeader={isProLeader}
                onOpenNotifications={() => { haptic('light'); setSettingsOpen(true) }}
              />
            )}
            {tab === 'owner' && data.isOwner && (
              <div className="fade-in">
                <OwnerPanel />
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
                <span className="tab-icon">{tb.icon}</span>
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

      const tabs: { id: Tab; icon: string; label: string }[] = [
        { id: 'clan', icon: '🏰', label: t.tabs.clan },
        { id: 'me', icon: '👤', label: t.tabs.me },
        { id: 'search', icon: '🔍', label: t.tabs.search },
        { id: 'more', icon: '⚙️', label: t.tabs.more },
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
                <ClanSectionTabs value={clanSection} onChange={next => { haptic('light'); setClanSection(next) }} t={t} />
              </div>
            )}
            {tab === 'clan' && clanSection === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} />
                {/* Тот же порядок, что и у своих, за вычетом того, чего гостю не положено:
                    кнопки пинка (он не управляет кланом) и дисциплины (это Pro-ручка клана). */}
                <StatsStrip stats={data.stats} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <WarLogCard log={data.warLog} />
                <InsightsCard insights={data.insights} plan={data.plan} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
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
                <Leaderboard players={data.players} myPlayerTag={myPlayerTag} plan={data.plan} periodType={data.periodType} warLog={data.warLog ?? []} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <GuestMyStats data={data} myPlayerTag={myPlayerTag} />
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
                plan={data.plan}
                canManage={false}
                isProLeader={false}
                onOpenNotifications={() => {}}
              />
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
                <span className="tab-icon">{tb.icon}</span>
                <span className="tab-label">{tb.label}</span>
              </button>
            ))}
          </nav>
        </>
      )
    }
  }
}
