import { useEffect, useState, useCallback } from 'react'
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
import { PlayerList } from './components/PlayerList'
import { Leaderboard } from './components/Leaderboard'
import { MyStatsView } from './components/MyStatsView'
import { NudgeButton } from './components/NudgeButton'
import { ReminderCard } from './components/ReminderCard'
import { AboutCard } from './components/AboutCard'
import { OwnerPanel } from './components/OwnerPanel'
import { LinkPrompt } from './components/LinkPrompt'
import { PlayerSearchView } from './components/PlayerSearchView'
import { RecruitBoard } from './components/RecruitBoard'
import { RecruitToggle } from './components/RecruitToggle'
import { ClanlessView } from './components/ClanlessView'
import { CommunityCard } from './components/CommunityCard'

type State =
  | { kind: 'loading' }
  | { kind: 'notLinked' }
  | { kind: 'notInTelegram' }
  | { kind: 'clanless' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: ClanStatus }

type Tab = 'war' | 'rating' | 'me' | 'search' | 'owner' | 'recruit'

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [tab, setTab] = useState<Tab>('war')
  const { t } = useT()

  const load = useCallback(async () => {
    try {
      const data = await api.getMyClanStatus()
      setState({ kind: 'ready', data })
    } catch (e) {
      if (e instanceof ApiError && e.code === 'player_not_linked') {
        setState({ kind: 'notLinked' })
      } else if (e instanceof ApiError && e.code === 'clan_not_found') {
        setState({ kind: 'clanless' })
      } else if (e instanceof ApiError && (e.code === 'no_init_data' || e.code === 'bad_init_data')) {
        setState({ kind: 'notInTelegram' })
      } else if (e instanceof ApiError && e.status === 500) {
        setState({ kind: 'error', message: t.serverError })
      } else {
        setState({ kind: 'error', message: e instanceof Error && e.message ? e.message : t.networkError })
      }
    }
  }, [t])

  useEffect(() => {
    load()
    const id = setInterval(load, 60_000)
    return () => clearInterval(id)
  }, [load])

  const switchTab = (tab: Tab) => {
    haptic('light')
    setTab(tab)
  }

  switch (state.kind) {
    case 'loading':
      return (
        <div className="center">
          <div className="spinner" aria-label={t.loading} />
          <p className="muted">{t.loadingWar}</p>
        </div>
      )
    case 'notLinked':
      return <LinkPrompt />
    case 'clanless':
      return <main><ClanlessView /></main>
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
        ...(data.isClanLeader && data.plan === 'pro' ? [{ id: 'recruit' as Tab, icon: '👥', label: t.tabs.recruit }] : []),
        ...(data.isOwner ? [{ id: 'owner' as Tab, icon: '⚙️', label: t.tabs.owner }] : []),
      ]

      return (
        <>
          <main className="with-tabbar">
            {tab === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} />
                <InsightsCard insights={data.insights} plan={data.plan} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <WarLogCard log={data.warLog} />
                <StatsStrip stats={data.stats} />
                {canManage && data.periodType !== 'training' && (
                  <NudgeButton notPlayedCount={notFinished} isPro={isPro} />
                )}
                {canManage && (
                  <ReminderCard
                    initialHours={data.reminderHoursBeforeEnd ?? 3}
                    plan={data.plan}
                    linkedCount={data.players.filter(p => p.isLinked).length}
                  />
                )}
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} />
              </div>
            )}
            {tab === 'rating' && (
              <div className="fade-in">
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} plan={data.plan} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <MyStatsView />
                <div style={{ height: 12 }} />
                <RecruitToggle />
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
            {tabs.map(t => (
              <button
                key={t.id}
                role="tab"
                aria-selected={tab === t.id}
                className={`tab ${tab === t.id ? 'tab-active' : ''}`}
                onClick={() => switchTab(t.id)}
              >
                <span className="tab-icon">{t.icon}</span>
                <span className="tab-label">{t.label}</span>
              </button>
            ))}
          </nav>
        </>
      )
    }
  }
}
