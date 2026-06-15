import type { ClanInsights, Plan } from '../types'
import { useT } from '../lib/i18n'

interface Props {
  insights: ClanInsights | null
  plan: Plan
}

export function InsightsCard({ insights, plan }: Props) {
  const { t } = useT()

  if (plan !== 'pro') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">{t.insights.title}</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">{t.insights.lockedNote}</p>
      </section>
    )
  }

  if (!insights) return null

  const { winChance, winChanceIfSlackersOut, topRivalName, healthScore, healthLabel, factors } = insights
  const drop = winChance !== null && winChanceIfSlackersOut !== null
    ? winChance - winChanceIfSlackersOut
    : 0

  return (
    <section className="card insights-card">
      <div className="card-title-row">
        <div className="card-title">{t.insights.title}</div>
        <span className="pro-chip">PRO</span>
      </div>

      {winChance !== null && (
        <div className="win-block">
          <div className="win-row">
            <span className={`win-value ${winChance >= 60 ? 'win-good' : winChance >= 40 ? 'win-mid' : 'win-bad'}`}>
              {winChance}%
            </span>
            <div className="win-info">
              <span className="win-caption">{t.insights.winCaption}</span>
              {topRivalName && <span className="muted small">{t.insights.topRival} {topRivalName}</span>}
            </div>
          </div>
          <div className="win-track">
            <div
              className={`win-fill ${winChance >= 60 ? 'fill-good' : winChance >= 40 ? 'fill-mid' : 'fill-bad'}`}
              style={{ width: `${winChance}%` }}
            />
          </div>
          {drop >= 5 && winChanceIfSlackersOut !== null && (
            <p className="win-warning">
              ⚠️ {t.insights.slackersWarn1}<strong>{winChanceIfSlackersOut}%</strong>{t.insights.slackersWarn2}
            </p>
          )}
        </div>
      )}

      <div className="health-block">
        <div className="health-head">
          <span className="health-score">
            🩺 {healthScore}<span className="muted">/100</span>
          </span>
          <span className={`health-label ${healthScore >= 75 ? 'win-good' : healthScore >= 50 ? 'win-mid' : 'win-bad'}`}>
            {healthLabel}
          </span>
        </div>
        <div className="health-factors">
          {factors.map(f => (
            <div key={f.name} className="health-factor">
              <span className="health-factor-name muted small">{f.name}</span>
              <div className="health-factor-track">
                <div
                  className={`health-factor-fill ${f.score >= 70 ? 'fill-good' : f.score >= 45 ? 'fill-mid' : 'fill-bad'}`}
                  style={{ width: `${Math.max(4, f.score)}%` }}
                />
              </div>
              <span className="health-factor-score small">{f.score}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
