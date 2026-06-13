import type { ClanForecast, ClanStats, ClanStatus } from '../types'
import { fmt } from '../lib/format'

const TREND_META: Record<ClanForecast['trend'], { icon: string; label: string; cls: string }> = {
  ahead: { icon: '📈', label: 'Идём выше темпа', cls: 'trend-ahead' },
  onPace: { icon: '➡️', label: 'Идём в графике', cls: 'trend-onpace' },
  behind: { icon: '📉', label: 'Отстаём от темпа', cls: 'trend-behind' },
}

interface Props {
  forecast: ClanForecast | null
  stats: ClanStats
  periodType: ClanStatus['periodType']
}

export function ForecastCard({ forecast, stats, periodType }: Props) {
  if (periodType === 'training') {
    return (
      <section className="card forecast-card">
        <div className="card-title">🔮 Прогноз</div>
        <p className="muted small">Идут тренировочные дни — прогноз появится с началом войны.</p>
      </section>
    )
  }

  if (forecast === null) {
    return (
      <section className="card forecast-card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">🔮 Прогноз клана</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">
          Сколько медалей клан наберёт к концу дня и недели, тренд и точность прогноза —
          доступно на тарифе Pro. 🔒
        </p>
      </section>
    )
  }

  const trend = TREND_META[forecast.trend]
  const dayGain = forecast.projectedDayFame - stats.totalFame
  const hasCI = forecast.projectedDayFameLow > 0 && forecast.projectedDayFameHigh > 0

  return (
    <section className="card forecast-card">
      <div className="card-title-row">
        <div className="card-title">🔮 Прогноз клана</div>
        <span className={`trend-chip ${trend.cls}`}>{trend.icon} {trend.label}</span>
      </div>

      <div className="forecast-numbers">
        {/* Primary: day fame */}
        <div className="forecast-block forecast-block-primary">
          <span className="forecast-label-small">🏅 к концу дня</span>
          <span className="forecast-value">{fmt(forecast.projectedDayFame)}</span>
          {dayGain > 0 && (
            <span className="forecast-delta">+{fmt(dayGain)} ожидаем</span>
          )}
          {hasCI && (
            <span className="forecast-ci muted small">
              {fmt(forecast.projectedDayFameLow)} – {fmt(forecast.projectedDayFameHigh)}
            </span>
          )}
        </div>

        {/* Secondary: week fame */}
        <div className="forecast-week-row">
          <span className="forecast-week-label muted small">🏆 к концу недели:</span>
          <span className="forecast-week-value">{fmt(forecast.projectedWeekFame)}</span>
          <span className="forecast-week-attacks muted small">
            атак осталось: ~{forecast.expectedRemainingAttacksToday}
          </span>
        </div>
      </div>

      <div className="confidence-row">
        <span className="confidence-label">Точность прогноза</span>
        <div className="confidence-track">
          <div
            className={`confidence-fill ${forecast.confidence >= 70 ? 'conf-high' : forecast.confidence >= 50 ? 'conf-mid' : 'conf-low'}`}
            style={{ width: `${forecast.confidence}%` }}
          />
        </div>
        <span className="confidence-pct">{forecast.confidence}%</span>
      </div>
    </section>
  )
}
