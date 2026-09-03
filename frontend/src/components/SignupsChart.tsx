import { useState } from 'react'
import type { SignupPoint } from '../types'
import { haptic } from '../lib/telegram'

type Metric = 'users' | 'clans'
type Range = 30 | 90

interface Bar {
  label: string       // подпись под курсором: «14 авг» или «11–17 авг»
  value: number
}

/**
 * Приход новых игроков и кланов по дням.
 *
 * Одна серия за раз, переключателем: две линии на одном поле потребовали бы легенды
 * и разведения цветов, а сравнивать игроков с кланами всё равно бессмысленно —
 * величины разного порядка.
 *
 * За 90 дней склеиваем по неделям. Девяносто столбиков на ширину телефона дают
 * меньше четырёх пикселей на каждый: формально данные показаны, фактически не
 * прочитать ни одного.
 */
export function SignupsChart({ points }: { points: SignupPoint[] }) {
  const [metric, setMetric] = useState<Metric>('users')
  const [range, setRange] = useState<Range>(30)
  const [picked, setPicked] = useState<number | null>(null)

  const window = points.slice(-range)
  const bars = range === 30 ? daily(window, metric) : weekly(window, metric)

  const total = bars.reduce((sum, b) => sum + b.value, 0)
  const max = bars.reduce((m, b) => Math.max(m, b.value), 0)
  const peak = max > 0 ? bars.findIndex(b => b.value === max) : -1

  const shown = picked !== null ? bars[picked] : null
  const noun = metric === 'users' ? 'игроков' : 'кланов'

  return (
    <div className="card">
      <p className="adm-block-title">📈 Кто и когда приходил</p>

      <div className="chart-controls">
        <div className="chart-switch">
          <button
            className={`chart-opt ${metric === 'users' ? 'chart-opt-on' : ''}`}
            onClick={() => { haptic('light'); setMetric('users'); setPicked(null) }}
          >Игроки</button>
          <button
            className={`chart-opt ${metric === 'clans' ? 'chart-opt-on' : ''}`}
            onClick={() => { haptic('light'); setMetric('clans'); setPicked(null) }}
          >Кланы</button>
        </div>
        <div className="chart-switch">
          <button
            className={`chart-opt ${range === 30 ? 'chart-opt-on' : ''}`}
            onClick={() => { haptic('light'); setRange(30); setPicked(null) }}
          >30 дней</button>
          <button
            className={`chart-opt ${range === 90 ? 'chart-opt-on' : ''}`}
            onClick={() => { haptic('light'); setRange(90); setPicked(null) }}
          >90 дней</button>
        </div>
      </div>

      {/* Строка под заголовком вместо всплывающей подсказки: на узком экране она
          упиралась бы в край карточки, а место здесь всё равно занято постоянно. */}
      <p className="chart-readout">
        {shown
          ? <><b>{shown.value}</b> {noun} · {shown.label}</>
          : <>всего <b>{total}</b> {noun} за {range} дн.{max > 0 && <> · пик {max} {perBar(range)}</>}</>}
      </p>

      {max === 0 ? (
        <p className="muted small">За этот период никто не приходил.</p>
      ) : (
        <div className="chart-plot">
          {bars.map((b, i) => (
            <button
              key={i}
              className={`chart-bar ${picked === i ? 'chart-bar-on' : ''}`}
              // Нулевой день не рисуем вовсе: столбик в пару пикселей читался бы
              // как «кто-то был», а это ровно противоположное значение.
              style={{ height: b.value === 0 ? 0 : `${Math.max(6, b.value / max * 100)}%` }}
              onPointerEnter={() => setPicked(i)}
              onPointerLeave={() => setPicked(null)}
              onClick={() => { haptic('light'); setPicked(i) }}
              aria-label={`${b.label}: ${b.value} ${noun}`}
            >
              {i === peak && picked === null && <span className="chart-peak">{b.value}</span>}
            </button>
          ))}
        </div>
      )}

      <div className="chart-axis">
        <span>{bars[0]?.label}</span>
        <span>{bars[bars.length - 1]?.label}</span>
      </div>
    </div>
  )
}

const MONTHS = ['янв', 'фев', 'мар', 'апр', 'мая', 'июн', 'июл', 'авг', 'сен', 'окт', 'ноя', 'дек']

/** «2026-08-14» → «14 авг». Разбираем строку, а не Date: часовой пояс сдвинул бы дату. */
function short(iso: string): string {
  const [, m, d] = iso.split('-')
  return `${Number(d)} ${MONTHS[Number(m) - 1]}`
}

function daily(points: SignupPoint[], metric: Metric): Bar[] {
  return points.map(p => ({ label: short(p.date), value: p[metric] }))
}

/**
 * Недели считаем от свежего к старому, чтобы последняя неделя была полной:
 * человек смотрит на правый край графика, и обрезанный «хвост» там выглядел бы
 * как спад, которого нет.
 */
function weekly(points: SignupPoint[], metric: Metric): Bar[] {
  const out: Bar[] = []
  for (let end = points.length; end > 0; end -= 7) {
    const chunk = points.slice(Math.max(0, end - 7), end)
    out.unshift({
      label: chunk.length > 1
        ? `${short(chunk[0].date)} – ${short(chunk[chunk.length - 1].date)}`
        : short(chunk[0].date),
      value: chunk.reduce((sum, p) => sum + p[metric], 0),
    })
  }
  return out
}

const perBar = (range: Range) => (range === 30 ? 'за день' : 'за неделю')
