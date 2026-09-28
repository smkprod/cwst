import { useEffect, useState } from 'react'
import { REDUCED, useCountUp, useInView } from '../lib/anim'
import type { Matchup, MatchupTier } from '../types'
import { haptic } from '../lib/telegram'
import type { Translations } from '../lib/i18n'

/**
 * «📊 Матчап»: как такие колоды играют друг против друга — по боям топ-500 за неделю
 * и по своим боям за месяц. Лестница от «точно такие колоды» до «только вин-кон»,
 * у каждой ступени — сколько боёв за ней, чтобы 3 из 4 не читались как закон.
 */
export function MatchupPanel({ m, t, onPlus }: { m: Matchup; t: Translations; onPlus: () => void }) {
  const s = t.trk
  const [src, setSrc] = useState<'top' | 'own'>('top')
  const [ref, seen] = useInView<HTMLDivElement>()
  const tiers = src === 'top' ? m.top : m.own
  const head = tiers?.find(x => x.key === m.headline && src === 'top')
    ?? tiers?.find(x => x.reliability !== 'low') ?? null

  const pick = (next: 'top' | 'own') => {
    if (next === 'own' && m.ownLocked) { onPlus(); return }
    haptic('light')
    setSrc(next)
  }

  return (
    <div className="trk-block mu" ref={ref}>
      <div className="mu-head">
        <div>
          <div className="tilt-rules-title">{s.matchTitle}</div>
          <span className="muted small">{s.matchSub}</span>
        </div>
      </div>

      {(m.me || m.them) && (
        <div className="mu-shapes">
          <Shape label={s.matchYou} v={m.me} t={t} />
          <span className="mu-vs">VS</span>
          <Shape label={s.matchThem} v={m.them} t={t} />
        </div>
      )}

      <div className="mu-switch">
        <button className={`trk-chip ${src === 'top' ? 'trk-chip-on' : ''}`} onClick={() => pick('top')}>{s.matchTop}</button>
        <button className={`trk-chip ${src === 'own' ? 'trk-chip-on' : ''}`} onClick={() => pick('own')}>
          {s.matchOwn}{m.ownLocked ? ' 🔒' : ''}
        </button>
      </div>

      {!tiers ? (
        <p className="muted small" style={{ margin: 0 }}>{src === 'top' ? s.matchEmpty : s.matchOwnEmpty}</p>
      ) : (
        <>
          {head && <Gauge key={`${src}-${head.key}`} tier={head} t={t} run={seen} />}
          <div className="mu-ladder">
            {tiers.map((tier, i) => <Row key={`${src}-${tier.key}`} tier={tier} t={t} run={seen} delay={i * 110} />)}
          </div>
          <div className="mu-legend">
            <span><i className="mu-dot mu-reliable" />{s.relReliable}</span>
            <span><i className="mu-dot mu-adequate" />{s.relAdequate}</span>
            <span><i className="mu-dot mu-low" />{s.relLow}</span>
          </div>
        </>
      )}
      <span className="muted small">{s.matchNote}</span>
    </div>
  )
}

/** 775415 → «775 415»: длинное число без разрядов не читается. */
function fmtN(n: number, t: Translations) {
  return n.toLocaleString(t.dateLocale).replace(/[\u00a0\u202f,]/g, ' ')
}

function tierLabel(key: string, t: Translations) {
  const s = t.trk
  return key === 'exact' ? s.tierExact : key === 'seven' ? s.tierSeven : key === 'six' ? s.tierSix
    : key === 'wincon4' ? s.tierWincon4 : s.tierWincon
}

/** Кольцо с процентом самой точной надёжной ступени — главная цифра блока. */
function Gauge({ tier, t, run }: { tier: MatchupTier; t: Translations; run: boolean }) {
  const pct = useCountUp(tier.winPercent, run, 1100)
  const R = 34, C = 2 * Math.PI * R
  const good = tier.winPercent >= 50
  return (
    <div className="mu-gauge">
      <svg width="92" height="92" viewBox="0 0 92 92" aria-hidden>
        <circle cx="46" cy="46" r={R} className="mu-ring-bg" />
        <circle cx="46" cy="46" r={R} className={`mu-ring ${good ? 'mu-ring-good' : 'mu-ring-bad'}`}
          strokeDasharray={C} strokeDashoffset={C * (1 - pct / 100)} transform="rotate(-90 46 46)" />
      </svg>
      <div className="mu-gauge-num"><span>{Math.round(pct)}<small>%</small></span></div>
      <div className="mu-gauge-text">
        <b>{tierLabel(tier.key, t)}</b>
        <span className="muted small">{t.trk.matchBattles.replace('{n}', fmtN(tier.games, t))} · {t.trk.matchWin}</span>
      </div>
    </div>
  )
}

/** Ступень лестницы: полоса побед/ничьих/поражений заполняется слева направо. */
function Row({ tier, t, run, delay }: { tier: MatchupTier; t: Translations; run: boolean; delay: number }) {
  const [go, setGo] = useState(REDUCED)
  useEffect(() => {
    if (!run || REDUCED) return
    const id = window.setTimeout(() => setGo(true), delay)
    return () => window.clearTimeout(id)
  }, [run, delay])
  const pct = useCountUp(tier.winPercent, go, 800)
  const g = Math.max(1, tier.games)
  const w = go ? (tier.wins / g) * 100 : 0
  const d = go ? (tier.draws / g) * 100 : 0
  const l = go ? (tier.losses / g) * 100 : 0
  const empty = tier.games === 0

  return (
    <div className={`mu-row ${empty ? 'mu-row-empty' : ''}`}>
      <div className="mu-row-top">
        <span className="mu-row-name"><i className={`mu-dot mu-${tier.reliability}`} />{tierLabel(tier.key, t)}</span>
        <span className="mu-row-num">
          {empty ? '—' : tier.games < 5 ? `${tier.wins}–${tier.losses}` : `${Math.round(pct)}%`}
          <span className="muted"> · {fmtN(tier.games, t)}</span>
        </span>
      </div>
      <div className="mu-bar">
        <span className="mu-w" style={{ width: `${w}%` }} />
        <span className="mu-d" style={{ width: `${d}%` }} />
        <span className="mu-l" style={{ width: `${l}%` }} />
      </div>
    </div>
  )
}

function Shape({ label, v, t }: { label: string; v: { avgElixir: number; cycle: number } | null; t: Translations }) {
  return (
    <div className="mu-shape">
      <span className="muted small">{label}</span>
      {v ? (
        <span className="mu-shape-vals">
          <b>💧 {v.avgElixir.toFixed(1)}</b>
          <span className="muted small">♻️ {t.trk.matchCycle.replace('{n}', String(v.cycle))}</span>
        </span>
      ) : <b>—</b>}
    </div>
  )
}
