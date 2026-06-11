import { useEffect, useState, useCallback } from 'react'
import { api, ApiError } from './lib/api'
import { haptic } from './lib/telegram'
import type { ClanStatus } from './types'
import { WarHeader } from './components/WarHeader'
import { ForecastCard } from './components/ForecastCard'
import { StatsStrip } from './components/StatsStrip'
import { PlayerList } from './components/PlayerList'
import { Leaderboard } from './components/Leaderboard'
import { HistoryCard } from './components/HistoryCard'
import { MyStatsView } from './components/MyStatsView'
import { NudgeButton } from './components/NudgeButton'
import { ReminderCard } from './components/ReminderCard'
import { OwnerPanel } from './components/OwnerPanel'
import { LinkPrompt } from './components/LinkPrompt'

type State =
  | { kind: 'loading' }
  | { kind: 'notLinked' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: ClanStatus }

type Tab = 'war' | 'rating' | 'me' | 'owner'

export default function App() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [tab, setTab] = useState<Tab>('war')

  const load = useCallback(async () => {
    try {
      const data = await api.getMyClanStatus()
      setState({ kind: 'ready', data })
    } catch (e) {
      if (e instanceof ApiError && e.code === 'player_not_linked') {
        setState({ kind: 'notLinked' })
      } else {
        setState({ kind: 'error', message: e instanceof Error ? e.message : 'Ошибка сети' })
      }
    }
  }, [])

  useEffect(() => {
    load()
    const id = setInterval(load, 60_000) // авто-обновление раз в минуту
    return () => clearInterval(id)
  }, [load])

  const switchTab = (t: Tab) => {
    haptic('light')
    setTab(t)
  }

  switch (state.kind) {
    case 'loading':
      return (
        <div className="center">
          <div className="spinner" aria-label="Загрузка" />
          <p className="muted">Загружаю статус войны…</p>
        </div>
      )
    case 'notLinked':
      return <LinkPrompt />
    case 'error':
      return (
        <div className="center">
          <p className="muted">{state.message}</p>
          <button className="btn" onClick={load}>Повторить</button>
        </div>
      )
    case 'ready': {
      const { data } = state
      const isPro = data.plan === 'pro'
      const notFinished = data.players.filter(p => p.status !== 'played').length

      const tabs: { id: Tab; icon: string; label: string }[] = [
        { id: 'war', icon: '⚔️', label: 'Война' },
        { id: 'rating', icon: '🏆', label: 'Рейтинг' },
        { id: 'me', icon: '👤', label: 'Я' },
        ...(data.isOwner ? [{ id: 'owner' as Tab, icon: '⚙️', label: 'Панель' }] : []),
      ]

      return (
        <>
          <main className="with-tabbar">
            {tab === 'war' && (
              <div className="fade-in">
                <WarHeader status={data} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <StatsStrip stats={data.stats} />
                {data.isAdmin && isPro && data.periodType !== 'training' && (
                  <NudgeButton notPlayedCount={notFinished} />
                )}
                {data.isAdmin && (
                  <ReminderCard initialHours={data.reminderHoursBeforeEnd ?? 3} />
                )}
                <PlayerList players={data.players} myPlayerTag={data.myPlayerTag} />
              </div>
            )}
            {tab === 'rating' && (
              <div className="fade-in">
                <Leaderboard players={data.players} myPlayerTag={data.myPlayerTag} plan={data.plan} />
                <div style={{ height: 12 }} />
                <HistoryCard plan={data.plan} />
              </div>
            )}
            {tab === 'me' && (
              <div className="fade-in">
                <MyStatsView />
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
