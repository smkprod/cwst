import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { MyStats, PlayerHistory, PlayerWeekHistory } from '../types'
import { fmt, fmtShort } from '../lib/format'
import { haptic, shareToTelegram } from '../lib/telegram'
import { AchievementsCard } from './AchievementsCard'
import { useT, perfLabel, type Translations } from '../lib/i18n'

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
  const [history, setHistory] = useState<PlayerHistory | null>(null)
  const { t } = useT()

  useEffect(() => {
    api.getMyStats()
      .then(data => setState({ kind: 'ready', data }))
      .catch(e => setState({
        kind: 'error',
        message: e instanceof ApiError && e.code === 'not_in_current_war'
          ? t.me.notInWar
          : t.me.loadError,
      }))
  }, [t.me.notInWar, t.me.loadError])

  const playerTag = state.kind === 'ready' ? state.data.playerTag : null
  useEffect(() => {
    if (!playerTag) return
    api.getPlayerHistory(playerTag).then(setHistory).catch(() => setHistory(null))
  }, [playerTag])

  if (state.kind === 'loading') {
    return <div className="center"><div className="spinner" /></div>
  }
  if (state.kind === 'error') {
    return <p className="center muted">{state.message}</p>
  }

  const s = state.data
  const perf = PERF_META[s.performanceLabel] ?? { emoji: '🙂', cls: 'perf-mid' }
  const perfTranslated = perfLabel(s.performanceLabel, t)
  const decksPct = Math.round((s.decksUsedToday / 4) * 100)
  const vsClan = s.clanAvgFamePerAttack > 0
    ? Math.round((s.avgFamePerAttack / s.clanAvgFamePerAttack - 1) * 100)
    : 0

  const share = () => {
    haptic('medium')
    shareToTelegram(
      `${t.me.shareHeader} (${s.clanName}):\n` +
      `${t.me.shareFame} ${fmt(s.fame)} (#${s.rank} ${t.playerModal.rankInClan})\n` +
      `${t.me.shareAvg} ${Math.round(s.avgFamePerAttack)} ${t.me.medalsPerAttackUnit}\n` +
      `${t.me.shareWeekForecast} ${fmt(s.projectedWeekFame)}\n` +
      (s.season ? `${t.me.shareSeason} ${fmt(s.season.totalFame)} (#${s.season.rank})\n` : '') +
      `${t.me.shareContrib} ${s.contributionPercent}${t.me.shareContribSuffix}`,
    )
  }

  return (
    <div>
      <div className="me-header">
        <div className="me-title-row">
          <h2 className="me-name">{s.name}</h2>
          <span className={`perf-chip ${perf.cls}`}>{perf.emoji} {perfTranslated}</span>
        </div>
        <p className="muted small">{s.clanName} · {t.me.rankOf} #{s.rank} {t.me.of} {s.clanSize}</p>
      </div>

      <div className="card me-ring-card">
        <ContributionRing percent={s.contributionPercent} label={t.me.contrib} ariaLabel={t.me.contribAria} />
        <div className="ring-side">
          <div className="ring-fact">
            <span className="ring-fact-value">{fmt(s.fame)}</span>
            <span className="ring-fact-label">{t.me.weekMedals}</span>
          </div>
          <div className="ring-fact">
            <span className="ring-fact-value">{s.decksUsedToday}/4</span>
            <span className="ring-fact-label">{t.me.decksToday}</span>
            <div className="mini-track"><div className="mini-fill" style={{ width: `${decksPct}%` }} /></div>
          </div>
        </div>
      </div>

      <div className="me-grid">
        <div className="card me-stat">
          <span className="me-stat-value">{s.avgFamePerAttack > 0 ? Math.round(s.avgFamePerAttack) : '—'}</span>
          <span className="me-stat-label">{t.me.medalsPerAttack}</span>
          {s.avgFamePerAttack > 0 && vsClan !== 0 && (
            <span className={`me-stat-delta ${vsClan > 0 ? 'delta-up' : 'delta-down'}`}>
              {vsClan > 0 ? '▲' : '▼'} {Math.abs(vsClan)}% {t.me.vsClan}
            </span>
          )}
        </div>
        <div className="card me-stat">
          <span className="me-stat-value">{s.decksUsed}</span>
          <span className="me-stat-label">{t.me.weekAttacks}</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(s.projectedDayFame)}</span>
          <span className="me-stat-label">{t.me.dayForecast}</span>
        </div>
        <div className="card me-stat me-stat-accent">
          <span className="me-stat-value">{fmt(s.projectedWeekFame)}</span>
          <span className="me-stat-label">{t.me.weekForecast}</span>
        </div>
      </div>

      {history && history.weeks.length >= 2 && <WeeklyFameChart weeks={history.weeks} t={t} />}
      {history && history.weeks.length >= 2 && <VsClanChart weeks={history.weeks} t={t} />}

      {s.season && (
        <div className="card season-card">
          <div className="card-title-row">
            <div className="card-title">{t.me.seasonTitle}{s.season.seasonId}</div>
            <span className="muted small">{t.me.seasonWeeks} {s.season.weeksTracked}</span>
          </div>
          <div className="season-grid">
            <div className="season-cell">
              <span className="season-value">{fmt(s.season.totalFame)}</span>
              <span className="season-label">{t.me.seasonMedals}</span>
            </div>
            <div className="season-cell">
              <span className="season-value">#{s.season.rank}</span>
              <span className="season-label">{t.me.of} {s.season.clanSize} {t.me.seasonRankSuffix}</span>
            </div>
            <div className="season-cell">
              <span className="season-value">{fmt(s.season.bestWeekFame)}</span>
              <span className="season-label">{t.me.bestWeek}</span>
            </div>
            <div className="season-cell">
              <span className="season-value">{s.season.weeksParticipated}/{s.season.weeksTracked}</span>
              <span className="season-label">{t.me.weeksIn}</span>
            </div>
          </div>
        </div>
      )}

      <AchievementsCard />


      <button className="btn btn-share" onClick={share}>
        {t.me.share}
      </button>
    </div>
  )
}

function ContributionRing({ percent, label, ariaLabel }: { percent: number; label: string; ariaLabel: string }) {
  const r = 52
  const c = 2 * Math.PI * r
  const filled = Math.min(100, Math.max(0, percent))
  return (
    <div className="ring-wrap">
      <svg viewBox="0 0 120 120" className="ring-svg" aria-label={`${ariaLabel} ${percent}%`}>
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
        <span className="ring-caption">{label}</span>
      </div>
    </div>
  )
}

function WeeklyFameChart({ weeks, t }: { weeks: PlayerWeekHistory[]; t: Translations }) {
  const chronological = [...weeks].reverse()
  const maxFame = Math.max(...chronological.map(w => w.fame), 1)
  return (
    <section className="card">
      <div className="card-title" style={{ marginBottom: 10 }}>{t.me.weeklyFameTitle}</div>
      <div className="history-weeks">
        {chronological.map(w => (
          <div key={`${w.seasonId}-${w.sectionIndex}`} className="history-week">
            <div className="history-bar-wrap">
              <div
                className={`history-bar ${w.isColosseum ? 'history-bar-colosseum' : ''}`}
                style={{ height: `${Math.max(8, Math.round((w.fame / maxFame) * 100))}%` }}
                title={`${fmt(w.fame)} ${t.leaderboard.medals}`}
              />
            </div>
            <span className="history-fame">{fmtShort(w.fame)}</span>
            <span className="history-label">{w.isColosseum ? '🏟' : `н.${w.sectionIndex + 1}`}</span>
          </div>
        ))}
      </div>
    </section>
  )
}

function VsClanChart({ weeks, t }: { weeks: PlayerWeekHistory[]; t: Translations }) {
  const chronological = [...weeks].reverse()
  const maxVal = Math.max(...chronological.flatMap(w => [w.avgFamePerAttack, w.clanAvgFamePerAttack]), 1)
  return (
    <section className="card">
      <div className="card-title" style={{ marginBottom: 6 }}>{t.me.vsClanTitle}</div>
      <div className="cmp-legend">
        <span><span className="cmp-dot cmp-dot-mine" />{t.me.vsClanMineLegend}</span>
        <span><span className="cmp-dot cmp-dot-clan" />{t.me.vsClanClanLegend}</span>
      </div>
      <div className="cmp-weeks">
        {chronological.map(w => (
          <div key={`${w.seasonId}-${w.sectionIndex}`} className="cmp-week">
            <div className="cmp-bar-wrap">
              <div
                className="cmp-bar cmp-bar-mine"
                style={{ height: `${Math.max(4, Math.round((w.avgFamePerAttack / maxVal) * 100))}%` }}
                title={`${t.me.vsClanMineLegend}: ${w.avgFamePerAttack}`}
              />
              <div
                className="cmp-bar cmp-bar-clan"
                style={{ height: `${Math.max(4, Math.round((w.clanAvgFamePerAttack / maxVal) * 100))}%` }}
                title={`${t.me.vsClanClanLegend}: ${w.clanAvgFamePerAttack}`}
              />
            </div>
            <span className="history-label">{w.isColosseum ? '🏟' : `н.${w.sectionIndex + 1}`}</span>
          </div>
        ))}
      </div>
    </section>
  )
}
