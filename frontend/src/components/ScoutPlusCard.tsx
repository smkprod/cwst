import { useEffect, useMemo, useRef, useState } from 'react'
import { api } from '../lib/api'
import type { ScoutPlayerPlay, ScoutPlus, ScoutPlusClan, ScoutPlusPlayer, ScoutTiming } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

type ProT = Translations['clanPro']

/** Момент дня: начало военного дня плюс часы - в часах телефона, с точностью до 10 минут. */
function clock(dayStartUtc: string, hours: number): string {
  const ms = Date.parse(dayStartUtc) + Math.round(hours * 6) * 600_000
  return new Date(ms).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
}

const fill = (s: string, vars: Record<string, string | number>) =>
  Object.entries(vars).reduce((acc, [k, v]) => acc.split(`{${k}}`).join(String(v)), s)

/**
 * «Разведка+» (Клан Про): соперник изнутри. Сверху - выбор клана гонки, ниже его
 * распорядок дня (когда играют, когда приходят очки, к которому часу доигрывают)
 * и кто у них приносит медали - с колодами по нажатию.
 */
export function ScoutPlusCard({ data }: { data: ScoutPlus }) {
  const { t } = useT()
  const p = t.clanPro
  const rivals = useMemo(() => data.clans.filter(c => !c.isOurClan), [data])
  const ours = data.clans.find(c => c.isOurClan) ?? null
  const ordered = useMemo(() => [...rivals, ...(ours ? [ours] : [])], [rivals, ours])
  const [tag, setTag] = useState(ordered[0]?.tag ?? '')
  const clan = ordered.find(c => c.tag === tag) ?? ordered[0]

  return (
    <section className="card sp-card fade-in">
      <SectionHead icon="radar" tone="gold" title={p.scoutTitle} aside={<span className="pro-badge">{p.badge}</span>} />
      <p className="muted small sp-lead">{p.scoutLead}</p>

      <div className="sp-clans" role="tablist">
        {ordered.map(c => (
          <button key={c.tag} role="tab" aria-selected={c.tag === clan?.tag}
            className={`sp-clan ${c.tag === clan?.tag ? 'on' : ''} ${c.isOurClan ? 'ours' : ''}`}
            onClick={() => { haptic('light'); setTag(c.tag) }}>
            <span className="sp-clan-name">{c.isOurClan ? `${p.us} · ` : ''}{c.name}</span>
            <span className="sp-clan-fame cl-ic">{fmt(c.fame)} <Icon name="medal" size={11} className="cl-fame" /></span>
          </button>
        ))}
      </div>

      {clan && <ClanScout key={clan.tag} clan={clan} ours={clan.isOurClan ? null : ours} data={data} p={p} />}
    </section>
  )
}

function ClanScout({ clan, ours, data, p }: { clan: ScoutPlusClan; ours: ScoutPlusClan | null; data: ScoutPlus; p: ProT }) {
  return (
    <div className="sp-body fade-in">
      {data.isWarDay && (
        <div className="sp-stats">
          <Stat value={fmt(clan.decksToday)} label={p.statDecks} />
          <Stat value={String(clan.activeToday)} label={p.statActive} />
          <Stat value={String(clan.fighters)} label={p.statFighters} />
        </div>
      )}

      <Schedule timing={clan.timing} ours={ours?.timing ?? null} data={data} p={p} />

      <h4 className="sp-sub"><Icon name="crown" size={14} /> {p.top}</h4>
      {clan.top.length === 0
        ? <p className="muted small">{data.isWarDay ? p.collecting : p.training}</p>
        : (
          <ol className="sp-top">
            {clan.top.map((pl, i) => <TopPlayer key={pl.tag} player={pl} place={i + 1} dayStartUtc={data.dayStartUtc} p={p} />)}
          </ol>
        )}
    </div>
  )
}

function Stat({ value, label }: { value: string; label: string }) {
  return (
    <div className="sp-stat">
      <b>{value}</b>
      <span className="muted small">{label}</span>
    </div>
  )
}

/**
 * Распорядок дня: столбик - доля колод в этом часу, золотом - окно пика медалей,
 * черта - «сейчас». По этой картинке глава видит, когда соперник садится играть.
 */
function Schedule({ timing, ours, data, p }: { timing: ScoutTiming | null; ours: ScoutTiming | null; data: ScoutPlus; p: ProT }) {
  if (!timing) {
    return (
      <div className="sp-sched sp-sched-empty">
        <Icon name="hourglass" size={16} />
        <span className="muted small">{data.isWarDay ? p.collecting : p.training}</span>
      </div>
    )
  }

  const max = Math.max(1, ...timing.hourDecks)
  const inPeak = (h: number) => h === timing.peakHour || h === (timing.peakHour + 1) % 24
  const now = data.isWarDay ? data.hoursIntoDay : null

  const pace = timing.todayPercentNow != null && timing.usualPercentNow != null && data.isWarDay
    ? { today: timing.todayPercentNow, usual: timing.usualPercentNow }
    : null
  const paceText = pace
    ? fill(pace.today >= pace.usual + 10 ? p.faster : pace.today <= pace.usual - 10 ? p.slower : p.even, pace)
    : null
  const paceKind = pace ? (pace.today >= pace.usual + 10 ? 'up' : pace.today <= pace.usual - 10 ? 'down' : 'even') : null

  return (
    <div className="sp-sched">
      <div className="sp-sched-head">
        <span className="sp-sub-inline"><Icon name="clock" size={14} /> {p.schedule}</span>
        <span className="muted small">{fill(p.daysObs, { n: timing.daysObserved })}</span>
      </div>

      <div className="sp-bars" aria-hidden>
        {timing.hourDecks.map((v, h) => (
          <span key={h} className={`sp-bar ${inPeak(h) ? 'peak' : ''}`} style={{ height: `${8 + (v / max) * 92}%` }} />
        ))}
        {now != null && <span className="sp-now" style={{ left: `${(now / 24) * 100}%` }} />}
      </div>
      <div className="sp-axis muted small">
        {[0, 6, 12, 18, 24].map(h => <span key={h}>{clock(data.dayStartUtc, h)}</span>)}
      </div>

      <div className="sp-facts">
        <Fact icon="flame" tone="gold" label={p.peak}
          value={`${clock(data.dayStartUtc, timing.peakHour)}–${clock(data.dayStartUtc, timing.peakHour + 2)}`}
          sub={fill(p.peakShare, { p: timing.peakSharePercent })} />
        {timing.halfDoneHour != null && (
          <Fact icon="activity" tone="blue" label={p.half} value={clock(data.dayStartUtc, timing.halfDoneHour)} />
        )}
        {timing.mostlyDoneHour != null && (
          <Fact icon="flag" tone="green" label={p.mostly} value={clock(data.dayStartUtc, timing.mostlyDoneHour)}
            sub={ours?.mostlyDoneHour != null ? `${p.us}: ${clock(data.dayStartUtc, ours.mostlyDoneHour)}` : undefined} />
        )}
      </div>

      {paceText && (
        <p className={`sp-pace sp-pace-${paceKind}`}>
          <Icon name={paceKind === 'up' ? 'trendUp' : paceKind === 'down' ? 'trendDown' : 'activity'} size={14} /> {paceText}
        </p>
      )}
    </div>
  )
}

function Fact({ icon, tone, label, value, sub }: {
  icon: 'flame' | 'activity' | 'flag'; tone: string; label: string; value: string; sub?: string
}) {
  return (
    <div className={`sp-fact sp-fact-${tone}`}>
      <span className="sp-fact-label"><Icon name={icon} size={12} /> {label}</span>
      <b>{value}</b>
      {sub && <span className="muted small">{sub}</span>}
    </div>
  )
}

type Play = { kind: 'idle' } | { kind: 'loading' } | { kind: 'error' } | { kind: 'ready'; data: ScoutPlayerPlay }

function TopPlayer({ player, place, dayStartUtc, p }: { player: ScoutPlusPlayer; place: number; dayStartUtc: string; p: ProT }) {
  const [open, setOpen] = useState(false)
  const [play, setPlay] = useState<Play>({ kind: 'idle' })
  const started = useRef(false)
  const alive = useRef(true)
  useEffect(() => { alive.current = true; return () => { alive.current = false } }, [])

  // Бои грузим один раз - при первом раскрытии; повторное раскрытие берёт готовое
  useEffect(() => {
    if (!open || started.current) return
    started.current = true
    setPlay({ kind: 'loading' })
    api.getScoutPlusPlayer(player.tag)
      .then(data => { if (alive.current) setPlay({ kind: 'ready', data }) })
      .catch(() => { if (alive.current) setPlay({ kind: 'error' }) })
  }, [open, player.tag])

  // 200 медалей за колоду - победа в каждом бою; 150 - половина побед
  const grade = player.famePerDeck >= 190 ? 'top' : player.famePerDeck >= 150 ? 'good' : 'low'

  return (
    <li className={`sp-player ${open ? 'open' : ''}`}>
      <button className="sp-player-row" onClick={() => { haptic('light'); setOpen(o => !o) }} aria-expanded={open}>
        <span className={`sp-place sp-place-${place}`}>{place}</span>
        <span className="sp-player-info">
          <span className="sp-player-name">{player.name}</span>
          <span className="muted small">
            {player.decks} {p.decks}
            {player.usualHour != null && ` · ${fill(p.finishesAt, { t: clock(dayStartUtc, player.usualHour) })}`}
          </span>
        </span>
        <span className="sp-player-right">
          <span className="cl-ic"><b>{fmt(player.fame)}</b> <Icon name="medal" size={12} className="cl-fame" /></span>
          <span className={`sp-fpd sp-fpd-${grade}`}>{player.famePerDeck} {p.fpd}</span>
        </span>
        <span className={`cl-chev ${open ? 'cl-chev-open' : ''}`}><Icon name="chevronRight" size={16} /></span>
      </button>

      {open && (
        <div className="sp-play fade-in">
          {play.kind === 'loading' && <p className="muted small"><span className="spinner spinner-inline" /> {p.loadingPlay}</p>}
          {play.kind === 'error' && <p className="muted small">{p.error}</p>}
          {play.kind === 'ready' && (
            <>
              <p className="small sp-record">
                <Icon name="swords" size={13} /> {fill(p.warRecord, { w: play.data.warWins, l: play.data.warLosses })}
              </p>
              {play.data.decks.length === 0 && <p className="muted small">{p.noBattles}</p>}
              {play.data.decks.map((d, i) => (
                <div key={i} className="sp-deck">
                  <div className="wtop-deck">
                    {d.cards.map((c, j) => (
                      <span key={`${c.name}-${j}`} className={`wtop-deck-card ${c.evo ? 'sp-evo' : ''}`} title={c.name}>
                        {c.iconUrl ? <img src={c.iconUrl} alt={c.name} loading="lazy" /> : <span className="wtop-card-blank" />}
                      </span>
                    ))}
                  </div>
                  <span className="muted small">{fill(p.deckGames, { g: d.games, w: d.wins })}</span>
                </div>
              ))}
            </>
          )}
        </div>
      )}
    </li>
  )
}
