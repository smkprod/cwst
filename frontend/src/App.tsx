import { useEffect, useState, useCallback } from 'react'
import { api, ApiError } from './lib/api'
import { haptic } from './lib/telegram'
import type { ClanStatus } from './types'
import { WarHeader } from './components/WarHeader'
import { ForecastCard } from './components/ForecastCard'
import { InsightsCard } from './components/InsightsCard'
import { RaceCard } from './components/RaceCard'
import { StatsStrip } from './components/StatsStrip'
import { PlayerList } from './components/PlayerList'
import { Leaderboard } from './components/Leaderboard'
import { MyStatsView } from './components/MyStatsView'
import { NudgeButton } from './components/NudgeButton'
import { ReminderCard } from './components/ReminderCard'
import { OwnerPanel } from './components/OwnerPanel'
import { LinkPrompt } from './components/LinkPrompt'

type State =
  | { kind: 'loading' }
  | { kind: 'notLinked' }
  | { kind: 'notInTelegram' }
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
      } else if (e instanceof ApiError && (e.code === 'no_init_data' || e.code === 'bad_init_data')) {
        setState({ kind: 'notInTelegram' })
      } else if (e instanceof ApiError && e.status === 500) {
        setState({ kind: 'error', message: 'Ошибка сервера — проверьте логи API или попробуйте позже' })
      } else {
        setState({ kind: 'error', message: e instanceof Error && e.message ? e.message : 'Ошибка сети' })
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
    case 'notInTelegram':
      return (
        <div className="center">
          <p style={{ fontSize: 40, margin: 0 }}>🔒</p>
          <p><strong>Открой приложение через Telegram</strong></p>
          <p className="muted small" style={{ maxWidth: 280, textAlign: 'center' }}>
            Зайди в группу клана или в чат с ботом и открой Mini App кнопкой —
            при открытии по обычной ссылке Telegram не передаёт данные для входа.
          </p>
        </div>
      )
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
      const canManage = data.isAdmin || data.isClanLeader
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
                <InsightsCard insights={data.insights} plan={data.plan} />
                <RaceCard race={data.race} periodType={data.periodType} />
                <ForecastCard forecast={data.forecast} stats={data.stats} periodType={data.periodType} />
                <StatsStrip stats={data.stats} />
                {canManage && data.periodType !== 'training' && (
                  <NudgeButton notPlayedCount={notFinished} isPro={isPro} />
                )}
                {canManage && (
                  <ReminderCard initialHours={data.reminderHoursBeforeEnd ?? 3} />
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
