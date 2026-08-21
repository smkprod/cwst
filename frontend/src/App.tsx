import { useEffect, useState, useCallback, useRef } from 'react'
import { api, ApiError } from './lib/api'
import { haptic } from './lib/telegram'
import { useT } from './lib/i18n'
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
import { AboutCard } from './components/AboutCard'
import { OwnerPanel } from './components/OwnerPanel'
import { LinkPrompt } from './components/LinkPrompt'
import { PlayerSearchView } from './components/PlayerSearchView'
import { RecruitBoard } from './components/RecruitBoard'
import { RecruitToggle } from './components/RecruitToggle'
import { ClanlessView } from './components/ClanlessView'
import { CommunityCard } from './components/CommunityCard'
import { GuestEntry } from './components/GuestEntry'
import { GuestMyStats } from './components/GuestMyStats'
import { TournamentView } from './components/TournamentView'
import { InviteCard } from './components/InviteCard'
import { LeaderCtaCard } from './components/LeaderCtaCard'
import { MyActionBanner } from './components/MyActionBanner'

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

type Tab = 'war' | 'rating' | 'me' | 'search' | 'owner' | 'recruit' | 'tournament'

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [tab, setTab] = useState<Tab>('war')
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
      return (
        <div className="center">
          <div className="spinner" aria-label={t.loading} />
          <p className="muted">{t.loadingWar}</p>
        </div>
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
      const canManage = data.isAdmin || data.isClanLeader
      const notFinished = data.players.filter(p => p.status !== 'played').length

      const tabs: { id: Tab; icon: string; label: string }[] = [
        { id: 'war', icon: '⚔️', label: t.tabs.war },
        { id: 'rating', icon: '🏆', label: t.tabs.rating },
        { id: 'me', icon: '👤', label: t.tabs.me },
        { id: 'search', icon: '🔍', label: t.tabs.search },
        { id: 'tournament', icon: '🥇', label: t.tabs.tournament },
        ...(data.isClanLeader && data.plan === 'pro' ? [{ id: 'recruit' as Tab, icon: '👥', label: t.tabs.recruit }] : []),
        ...(data.isOwner ? [{ id: 'owner' as Tab, icon: '⚙️', label: t.tabs.owner }] : []),
      ]

      return (
        <>
          <main className="with-tabbar">
            {tab === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} canManage={canManage} onOpenSettings={() => { haptic('light'); setSettingsOpen(true) }} />
                {/* Личный призыв — первым делом: «ты не доиграл» цепляет сильнее общих цифр */}
                <MyActionBanner status={data} />
                {/* Что изменилось лично у тебя с прошлого захода (сама решает, показываться ли) */}
                <WhatsNewCard />
                {/* Аналитика к аналитике: цифры → прогноз → инсайты, потом гонка и история */}
                <StatsStrip stats={data.stats} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <InsightsCard insights={data.insights} plan={data.plan} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <WarLogCard log={data.warLog} />
                {canManage && data.periodType !== 'training' && (
                  <NudgeButton notPlayedCount={notFinished} isPro={isPro} />
                )}
                {/* Автонапоминания перенесены в ⚙️ «Уведомления» (шестерёнка в шапке) —
                    там же вкл/выкл, канал, часы и время окончания КВ. */}
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} />
                <WarJournalCard />
              </div>
            )}
            {tab === 'rating' && (
              <div className="fade-in">
                <ClanWorldRankCard />
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} plan={data.plan} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <MyStatsView />
                <div style={{ height: 12 }} />
                <RecruitToggle />
                <div style={{ height: 12 }} />
                <InviteCard />
                <div style={{ height: 12 }} />
                <AboutCard plan={data.plan} />
                <div style={{ height: 12 }} />
                <CommunityCard />
              </div>
            )}
            {tab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {tab === 'tournament' && (
              <div className="fade-in">
                <TournamentView />
              </div>
            )}
            {tab === 'recruit' && data.isClanLeader && data.plan === 'pro' && (
              <div className="fade-in">
                <RecruitBoard />
              </div>
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

      const tabs: { id: Tab; icon: string; label: string }[] = [
        { id: 'war', icon: '⚔️', label: t.tabs.war },
        { id: 'rating', icon: '🏆', label: t.tabs.rating },
        { id: 'me', icon: '👤', label: t.tabs.me },
        { id: 'search', icon: '🔍', label: t.tabs.search },
        { id: 'tournament', icon: '🥇', label: t.tabs.tournament },
      ]

      return (
        <>
          <main className="with-tabbar">
            <div className="guest-banner">
              <span className="muted small">{t.guest.guestBanner}</span>
              <button className="btn-mini" onClick={exitGuest}>{t.guest.exit}</button>
            </div>

            {tab === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} />
                {/* Аналитика к аналитике: цифры → прогноз → инсайты, потом гонка и история */}
                <StatsStrip stats={data.stats} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <InsightsCard insights={data.insights} plan={data.plan} players={data.players} dayLogs={data.dayLogs ?? []} warLog={data.warLog ?? []} race={data.race ?? []} periodType={data.periodType} periodIndex={data.periodIndex} hoursLeft={data.hoursLeft} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <WarLogCard log={data.warLog} />
                <PlayerList players={data.players} myPlayerTag={myPlayerTag} />
                <div style={{ height: 12 }} />
                <LeaderCtaCard />
              </div>
            )}
            {tab === 'rating' && (
              <div className="fade-in">
                <Leaderboard players={data.players} myPlayerTag={myPlayerTag} plan={data.plan} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <GuestMyStats data={data} myPlayerTag={myPlayerTag} />
                <div style={{ height: 12 }} />
                <InviteCard />
                <div style={{ height: 12 }} />
                <AboutCard plan={data.plan} />
                <div style={{ height: 12 }} />
                <CommunityCard />
              </div>
            )}
            {tab === 'search' && (
              <div className="fade-in">
                <PlayerSearchView />
              </div>
            )}
            {tab === 'tournament' && (
              <div className="fade-in">
                <TournamentView />
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
        </>
      )
    }
  }
}
