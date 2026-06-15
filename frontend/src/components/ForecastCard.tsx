import type { ClanForecast, ClanStats, ClanStatus } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

const TREND_ICON: Record<ClanForecast['trend'], { icon: string; cls: string }> = {
  ahead: { icon: '📈', cls: 'trend-ahead' },
  onPace: { icon: '➡️', cls: 'trend-onpace' },
  behind: { icon: '📉', cls: 'trend-behind' },
}

interface Props {
  forecast: ClanForecast | null
  stats: ClanStats
  periodType: ClanStatus['periodType']
}

export function ForecastCard({ forecast, stats: _stats, periodType }: Props) {
  const { t } = useT()

  if (periodType === 'training') {
    return (
      <section className="card forecast-card">
        <div className="card-title">{t.forecast.titleSimple}</div>
        <p className="muted small">{t.forecast.trainingNote}</p>
      </section>
    )
  }

  if (forecast === null) {
    return (
      <section className="card forecast-card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">{t.forecast.title}</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">{t.forecast.lockedNote}</p>
      </section>
    )
  }

  const trendMeta = TREND_ICON[forecast.trend]
  const trendLabel = t.forecast.trend[forecast.trend]
  const dayTotal = forecast.projectedDayFame
  const ciLow = forecast.projectedDayFameLow
  const ciHigh = forecast.projectedDayFameHigh
  const hasCI = ciHigh > ciLow && ciLow > 0

  return (
    <section className="card forecast-card">
      <div className="card-title-row">
        <div className="card-title">{t.forecast.title}</div>
        <span className={`trend-chip ${trendMeta.cls}`}>{trendMeta.icon} {trendLabel}</span>
      </div>

      <div className="forecast-numbers">
        <div className="forecast-block forecast-block-primary">
          <span className="forecast-label-small">{t.forecast.dayLabel}</span>
          <span className="forecast-value">{fmt(dayTotal)}</span>
          {hasCI && (
            <span className="forecast-ci muted small">
              {t.forecast.range} {fmt(ciLow)} – {fmt(ciHigh)}
            </span>
          )}
        </div>

        <div className="forecast-week-row">
          <span className="forecast-week-label muted small">{t.forecast.weekLabel}</span>
          <span className="forecast-week-value">{fmt(forecast.projectedWeekFame)}</span>
          <span className="forecast-week-attacks muted small">
            {t.forecast.attacksLeft}{forecast.expectedRemainingAttacksToday}
          </span>
        </div>
      </div>

      <div className="confidence-row">
        <span className="confidence-label">{t.forecast.accuracy}</span>
        <div className="confidence-track">
          <div
            className={`confidence-fill ${forecast.confidence >= 70 ? 'conf-high' : forecast.confidence >= 50 ? 'conf-mid' : 'conf-low'}`}
            style={{ width: `${forecast.confidence}%` }}
          />
        </div>
        <span className="confidence-pct">{forecast.confidence}%</span>
      </div>

      <details className="forecast-help">
        <summary>{t.forecast.howTitle}</summary>
        <p className="muted small">
          {t.forecast.howDay1}<strong>~{forecast.expectedRemainingAttacksToday}</strong>{t.forecast.howDay2}
        </p>
        <p className="muted small">{t.forecast.howWeek}</p>
      </details>
    </section>
  )
}
