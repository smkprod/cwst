import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { MyStats } from '../types'
import { fmt } from '../lib/format'
import { haptic, shareToTelegram } from '../lib/telegram'

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: MyStats }

const PERF_META: Record<string, { emoji: string; cls: string }> = {
  'топ': { emoji: '🔥', cls: 'perf-top' },
  'выше среднего': { emoji: '💪', cls: 'perf-above' },
  'средне': { emoji: '🙂', cls: 'perf-mid' },
  'ниже среднего': { emoji: '😴', cls: 'perf-below' },
}

export function MyStatsView() {
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    api.getMyStats()
      .then(data => setState({ kind: 'ready', data }))
      .catch(e => setState({
        kind: 'error',
        message: e instanceof ApiError && e.code === 'not_in_current_war'
          ? 'Тебя нет в составе текущей войны'
          : 'Не удалось загрузить статистику',
      }))
  }, [])

  if (state.kind === 'loading') {
    return <div className="center"><div className="spinner" /></div>
  }
  if (state.kind === 'error') {
    return <p className="center muted">{state.message}</p>
  }

  const s = state.data
  const perf = PERF_META[s.performanceLabel] ?? { emoji: '🙂', cls: 'perf-mid' }
  const decksPct = Math.round((s.decksUsedToday / 4) * 100)
  const vsClan = s.clanAvgFamePerAttack > 0
    ? Math.round((s.avgFamePerAttack / s.clanAvgFamePerAttack - 1) * 100)
    : 0

  const share = () => {
    haptic('medium')
    shareToTelegram(
      `⚔️ Мои результаты в Clan War (${s.clanName}):\n` +
      `🏅 Слава: ${fmt(s.fame)} (#${s.rank} в клане)\n` +
      `⚡ ${Math.round(s.avgFamePerAttack)} славы за атаку\n` +
      `🔮 Прогноз недели: ${fmt(s.projectedWeekFame)}\n` +
      (s.season ? `🗓 За сезон: ${fmt(s.season.totalFame)} (#${s.season.rank})\n` : '') +
      `Мой вклад: ${s.contributionPercent}% славы клана 💪`,
    )
  }

  return (
    <div>
      <div className="me-header">
        <div className="me-title-row">
          <h2 className="me-name">{s.name}</h2>
          <span className={`perf-chip ${perf.cls}`}>{perf.emoji} {s.performanceLabel}</span>
        </div>
        <p className="muted small">{s.clanName} · место #{s.rank} из {s.clanSize}</p>
      </div>

      {/* Кольцо вклада */}
      <div className="card me-ring-card">
        <ContributionRing percent={s.contributionPercent} />
        <div className="ring-side">
          <div className="ring-fact">
            <span className="ring-fact-value">{fmt(s.fame)}</span>
            <span className="ring-fact-label">🏅 слава за неделю</span>
          </div>
          <div className="ring-fact">
            <span className="ring-fact-value">{s.decksUsedToday}/4</span>
            <span className="ring-fact-label">🃏 колоды сегодня</span>
            <div className="mini-track"><div className="mini-fill" style={{ width: `${decksPct}%` }} /></div>
          </div>
        </div>
      </div>

      <div className="me-grid">
        <div className="card me-stat">
          <span className="me-stat-value">{s.avgFamePerAttack > 0 ? Math.round(s.avgFamePerAttack) : '—'}</span>
          <span className="me-stat-label">слава / атака</span>
          {s.avgFamePerAttack > 0 && vsClan !== 0 && (
            <span className={`me-stat-delta ${vsClan > 0 ? 'delta-up' : 'delta-down'}`}>
              {vsClan > 0 ? '▲' : '▼'} {Math.abs(vsClan)}% от среднего по клану
            </span>
          )}
        </div>
        <div className="card me-stat">
          <span className="me-stat-value">{s.decksUsed}</span>
          <span className="me-stat-label">атак за неделю</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(s.projectedDayFame)}</span>
          <span className="me-stat-label">🔮 прогноз на день</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(s.projectedWeekFame)}</span>
          <span className="me-stat-label">🔮 прогноз недели</span>
        </div>
      </div>

      {s.season && (
        <div className="card season-card">
          <div className="card-title-row">
            <div className="card-title">🗓 Сезон #{s.season.seasonId}</div>
            <span className="muted small">недель: {s.season.weeksTracked}</span>
          </div>
          <div className="season-grid">
            <div className="season-cell">
              <span className="season-value">{fmt(s.season.totalFame)}</span>
              <span className="season-label">🏅 слава за сезон</span>
            </div>
            <div className="season-cell">
              <span className="season-value">#{s.season.rank}</span>
              <span className="season-label">из {s.season.clanSize} в зачёте</span>
            </div>
            <div className="season-cell">
              <span className="season-value">{fmt(s.season.bestWeekFame)}</span>
              <span className="season-label">⚡ лучшая неделя</span>
            </div>
            <div className="season-cell">
              <span className="season-value">{s.season.weeksParticipated}/{s.season.weeksTracked}</span>
              <span className="season-label">недель участвовал</span>
            </div>
          </div>
        </div>
      )}

      <button className="btn btn-share" onClick={share}>
        📤 Поделиться результатами
      </button>
    </div>
  )
}

/** SVG-кольцо: % вклада игрока в славу клана. */
function ContributionRing({ percent }: { percent: number }) {
  const r = 52
  const c = 2 * Math.PI * r
  const filled = Math.min(100, Math.max(0, percent))
  return (
    <div className="ring-wrap">
      <svg viewBox="0 0 120 120" className="ring-svg" aria-label={`Вклад ${percent}%`}>
        <circle cx="60" cy="60" r={r} className="ring-bg" />
        <circle
          cx="60" cy="60" r={r}
          className="ring-fg"
          strokeDasharray={`${(filled / 100) * c} ${c}`}
          transform="rotate(-90 60 60)"
        />
      </svg>
      <div className="ring-center">
        <span className="ring-pct">{percent}%</span>
        <span className="ring-caption">вклад в клан</span>
      </div>
    </div>
  )
}
