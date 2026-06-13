import type { ClanInsights, Plan } from '../types'

interface Props {
  insights: ClanInsights | null
  plan: Plan
}

/** Pro-аналитика: шанс победы в гонке + здоровье клана. На Free — тизер с замком. */
export function InsightsCard({ insights, plan }: Props) {
  if (plan !== 'pro') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">🧠 Аналитика клана</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">
          Шанс победы в гонке в реальном времени, «здоровье» клана и DNA-профили игроков
          (кто тащит, а кто балласт) — доступно на тарифе Pro. 🔒
        </p>
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
        <div className="card-title">🧠 Аналитика клана</div>
        <span className="pro-chip">PRO</span>
      </div>

      {winChance !== null && (
        <div className="win-block">
          <div className="win-row">
            <span className={`win-value ${winChance >= 60 ? 'win-good' : winChance >= 40 ? 'win-mid' : 'win-bad'}`}>
              {winChance}%
            </span>
            <div className="win-info">
              <span className="win-caption">шанс победы в гонке</span>
              {topRivalName && <span className="muted small">главный соперник: {topRivalName}</span>}
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
              ⚠️ Если не доигравшие так и не сыграют — шанс упадёт до <strong>{winChanceIfSlackersOut}%</strong>
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
