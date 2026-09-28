import { useEffect, useMemo, useState } from 'react'
import type { ArchetypeRow, MatchSession } from '../types'
import type { Translations } from '../lib/i18n'
import { REDUCED, useInView } from '../lib/anim'

/** Запускает анимацию, когда блок показался на экране. */
function useReveal<T extends Element>() {
  const [ref, seen] = useInView<T>()
  const [on, setOn] = useState(REDUCED)
  useEffect(() => {
    if (!seen || REDUCED) return
    const id = requestAnimationFrame(() => setOn(true))
    return () => cancelAnimationFrame(id)
  }, [seen])
  return [ref, on] as const
}

/* ------------------------------------------------------------------ */
/* 📈 Кубки по ходу захода                                            */
/* ------------------------------------------------------------------ */

/**
 * Линия кубков через весь заход: видно не «4–2», а как именно шёл вечер — где
 * был пик, где началась серия и сколько она стоила. Место, где Стоп-тильт
 * написал бы (второе поражение подряд), отмечено льдинкой, а всё после него
 * подсвечено — это и есть цена тильта в кубках.
 */
export function SessionChart({ session, t }: { session: MatchSession; t: Translations }) {
  const [ref, on] = useReveal<HTMLDivElement>()
  const data = useMemo(() => {
    const games = [...session.matches].sort((a, b) => a.timeUtc.localeCompare(b.timeUtc))
    const trophyMode = games.some(g => g.trophyChange !== null)
    let acc = 0
    let streak = 0
    let tiltAt: number | null = null
    const points = games.map((g, i) => {
      acc += trophyMode ? (g.trophyChange ?? 0) : g.result
      streak = g.result < 0 ? streak + 1 : g.result > 0 ? 0 : streak
      if (streak === 2 && tiltAt === null) tiltAt = i
      return { v: acc, r: g.result }
    })
    return { points: [{ v: 0, r: 0 }, ...points], trophyMode, tiltAt: tiltAt === null ? null : tiltAt + 1 }
  }, [session.matches])

  if (data.points.length < 4) return null

  const W = 300, H = 76, PAD = 8
  const vs = data.points.map(p => p.v)
  const min = Math.min(0, ...vs), max = Math.max(0, ...vs)
  const span = Math.max(1, max - min)
  const x = (i: number) => PAD + (i * (W - 2 * PAD)) / (data.points.length - 1)
  const y = (v: number) => PAD + ((max - v) * (H - 2 * PAD)) / span
  const line = data.points.map((p, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)} ${y(p.v).toFixed(1)}`).join(' ')
  const area = `${line} L${x(data.points.length - 1).toFixed(1)} ${y(min).toFixed(1)} L${x(0).toFixed(1)} ${y(min).toFixed(1)} Z`
  const end = vs[vs.length - 1]
  const up = end >= 0
  const id = `sc-${session.startUtc.replace(/\W/g, '')}`

  return (
    <div className="sc" ref={ref}>
      <div className="sc-head">
        <span className="muted small">{t.trk.chartTitle}</span>
        <b className={up ? 'trk-good' : 'trk-bad'}>{end > 0 ? '+' : end < 0 ? '−' : ''}{Math.abs(end)}{data.trophyMode ? '🏆' : ''}</b>
      </div>
      <div className="sc-box">
        <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" className="sc-svg" aria-hidden>
          <defs>
            <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={up ? '#5fd573' : '#ff6b5a'} stopOpacity="0.45" />
              <stop offset="100%" stopColor={up ? '#5fd573' : '#ff6b5a'} stopOpacity="0" />
            </linearGradient>
          </defs>
          {data.tiltAt !== null && (
            <rect x={x(data.tiltAt)} y={0} width={Math.max(0, W - PAD - x(data.tiltAt))} height={H}
              className={`sc-tilt ${on ? 'sc-on' : ''}`} />
          )}
          <line x1={PAD} x2={W - PAD} y1={y(0)} y2={y(0)} className="sc-zero" vectorEffect="non-scaling-stroke" />
          <path d={area} fill={`url(#${id})`} className={`sc-area ${on ? 'sc-on' : ''}`} />
          <path d={line} pathLength={1} className={`sc-line ${up ? 'sc-line-up' : 'sc-line-down'} ${on ? 'sc-on' : ''}`}
            vectorEffect="non-scaling-stroke" />
        </svg>
        {data.points.slice(1).map((p, i) => (
          <span key={i}
            className={`sc-dot ${p.r > 0 ? 'sc-dot-w' : p.r < 0 ? 'sc-dot-l' : ''} ${on ? 'sc-on' : ''}`}
            style={{ left: `${(x(i + 1) / W) * 100}%`, top: `${(y(p.v) / H) * 100}%`, transitionDelay: `${0.15 + i * 0.06}s` }} />
        ))}
        {data.tiltAt !== null && (
          <span className={`sc-ice ${on ? 'sc-on' : ''}`} style={{ left: `${(x(data.tiltAt) / W) * 100}%` }}>🧊</span>
        )}
      </div>
      {data.tiltAt !== null && <span className="muted small sc-note">{t.trk.chartTilt}</span>}
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* 🗓 Когда ты играешь лучше                                          */
/* ------------------------------------------------------------------ */

const MIN_CELL = 3
const MIN_BEST = 5
/** Порядок строк: утро, день, вечер, ночь — так сутки читаются сверху вниз. */
const ROWS = [1, 2, 3, 0]

/**
 * Тепловая карта недели: день × часть суток. Цвет — не процент сам по себе, а
 * разница с твоим обычным: зелёное — тут ты сильнее себя, красное — слабее.
 * Клетки, где боёв меньше трёх, серые: цвет по одному бою — это шум.
 */
export function HeatMap({ cells, basePct, t }: {
  cells: { weekday: number; part: number; games: number; wins: number }[]
  basePct: number
  t: Translations
}) {
  const [ref, on] = useReveal<HTMLElement>()
  const s = t.trk
  const parts = s.heatParts.split('|')
  const days = useMemo(() => Array.from({ length: 7 }, (_, d) =>
    new Date(2024, 0, 1 + d).toLocaleDateString(t.dateLocale, { weekday: 'short' }).replace('.', '')), [t.dateLocale])

  const grid = new Map(cells.map(c => [`${c.weekday}:${c.part}`, c]))
  const rated = cells.filter(c => c.games >= MIN_BEST).map(c => ({ ...c, pct: (100 * c.wins) / c.games }))
  if (cells.every(c => c.games < MIN_CELL)) return null
  const best = rated.length ? rated.reduce((a, b) => (b.pct > a.pct ? b : a)) : null
  const worst = rated.length > 1 ? rated.reduce((a, b) => (b.pct < a.pct ? b : a)) : null
  const slot = (c: { weekday: number; part: number }) => `${days[c.weekday]} ${parts[c.part]}`

  return (
    <section className="card hm" ref={ref}>
      <div className="card-title" style={{ marginBottom: 2 }}>{s.heatTitle}</div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>{s.heatSub.replace('{p}', String(Math.round(basePct)))}</p>
      <div className="hm-grid">
        <span />
        {days.map(d => <span key={d} className="hm-day">{d}</span>)}
        {ROWS.map((p, ri) => (
          <RowCells key={p} part={p} ri={ri} label={parts[p]} grid={grid} basePct={basePct} on={on} />
        ))}
      </div>
      <div className="hm-foot">
        {best && <span className="small">⬆ {s.heatBest.replace('{slot}', slot(best)).replace('{p}', String(Math.round(best.pct)))}</span>}
        {worst && worst !== best && <span className="small">⬇ {s.heatWorst.replace('{slot}', slot(worst)).replace('{p}', String(Math.round(worst.pct)))}</span>}
      </div>
    </section>
  )
}

function RowCells({ part, ri, label, grid, basePct, on }: {
  part: number; ri: number; label: string
  grid: Map<string, { games: number; wins: number }>; basePct: number; on: boolean
}) {
  return (
    <>
      <span className="hm-part">{label}</span>
      {Array.from({ length: 7 }, (_, d) => {
        const c = grid.get(`${d}:${part}`) ?? { games: 0, wins: 0 }
        const pct = c.games ? (100 * c.wins) / c.games : 0
        const delta = pct - basePct
        const strength = Math.min(1, Math.abs(delta) / 25)
        const rated = c.games >= MIN_CELL
        const bg = !rated
          ? 'rgba(255,255,255,0.05)'
          : delta >= 0
            ? `rgba(60, 200, 110, ${0.18 + strength * 0.62})`
            : `rgba(255, 80, 60, ${0.18 + strength * 0.62})`
        return (
          <span key={d} className={`hm-cell ${on ? 'hm-on' : ''}`}
            style={{ background: bg, transitionDelay: `${(d + ri) * 0.035}s` }}
            title={`${c.wins}/${c.games}`}>
            {rated ? Math.round(pct) : c.games > 0 ? '·' : ''}
          </span>
        )
      })}
    </>
  )
}

/* ------------------------------------------------------------------ */
/* 🕸 Радар матчапов                                                  */
/* ------------------------------------------------------------------ */

/** Для тизера без Плюса: форма есть, цифр нет. */
const TEASER: ArchetypeRow[] = ['Golem', 'Hog', 'Log Bait', 'LavaLoon', 'X-Bow', 'PEKKA'].map((label, i) => ({
  arch: { key: label, label, card: null },
  games: 10, wins: [3, 7, 5, 8, 4, 6][i], losses: 0, deltaPp: 0,
}))

/**
 * Паутина: процент побед против каждой из главных колод соперников, пунктир —
 * свой обычный процент. Где многоугольник проваливается внутрь пунктира — там
 * и надо искать, что поменять в колоде.
 */
export function ArchRadar({ rows, basePct, locked, onUnlock, t }: {
  rows: ArchetypeRow[]; basePct: number; locked: boolean; onUnlock: () => void; t: Translations
}) {
  const [ref, on] = useReveal<HTMLDivElement>()
  const axes = (locked ? TEASER : [...rows].sort((a, b) => b.games - a.games).slice(0, 6))
  if (axes.length < 3) return null

  const C = 110, R = 78
  const pt = (i: number, v: number) => {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / axes.length
    return [C + Math.cos(a) * R * v, C + Math.sin(a) * R * v] as const
  }
  const poly = (vals: number[]) => vals.map((v, i) => pt(i, v).map(n => n.toFixed(1)).join(',')).join(' ')
  const vals = axes.map(r => r.games ? r.wins / r.games : 0)

  return (
    <div className={`rd ${locked ? 'rd-locked' : ''}`} ref={ref}>
      <div className="tilt-rules-title">{t.trk.radarTitle}</div>
      <span className="muted small">{t.trk.radarSub}</span>
      <div className="rd-box">
        <svg viewBox="-50 -14 320 248" className="rd-svg" aria-hidden>
          {[0.25, 0.5, 0.75, 1].map(k => (
            <polygon key={k} points={poly(axes.map(() => k))} className="rd-ring" />
          ))}
          {axes.map((_, i) => {
            const [x2, y2] = pt(i, 1)
            return <line key={i} x1={C} y1={C} x2={x2} y2={y2} className="rd-axis" />
          })}
          <polygon points={poly(axes.map(() => basePct / 100))} className="rd-base" />
          <g className={`rd-shape ${on ? 'rd-on' : ''}`} style={{ transformOrigin: `${C}px ${C}px` }}>
            <polygon points={poly(vals)} className="rd-fill" />
            {vals.map((v, i) => {
              const [cx, cy] = pt(i, v)
              return <circle key={i} cx={cx} cy={cy} r="3.5"
                className={v * 100 >= basePct ? 'rd-dot-good' : 'rd-dot-bad'} />
            })}
          </g>
          {axes.map((r, i) => {
            const [lx, ly] = pt(i, 1.2)
            return (
              <text key={i} x={lx} y={ly} className="rd-label" textAnchor={lx < C - 5 ? 'end' : lx > C + 5 ? 'start' : 'middle'}
                dominantBaseline="middle">
                {r.arch.label}{locked ? '' : ` ${Math.round(vals[i] * 100)}%`}
              </text>
            )
          })}
        </svg>
        {locked && (
          <button className="rd-lock" onClick={onUnlock}>{t.trk.radarLocked}</button>
        )}
      </div>
    </div>
  )
}
