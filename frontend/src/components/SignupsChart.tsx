import { useEffect, useState } from 'react'
import type { SignupPoint } from '../types'
import { haptic } from '../lib/telegram'

/** Какие ряды показывать. key — поле в SignupPoint, noun — что считаем в подписи. */
export interface ChartMetric {
  key: 'users' | 'clans' | 'active' | 'acting'
  label: string
  noun: string
}

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
export function SignupsChart(
  { title, points, metrics }: { title: string; points: SignupPoint[]; metrics: ChartMetric[] },
) {
  const [metric, setMetric] = useState<ChartMetric['key']>(metrics[0].key)
  const [range, setRange] = useState<Range>(30)
  const [picked, setPicked] = useState<number | null>(null)

  // Столбики выезжают снизу: первый кадр рисуем нулевой высоты и сразу переключаем
  // на настоящую, чтобы сработал transition. Иначе график просто возникает целиком,
  // а это единственный момент, когда видно, что он про изменение во времени.
  const [grown, setGrown] = useState(false)
  useEffect(() => {
    const id = requestAnimationFrame(() => setGrown(true))
    return () => cancelAnimationFrame(id)
  }, [])

  const window = points.slice(-range)
  const bars = range === 30 ? daily(window, metric) : weekly(window, metric)

  const total = bars.reduce((sum, b) => sum + b.value, 0)
  const max = bars.reduce((m, b) => Math.max(m, b.value), 0)
  const peak = max > 0 ? bars.findIndex(b => b.value === max) : -1

  // Потолок шкалы — сам пик, а не круглое число над ним. Круглое давало бы до
  // четверти пустой высоты сверху, а на карточке в 126 пикселей это заметная
  // потеря: мелкие дни и так почти не видны. Читаемость шкалы вместо этого даёт
  // сетка на круглых значениях, а верх и без того подписан значением пика.
  const top = max
  const shown = picked !== null ? bars[picked] : null
  const noun = metrics.find(m => m.key === metric)?.noun ?? ''

  return (
    <div className="card">
      <p className="adm-block-title">{title}</p>

      <div className="chart-controls">
        <div className="chart-switch">
          {metrics.map(m => (
            <button
              key={m.key}
              className={`chart-opt ${metric === m.key ? 'chart-opt-on' : ''}`}
              onClick={() => { haptic('light'); setMetric(m.key); setPicked(null) }}
            >{m.label}</button>
          ))}
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
          {/* Сетка под столбиками: две волосяные линии с подписями. Больше двух на
              высоту телефона превращаются в штриховку, меньше — и столбик не с чем
              сопоставить, кроме подписанного пика. */}
          <div className="chart-grid" aria-hidden="true">
            {gridLines(top).map(v => (
              <div className="chart-gridline" key={v} style={{ bottom: `${(v / top) * 100}%` }}>
                <span className="chart-gridval">{v}</span>
              </div>
            ))}
          </div>

          <div className="chart-bars">
            {bars.map((b, i) => (
              <button
                key={i}
                className={`chart-bar ${picked === i ? 'chart-bar-on' : ''}`
                  + `${b.value === 0 ? ' chart-bar-zero' : ''}`}
                // Ноль рисуем серой чёрточкой на оси, а не пустотой и не обычным
                // столбиком: пустота неотличима от «данных нет», а цветной обрубок
                // читается как «кто-то всё же был» — ровно наоборот смыслу.
                style={{
                  height: b.value === 0
                    ? undefined
                    : grown ? `max(4px, ${(b.value / top) * 100}%)` : '0%',
                }}
                onPointerEnter={() => setPicked(i)}
                onPointerLeave={() => setPicked(null)}
                onClick={() => { haptic('light'); setPicked(i) }}
                aria-label={`${b.label}: ${b.value} ${noun}`}
              >
                {i === peak && picked === null && <span className="chart-peak">{b.value}</span>}
              </button>
            ))}
          </div>
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

function daily(points: SignupPoint[], metric: ChartMetric['key']): Bar[] {
  return points.map(p => ({ label: short(p.date), value: p[metric] }))
}

/**
 * Недели считаем от свежего к старому, чтобы последняя неделя была полной:
 * человек смотрит на правый край графика, и обрезанный «хвост» там выглядел бы
 * как спад, которого нет.
 */
function weekly(points: SignupPoint[], metric: ChartMetric['key']): Bar[] {
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

/** Больше трёх линий на высоте в сто двадцать пикселей читаются как штриховка. */
const MaxGridLines = 3

/**
 * Значения горизонтальных линий сетки: кратные круглому шагу, строго ниже пика.
 *
 * Шаг берём наименьший из лестницы 1/2/5, который сам укладывается в лимит линий.
 * Подбирать шаг «около трети пика», а потом обрезать лишнее, нельзя: у пика 10
 * так выходило 2, 4, 6 — верхняя треть шкалы оставалась без единой отметки.
 *
 * Дробные шаги в лестницу не берём: мы считаем людей, и «2.5 игрока» на подписи —
 * шкала, которой нельзя верить.
 */
function gridLines(max: number): number[] {
  if (max < 2) return []

  for (let pow = 1; pow <= max; pow *= 10) {
    for (const s of [1, 2, 5]) {
      const step = s * pow
      // Линии стоят на step, 2*step, … строго ниже пика — отсюда и их число.
      if (Math.ceil(max / step) - 1 > MaxGridLines) continue

      const out: number[] = []
      for (let v = step; v < max; v += step) out.push(v)
      return out
    }
  }
  return []
}
