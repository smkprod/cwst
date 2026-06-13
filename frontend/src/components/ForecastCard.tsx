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
          Сколько медалей клан наберёт за сегодня и за неделю, тренд и точность прогноза —
          доступно на тарифе Pro. 🔒
        </p>
      </section>
    )
  }

  const trend = TREND_META[forecast.trend]
  // Очки клана за СЕГОДНЯШНИЙ военный день к его концу (только этот день, не вся неделя)
  const dayTotal = forecast.projectedDayFame
  const ciLow  = forecast.projectedDayFameLow
  const ciHigh = forecast.projectedDayFameHigh
  const hasCI  = ciHigh > ciLow && ciLow > 0

  return (
    <section className="card forecast-card">
      <div className="card-title-row">
        <div className="card-title">🔮 Прогноз клана</div>
        <span className={`trend-chip ${trend.cls}`}>{trend.icon} {trend.label}</span>
      </div>

      <div className="forecast-numbers">
        {/* Primary: projected points for the current war day only */}
        <div className="forecast-block forecast-block-primary">
          <span className="forecast-label-small">🏅 очков за сегодняшний день</span>
          <span className="forecast-value">{fmt(dayTotal)}</span>
          {hasCI && (
            <span className="forecast-ci muted small">
              диапазон: {fmt(ciLow)} – {fmt(ciHigh)}
            </span>
          )}
        </div>

        {/* Secondary: week total */}
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

      <details className="forecast-help">
        <summary>Как считается прогноз?</summary>
        <p className="muted small">
          За сегодняшний день: набрано сегодня + <strong>~{forecast.expectedRemainingAttacksToday}</strong> оставшихся
          атак клана × средние медали клана за атаку. Считаем очки только текущего военного дня
          (как на RoyaleAPI), а не сумму за всю неделю.
        </p>
        <p className="muted small">
          К концу недели: то же, но до конца всех 4 военных дней. «Диапазон» — это разброс ±1σ
          (как может лечь реальный результат). Чем больше сыграно дней и чем активнее клан —
          тем выше точность.
        </p>
      </details>
    </section>
  )
}
