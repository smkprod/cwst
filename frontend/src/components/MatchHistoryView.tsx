import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { MatchHistory, MatchRow, MatchSession, TrackerState } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { usePlusSheet, PLUS_CHANGED } from '../lib/plusSheet'
import { CardIcon } from './MetaDecksView'
import { TrackerCard } from './TrackerCard'
import { MatchReportSheet } from './MatchReportSheet'

const RESULTS = ['all', 'win', 'loss'] as const
const MODES = ['all', 'ladder', 'pol', 'war', 'other'] as const

const VERDICT_ICON: Record<string, string> = {
  afk: '📴', levels: '⬆️', close: '🤏', leak: '💧', even: '⚖️',
  winLevels: '💪', winRank: '🌍', winUpset: '🚀', winClose: '😅',
}

/**
 * История боёв заходами: сверху «кому ты проигрываешь», ниже вечера и бои в них.
 * Тап по бою — полный отчёт. Работает и без трекера: бои копятся и так.
 */
export function MatchHistoryView({ openMatchId }: { openMatchId?: number | null }) {
  const { t, lang } = useT()
  const s = t.trk
  const openPlus = usePlusSheet()
  const [data, setData] = useState<MatchHistory | null>(null)
  const [sessions, setSessions] = useState<MatchSession[]>([])
  const [next, setNext] = useState<string | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error' | 'unlinked'>('loading')
  const [result, setResult] = useState<(typeof RESULTS)[number]>('all')
  const [mode, setMode] = useState<(typeof MODES)[number]>('all')
  const [arch, setArch] = useState<{ key: string; label: string } | null>(null)
  const [more, setMore] = useState(false)
  const [open, setOpen] = useState<number | null>(openMatchId ?? null)

  const load = useCallback(() => {
    setState(prev => prev === 'ready' ? 'ready' : 'loading')
    api.getMatches({ result, mode, arch: arch?.key ?? null, lang })
      .then(d => { setData(d); setSessions(d.sessions); setNext(d.nextBefore); setState('ready') })
      .catch(e => setState(e instanceof ApiError && e.code === 'player_not_linked' ? 'unlinked' : 'error'))
  }, [result, mode, arch, lang])
  useEffect(load, [load])
  useEffect(() => {
    window.addEventListener(PLUS_CHANGED, load)
    return () => window.removeEventListener(PLUS_CHANGED, load)
  }, [load])

  const loadMore = async () => {
    if (!next) return
    haptic('light')
    setMore(true)
    try {
      const d = await api.getMatches({ before: next, result, mode, arch: arch?.key ?? null, lang })
      // Заход мог разрезаться страницей — склеиваем по началу
      setSessions(prev => {
        const merged = [...prev]
        for (const sess of d.sessions) {
          const same = merged.find(x => x.startUtc === sess.startUtc)
          if (same) same.matches = [...same.matches, ...sess.matches]
          else merged.push(sess)
        }
        return merged
      })
      setNext(d.nextBefore)
    } finally {
      setMore(false)
    }
  }

  const setTracker = (tr: TrackerState) => setData(d => d ? { ...d, tracker: tr } : d)

  if (state === 'loading') return <div className="center"><div className="spinner" /></div>
  if (state === 'unlinked') return <p className="center muted small" style={{ marginTop: 16 }}>{t.battles.notLinked}</p>
  if (state === 'error' || !data) return <p className="center muted small" style={{ marginTop: 16 }}>{s.loadError}</p>

  const agg = data.aggregates
  const filtered = result !== 'all' || mode !== 'all' || arch !== null

  return (
    <div className="fade-in">
      <TrackerCard state={data.tracker} onChange={setTracker} />

      {agg && (agg.archetypes.length > 0 || agg.levels) && (
        <section className="card">
          <div className="card-title" style={{ marginBottom: 2 }}>{s.aggTitle}</div>
          <p className="muted small" style={{ margin: '0 0 8px' }}>{s.aggHint}</p>
          {agg.archetypes.map(r => (
            <button
              key={r.arch.key}
              className="trk-arch-row"
              onClick={() => {
                if (!data.unlocked) { openPlus(); return }
                haptic('light')
                setArch({ key: r.arch.key, label: r.arch.label })
              }}
            >
              {r.arch.card && <CardIcon card={r.arch.card} />}
              <span className="trk-arch-name">{r.arch.label}</span>
              <span className="muted small">{r.wins}–{r.losses}</span>
              <span className={`trk-delta ${r.deltaPp < 0 ? 'trk-bad' : 'trk-good'}`}>
                {r.deltaPp > 0 ? '+' : r.deltaPp < 0 ? '−' : ''}{Math.abs(r.deltaPp).toFixed(0)}
              </span>
            </button>
          ))}
          {agg.archetypesLocked > 0 && (
            <button className="ba-locked" onClick={() => openPlus()}>
              <span className="ba-locked-icon">🔒</span>
              <span className="ba-locked-text">{s.aggLocked.replace('{n}', String(agg.archetypesLocked))}</span>
              <span className="ba-locked-arrow">›</span>
            </button>
          )}
          {agg.levels && (
            <div className="trk-levels">
              {agg.levels.lossAvg !== null && agg.levels.winAvg !== null && (
                <span className="small">{s.levelsLine.replace('{l}', signed(agg.levels.lossAvg)).replace('{w}', signed(agg.levels.winAvg))}</span>
              )}
              {agg.levels.underWinPct !== null && (
                <span className="muted small">{s.underWin.replace('{p}', String(Math.round(agg.levels.underWinPct)))}</span>
              )}
              {agg.levels.evenWinPct !== null && (
                <span className="muted small">{s.evenWin.replace('{p}', String(Math.round(agg.levels.evenWinPct)))}</span>
              )}
            </div>
          )}
        </section>
      )}

      <div className="trk-chips">
        {RESULTS.map(r => (
          <button key={r} className={`trk-chip ${result === r ? 'trk-chip-on' : ''}`} onClick={() => { haptic('light'); setResult(r) }}>
            {r === 'all' ? s.chipAll : r === 'win' ? s.chipWin : s.chipLoss}
          </button>
        ))}
      </div>
      <div className="trk-chips">
        {MODES.map(m => (
          <button key={m} className={`trk-chip ${mode === m ? 'trk-chip-on' : ''}`} onClick={() => { haptic('light'); setMode(m) }}>
            {m === 'all' ? s.modeAll : s.modes[m]}
          </button>
        ))}
        {arch && (
          <button className="trk-chip trk-chip-on" onClick={() => setArch(null)}>
            {s.archFilter.replace('{a}', arch.label)} ✕
          </button>
        )}
      </div>

      {sessions.length === 0 ? (
        <section className="card center">
          <p className="muted small" style={{ margin: 0 }}>{filtered ? s.emptyFiltered : s.empty}</p>
        </section>
      ) : sessions.map(sess => (
        <SessionBlock key={sess.startUtc} session={sess} onOpen={id => { haptic('light'); setOpen(id) }} />
      ))}

      {next && (
        <button className="btn btn-ghost" style={{ width: '100%', marginTop: 8 }} disabled={more} onClick={loadMore}>
          {s.more}
        </button>
      )}
      {!next && data.lockedOlder > 0 && (
        <button className="ba-locked" style={{ marginTop: 8 }} onClick={() => openPlus()}>
          <span className="ba-locked-icon">🔒</span>
          <span className="ba-locked-text">{s.lockedOlder.replace('{n}', String(data.lockedOlder))}</span>
          <span className="ba-locked-arrow">›</span>
        </button>
      )}

      {open !== null && <MatchReportSheet id={open} onClose={() => setOpen(null)} />}
    </div>
  )
}

function SessionBlock({ session, onOpen }: { session: MatchSession; onOpen: (id: number) => void }) {
  const { t } = useT()
  const start = new Date(session.startUtc)
  const end = new Date(session.endUtc)
  const time = (d: Date) => d.toLocaleTimeString(t.dateLocale, { hour: '2-digit', minute: '2-digit' })
  return (
    <section className="card trk-session">
      <div className="trk-session-head">
        <span className="small">
          <b>{start.toLocaleDateString(t.dateLocale, { day: 'numeric', month: 'short' })}</b>
          {' '}{time(start)}–{time(end)}
        </span>
        <span className="small">
          {session.wins}–{session.losses}
          {' · '}{signed(session.trophies)}🏆
          {session.tilt && ' · 🧊'}
        </span>
      </div>
      <div className="tilt-series-cells" style={{ margin: '4px 0 6px' }}>
        {session.strip.split('').map((r, i) => (
          <span key={i} className={`tilt-cell tilt-cell-${r}`} />
        ))}
      </div>
      {session.matches.map(m => <Row key={m.id} m={m} onOpen={onOpen} />)}
    </section>
  )
}

function Row({ m, onOpen }: { m: MatchRow; onOpen: (id: number) => void }) {
  return (
    <button className="trk-row" onClick={() => onOpen(m.id)}>
      <span className={`trk-dot ${m.result > 0 ? 'trk-dot-w' : m.result < 0 ? 'trk-dot-l' : 'trk-dot-d'}`} />
      <span className="trk-score">{m.crownsFor}–{m.crownsAgainst}</span>
      <span className="trk-row-main">
        <span className="trk-row-arch">{m.arch?.label ?? '—'}</span>
        <span className="muted small trk-row-opp">{m.oppName ?? ''}</span>
      </span>
      <span className="trk-row-cards">
        {m.keyCards.map((c, i) => <CardIcon key={`${c.cardId}-${i}`} card={c} />)}
      </span>
      <span className="trk-row-tail">
        {m.trophyChange !== null && <span className="small">{signed(m.trophyChange)}</span>}
        {m.verdict && <span className="trk-verdict-icon">{VERDICT_ICON[m.verdict] ?? '💡'}</span>}
      </span>
    </button>
  )
}

export function signed(n: number) {
  const v = Math.round(n * 10) / 10
  return v > 0 ? `+${v}` : v < 0 ? `−${-v}` : '0'
}
