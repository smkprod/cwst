import type { ClanForecast, ClanStats, ClanStatus } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { Icon, type IconName } from './ui/Icon'
import { SectionHead } from './ui/Section'

const TREND_ICON: Record<ClanForecast['trend'], { icon: IconName; cls: string }> = {
  ahead: { icon: 'trendUp', cls: 'trend-ahead' },
  onPace: { icon: 'arrowRight', cls: 'trend-onpace' },
  behind: { icon: 'trendDown', cls: 'trend-behind' },
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
        <SectionHead icon="sparkles" tone="violet" title={t.forecast.titleSimple} />
        <p className="muted small">{t.forecast.trainingNote}</p>
      </section>
    )
  }

  // Сервер строит прогноз всегда; пустым он не приходит, но и замком его закрывать
  // больше незачем — тарифа нет.
  if (forecast === null) return null

  const trendMeta = TREND_ICON[forecast.trend]
  const trendLabel = t.forecast.trend[forecast.trend]
  const dayTotal = forecast.projectedDayFame
  const ciLow = forecast.projectedDayFameLow
  const ciHigh = forecast.projectedDayFameHigh
  const hasCI = ciHigh > ciLow && ciLow > 0

  return (
    <section className="card forecast-card">
      <SectionHead
        icon="sparkles"
        tone="violet"
        title={t.forecast.title}
        infoTitle={t.forecast.howTitle}
        info={<>
          <p>{t.forecast.howDay1}<strong>~{forecast.expectedRemainingAttacksToday}</strong>{t.forecast.howDay2}</p>
          <p>{t.forecast.howWeek}</p>
        </>}
      />
      {/* Тренд — под заголовком: справа от него чип сжимал «Прогноз клана» в две строки */}
      <div className="forecast-trend">
        <span className={`trend-chip cl-ic ${trendMeta.cls}`}><Icon name={trendMeta.icon} size={13} />{trendLabel}</span>
      </div>

      <div className="forecast-numbers">
        <div className="forecast-block forecast-block-primary">
          <span className="forecast-label-small">{periodType === 'colosseum' ? t.forecast.dayLabelColosseum : t.forecast.dayLabel}</span>
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
    </section>
  )
}
