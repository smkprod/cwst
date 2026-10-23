import { useEffect, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import { useT, type Translations } from '../lib/i18n'
import { usePlusSheet, PLUS_CHANGED } from '../lib/plusSheet'
import { openTip } from '../lib/tipSheet'
import { haptic } from '../lib/telegram'
import { isTipId, tipText } from '../lib/tips'
import type { Dashboard, DashboardInsight } from '../types'
import { Icon } from './ui/Icon'
import { GameIcon, type GameIconName } from './GameIcon'

/**
 * Профиль-дашборд: вся своя игра на одном экране. Сверху - кубки, процент побед и
 * счёт; сразу за ними «Что исправить» - ради этого блока сюда и приходят, и он же
 * продаёт Плюс: первый вывод целиком бесплатно, остальные - за ним.
 */
export function ProfileDashboard() {
  const { t } = useT()
  const s = t.dash
  const [data, setData] = useState<Dashboard | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let alive = true
    const load = () => api.getDashboard()
      .then(d => { if (alive) setData(d) })
      .catch(() => { if (alive) setFailed(true) })
    load()
    // Купил Плюс - перечитать: выводы и карты откроются без перезапуска
    window.addEventListener(PLUS_CHANGED, load)
    return () => { alive = false; window.removeEventListener(PLUS_CHANGED, load) }
  }, [])

  if (failed) return null
  if (!data) return <div className="pd"><div className="pd-panel pd-skeleton" /><div className="pd-panel pd-skeleton" style={{ height: 160 }} /></div>
  if (data.games === 0) return <div className="pd"><div className="pd-panel pd-empty">{s.empty}</div></div>

  return (
    <div className="pd fade-in">
      <Hero d={data} t={t} />
      <FixPanel d={data} t={t} />
      <Recent d={data} t={t} />
      {data.trophySeries.length >= 3 && <TrophyChart d={data} t={t} />}
      {data.deck.length > 0 && <Deck d={data} t={t} />}
      {data.cardRates.length > 0 && <CardRates d={data} t={t} />}
      <div className="pd-two">
        <Crowns d={data} t={t} />
        <Modes d={data} t={t} />
      </div>
      <Streaks d={data} t={t} />
    </div>
  )
}

const fmt = (n: number) => Math.round(n).toLocaleString('ru-RU').replace(/,/g, ' ')
const pct = (v: number) => `${Math.round(v)}%`

/** Корона - своя, не игровая: лучше простая своя, чем чужой арт. */
export function Crown({ tone, size = 26 }: { tone: 'win' | 'loss' | 'draw' | 'gold'; size?: number }) {
  const fill = tone === 'win' ? '#3d8bff' : tone === 'loss' ? '#ef4454' : tone === 'gold' ? '#ffc83d' : '#8c93ad'
  const light = tone === 'win' ? '#9cc6ff' : tone === 'loss' ? '#ffa0a8' : tone === 'gold' ? '#ffe9a8' : '#c3c8d6'
  return (
    <svg width={size} height={size * 0.8} viewBox="0 0 30 24" aria-hidden="true">
      <path d="M2 7 9 12 15 3l6 9 7-5-3 14H5L2 7Z" fill={fill} stroke={light} strokeWidth="1.4" strokeLinejoin="round" />
      <rect x="5" y="18" width="20" height="3.5" rx="1.2" fill={light} />
      <circle cx="15" cy="3" r="2" fill={light} />
    </svg>
  )
}

function Panel({ icon, title, note, children, className = '' }: { icon?: GameIconName; title?: string; note?: string; children: ReactNode; className?: string }) {
  return (
    <section className={`pd-panel ${className}`}>
      {title && (
        <div className="pd-head">
          {icon && <span className="pd-head-ic"><GameIcon name={icon} size={26} /></span>}
          <b>{title}</b>
          {note && <span className="pd-note">{note}</span>}
        </div>
      )}
      {children}
    </section>
  )
}

function Hero({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const ring = 2 * Math.PI * 38
  const share = d.games > 0 ? d.wins / Math.max(1, d.wins + d.losses) : 0
  const spark = d.trophySeries.slice(-14)
  return (
    <section className="pd-hero">
      <div className="pd-hero-top">
        <div>
          <div className="pd-name">{d.name}</div>
          <div className="pd-sub">{s.sub}</div>
        </div>
      </div>
      <div className="pd-hero-grid">
        <div className="pd-tile pd-trophies">
          <div className="pd-tile-row"><GameIcon name="trophy" size={24} /> <b>{d.trophies > 0 ? fmt(d.trophies) : '—'}</b></div>
          {d.trophyDelta !== 0 && (
            <div className={d.trophyDelta > 0 ? 'pd-up' : 'pd-down'}>
              {d.trophyDelta > 0 ? '▲ +' : '▼ '}{fmt(d.trophyDelta)} <span>{s.week.replace('{n}', '')}</span>
            </div>
          )}
          {spark.length >= 3 && <Spark values={spark} />}
          {d.bestTrophies > 0 && <small>{s.best.replace('{n}', fmt(d.bestTrophies))}</small>}
        </div>
        <div className="pd-tile pd-ring">
          <svg viewBox="0 0 100 100" width="96" height="96">
            <circle cx="50" cy="50" r="38" fill="none" stroke="rgba(239,68,84,0.55)" strokeWidth="10" />
            <circle cx="50" cy="50" r="38" fill="none" stroke="url(#pd-ring-g)" strokeWidth="10" strokeLinecap="round"
              strokeDasharray={`${ring * share} ${ring}`} transform="rotate(-90 50 50)" className="pd-ring-arc" />
            <defs>
              <linearGradient id="pd-ring-g" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0" stopColor="#35d08a" /><stop offset="1" stopColor="#3d8bff" />
              </linearGradient>
            </defs>
          </svg>
          <div className="pd-ring-text"><b>{pct(d.winPercent)}</b><small>{s.winRate}</small></div>
        </div>
        <div className="pd-tile pd-wl">
          <div className="pd-wl-row"><Crown tone="win" size={22} /><b>{d.wins}</b><small>{s.wins}</small></div>
          <Bars n={d.wins} of={Math.max(d.wins, d.losses)} tone="win" />
          <div className="pd-wl-row"><Crown tone="loss" size={22} /><b>{d.losses}</b><small>{s.losses}</small></div>
          <Bars n={d.losses} of={Math.max(d.wins, d.losses)} tone="loss" />
        </div>
      </div>
    </section>
  )
}

function Bars({ n, of, tone }: { n: number; of: number; tone: 'win' | 'loss' }) {
  const filled = of === 0 ? 0 : Math.round((n / of) * 8)
  return (
    <div className="pd-bars">
      {Array.from({ length: 8 }, (_, i) => <i key={i} className={i < filled ? `pd-bar-${tone}` : ''} />)}
    </div>
  )
}

function Spark({ values }: { values: number[] }) {
  const min = Math.min(...values), max = Math.max(...values)
  const pts = values.map((v, i) => `${(i / (values.length - 1)) * 100},${30 - ((v - min) / Math.max(1, max - min)) * 26 - 2}`)
  return (
    <svg className="pd-spark" viewBox="0 0 100 30" preserveAspectRatio="none">
      <polyline points={pts.join(' ')} fill="none" stroke="#5aa9ff" strokeWidth="2" vectorEffect="non-scaling-stroke" />
    </svg>
  )
}

function FixPanel({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const openPlus = usePlusSheet()
  const shown = d.insights.filter(i => i.tip !== null || i.games > 0)
  const locked = d.insights.filter(i => i.tip === null && i.games === 0)
  return (
    <Panel icon="target" title={s.fixTitle} className="pd-fix">
      <p className="pd-lead">{d.insights.length > 0 ? s.fixLead : s.fixEmpty}</p>
      {shown.map((ins, i) => <Insight key={i} ins={ins} t={t} />)}
      {locked.map((ins, i) => (
        <div key={`l${i}`} className="pd-insight pd-insight-locked" onClick={() => { haptic('light'); openPlus('plus') }}>
          <span className="pd-ins-ic"><GameIcon name="lock" size={24} /></span>
          <div className="pd-ins-body">
            <b>{s.lockedKind[ins.kind] ?? ins.kind}</b>
            <span className="pd-blur">██████ ███ ████████ ██ ███%</span>
          </div>
        </div>
      ))}
      {d.insightsLocked > 0 && (
        <button className="pd-cta" onClick={() => { haptic('medium'); openPlus('plus') }}>
          <GameIcon name="gem" size={20} /> {s.lockedCta} · {s.lockedMore.replace('{n}', String(d.insightsLocked))}
        </button>
      )}
    </Panel>
  )
}

function Insight({ ins, t }: { ins: DashboardInsight; t: Translations }) {
  const s = t.dash
  const { lang } = useT()
  const title = (s.insight[ins.kind] ?? ins.kind)
    .replace('{card}', ins.key ?? '')
    .replace('{part}', ins.key ? (s.parts[ins.key] ?? ins.key) : '')
  const why = (s.insightWhy[ins.kind] ?? '')
    .replace('{v}', String(ins.kind === 'leak' || ins.kind === 'levels' ? Math.abs(ins.value) : Math.round(ins.value)))
    .replace('{b}', String(ins.kind === 'leak' ? ins.baseline : Math.round(ins.baseline)))
    .replace('{n}', String(ins.games))
  const tip = isTipId(ins.tip) ? tipText(lang, ins.tip) : null
  return (
    <div className="pd-insight">
      <span className="pd-ins-ic">
        {ins.iconUrl ? <img src={ins.iconUrl} alt="" /> : <GameIcon name={KIND_ICON[ins.kind] ?? 'target'} size={26} />}
      </span>
      <div className="pd-ins-body">
        <b>{title}</b>
        <span className="pd-ins-why"><em>{s.why}:</em> {why}</span>
        {tip && <span className="pd-ins-fix"><em>{s.fix}:</em> {tip.steps[1] ?? tip.title}</span>}
        {tip && isTipId(ins.tip) && (
          <button className="btn-mini pd-ins-btn" onClick={() => openTip(ins.tip!)}><GameIcon name="play" size={16} /> {s.watchTip}</button>
        )}
      </div>
    </div>
  )
}

const KIND_ICON: Record<string, GameIconName> = {
  tough_card: 'swords', leak: 'drop', tilt: 'flame', daypart: 'scroll', levels: 'chart', deck_meta: 'cards',
}

function Recent({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const now = new Date()
  const label = (iso: string) => {
    const dt = new Date(iso)
    const hm = dt.toLocaleTimeString(t.dateLocale, { hour: '2-digit', minute: '2-digit' })
    const days = Math.floor((new Date(now.toDateString()).getTime() - new Date(dt.toDateString()).getTime()) / 86400000)
    return days === 0 ? `${s.today} ${hm}` : days === 1 ? `${s.yesterday} ${hm}` : dt.toLocaleDateString(t.dateLocale, { day: '2-digit', month: '2-digit' })
  }
  return (
    <Panel icon="swords" title={s.recentTitle} note={s.battles.replace('{n}', String(d.games))}>
      <div className="pd-recent">
        {d.recent.map(b => (
          <div key={b.id} className={`pd-battle pd-battle-${b.result > 0 ? 'win' : b.result < 0 ? 'loss' : 'draw'}`}
            title={b.oppArchetype ?? ''}>
            <Crown tone={b.result > 0 ? 'win' : b.result < 0 ? 'loss' : 'draw'} size={24} />
            <b>{b.crownsFor}–{b.crownsAgainst}</b>
            <small>{label(b.atUtc)}</small>
          </div>
        ))}
      </div>
    </Panel>
  )
}

function TrophyChart({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const v = d.trophySeries
  const min = Math.min(...v), max = Math.max(...v)
  const W = 320, H = 120
  const x = (i: number) => (i / (v.length - 1)) * W
  const y = (n: number) => H - 10 - ((n - min) / Math.max(1, max - min)) * (H - 26)
  const line = v.map((n, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(n).toFixed(1)}`).join(' ')
  const area = `${line} L${W},${H} L0,${H} Z`
  return (
    <Panel icon="trophy" title={s.trophyTitle} note={s.trophyNote.replace('{n}', String(v.length - 1))}>
      <svg className="pd-chart" viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none">
        <defs>
          <linearGradient id="pd-area" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#7c5cff" stopOpacity="0.55" /><stop offset="1" stopColor="#7c5cff" stopOpacity="0" />
          </linearGradient>
        </defs>
        <path d={area} fill="url(#pd-area)" />
        <path d={line} fill="none" stroke="#9d86ff" strokeWidth="2.5" vectorEffect="non-scaling-stroke" className="pd-chart-line" />
        <circle cx={x(v.length - 1)} cy={y(v[v.length - 1])} r="4" fill="#fff" stroke="#9d86ff" strokeWidth="2" />
      </svg>
      <div className="pd-chart-axis"><span>{fmt(min)}</span><b>{fmt(v[v.length - 1])}</b><span>{fmt(max)}</span></div>
    </Panel>
  )
}

function Deck({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  return (
    <Panel icon="cards" title={s.deckTitle}>
      <div className="pd-deck">
        {d.deck.map((c, i) => (
          <div key={i} className={`pd-card ${c.evo ? 'pd-card-evo' : ''}`}>
            <img src={c.iconUrl} alt={c.name} loading="lazy" />
            <span>{c.level}</span>
          </div>
        ))}
      </div>
      <div className="pd-elixir"><GameIcon name="drop" size={18} /> {s.avgElixir}: <b>{d.avgElixir.toFixed(1)}</b></div>
    </Panel>
  )
}

function CardRates({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const openPlus = usePlusSheet()
  const color = (p: number) => (p >= 60 ? '#35d08a' : p >= 52 ? '#9be15d' : p >= 45 ? '#f0c53b' : p >= 38 ? '#ff9a3c' : '#ef4454')
  return (
    <Panel icon="chart" title={s.cardsTitle} note={s.cardsNote}>
      {d.cardRates.map(r => (
        <div key={r.card.cardId} className="pd-rate">
          <img src={r.card.iconUrl} alt="" loading="lazy" />
          <span className="pd-rate-name">{r.card.name}</span>
          <span className="pd-rate-bar"><i style={{ width: `${r.winPercent}%`, background: color(r.winPercent) }} /></span>
          <b>{pct(r.winPercent)}</b>
          <small>{r.games}</small>
        </div>
      ))}
      {d.cardRatesLocked > 0 && (
        <button className="pd-locked-row" onClick={() => { haptic('light'); openPlus('plus') }}>
          <GameIcon name="lock" size={18} /> {s.cardsLocked.replace('{n}', String(d.cardRatesLocked))}
        </button>
      )}
    </Panel>
  )
}

function Crowns({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const parts = [
    { v: d.crowns.wins3, c: '#3d8bff', l: s.crowns3 },
    { v: d.crowns.wins2, c: '#7c5cff', l: s.crowns2 },
    { v: d.crowns.wins1, c: '#35d08a', l: s.crowns1 },
    { v: d.crowns.losses, c: '#ef4454', l: s.lossesL },
    { v: d.crowns.draws, c: '#8c93ad', l: s.draws },
  ].filter(p => p.v > 0)
  const total = parts.reduce((a, p) => a + p.v, 0)
  const C = 2 * Math.PI * 34
  let offset = 0
  return (
    <Panel icon="crown" title={s.crownsTitle}>
      <div className="pd-donut">
        <svg viewBox="0 0 100 100" width="104" height="104">
          {parts.map((p, i) => {
            const len = (p.v / Math.max(1, total)) * C
            const el = <circle key={i} cx="50" cy="50" r="34" fill="none" stroke={p.c} strokeWidth="12"
              strokeDasharray={`${len} ${C - len}`} strokeDashoffset={-offset} transform="rotate(-90 50 50)" />
            offset += len
            return el
          })}
          <text x="50" y="49" textAnchor="middle" fontSize="20" fontWeight="900" fill="#fff">{total}</text>
          <text x="50" y="63" textAnchor="middle" fontSize="9" fill="#9aa3c7">{s.battles.replace('{n}', '').trim()}</text>
        </svg>
        <div className="pd-legend">
          {parts.map((p, i) => <div key={i}><i style={{ background: p.c }} />{p.l}<b>{p.v}</b></div>)}
        </div>
      </div>
    </Panel>
  )
}

function Modes({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  return (
    <Panel icon="scroll" title={s.modesTitle}>
      {d.modes.map(m => (
        <div key={m.mode} className="pd-mode">
          <span>{s.modes[m.mode] ?? m.mode}</span>
          <span className="pd-rate-bar"><i style={{ width: `${m.winPercent}%` }} /></span>
          <b>{pct(m.winPercent)}</b>
          <small>{m.games}</small>
        </div>
      ))}
    </Panel>
  )
}

function Streaks({ d, t }: { d: Dashboard; t: Translations }) {
  const s = t.dash
  const now = d.currentStreak
  return (
    <Panel icon="flame" title={s.streaksTitle}>
      <div className="pd-streaks">
        <div><Crown tone="win" size={30} /><b>{d.bestWinStreak}</b><small>{s.bestWin}</small></div>
        <div><Crown tone="loss" size={30} /><b>{d.worstLossStreak}</b><small>{s.worstLoss}</small></div>
        <div><Crown tone={now > 0 ? 'gold' : now < 0 ? 'loss' : 'draw'} size={30} /><b>{now > 0 ? `+${now}` : now}</b><small>{s.streakNow}</small></div>
      </div>
    </Panel>
  )
}
