import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { api, ApiError } from '../lib/api'
import type { DuelLeague, DuelProfileView, DuelRow, DuelTopRow } from '../types'
import { haptic, hapticNotify, switchInline, openTelegramLink, botStartLink, tg } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { useCountUp } from '../lib/anim'
import { usePlayerSheet } from '../lib/playerSheet'
import { allRanks, LEAGUE_COLORS, rankName, rankOf } from '../lib/duelRank'
import { RankEmblem } from './duel/RankEmblem'
import {
  IconClipboard, IconClock, IconFlame, IconLink, IconSend,
  IconSwords, IconTarget, IconTrendUp, IconTrophy, IconUsers, IconX, IconCheck,
} from './duel/DuelIcons'
import { SectionHead } from './ui/Section'
import { InfoButton } from './ui/Info'

/** Пока идёт дуэль — опрашиваем чаще: счёт меняется прямо во время игры. */
const ACTIVE_POLL = 15_000
const IDLE_POLL = 60_000

/** Цвета лиги — в CSS-переменные: фон шапки, полоса прогресса и подсветки берут их оттуда. */
function leagueVars(league: keyof typeof LEAGUE_COLORS): CSSProperties {
  const c = LEAGUE_COLORS[league]
  return { '--lg-hi': c.hi, '--lg-mid': c.mid, '--lg-lo': c.lo, '--lg-glow': c.glow } as CSSProperties
}

/**
 * «Лига дуэлей»: ранг и кубки, вызов в любой чат, лестница рангов, топ и история.
 *
 * Вызов уходит карточкой через инлайн-режим бота, а счёт бот берёт из журнала боёв
 * сам. Отсюда - смотреть и звать: сама дуэль живёт в чате, где её приняли.
 */
export function DuelView() {
  const { t } = useT()
  const s = t.duel
  const [data, setData] = useState<DuelLeague | null>(null)
  const [error, setError] = useState(false)

  const load = useCallback(() => {
    api.getDuels().then(d => { setData(d); setError(false) }).catch(() => setError(true))
  }, [])
  useEffect(load, [load])

  const active = Boolean(data?.active)
  useEffect(() => {
    const id = window.setInterval(() => { if (!document.hidden) load() }, active ? ACTIVE_POLL : IDLE_POLL)
    return () => window.clearInterval(id)
  }, [load, active])

  if (!data) {
    return error
      ? <p className="center muted" style={{ marginTop: 24 }}>{t.trk.loadError}</p>
      : <div className="center"><div className="spinner" /></div>
  }

  const challenge = () => {
    haptic('medium')
    // Нет инлайн-метода (старый клиент) - в личку бота, там та же кнопка
    if (!switchInline('duel')) openTelegramLink(botStartLink('duel'))
  }

  return (
    <div className="fade-in dl">
      {data.me ? <Hero me={data.me} t={t} onChallenge={challenge} /> : <Pitch t={t} />}

      {!data.me && (
        data.linked
          ? <LinkForm t={t} mode="join" onSaved={setData} />
          : <section className="card dl-card"><p className="muted small" style={{ margin: 0 }}>{s.notLinked}</p></section>
      )}

      {data.active && <ActiveCard d={data.active} t={t} />}

      <Ladder trophies={data.me?.rating ?? null} t={t} />

      <section className="card dl-card">
        <SectionHead icon="crown" tone="gold" title={s.topTitle}
          aside={<><IconUsers size={14} /> {s.players.replace('{n}', String(data.players))}</>} />
        {data.top.length === 0
          ? <p className="muted small" style={{ margin: 0 }}>{s.emptyTop}</p>
          : <Top rows={data.top} t={t} />}
      </section>

      {data.me && (
        <section className="card dl-card">
          <SectionHead icon="swords" tone="violet" title={s.myTitle} />
          {data.mine.length === 0
            ? <p className="muted small" style={{ margin: 0 }}>{s.noDuels}</p>
            : <DuelList rows={data.mine} myTag={data.me.tag} t={t} />}
        </section>
      )}

      {data.recent.length > 0 && (
        <section className="card dl-card">
          <SectionHead icon="bolt" tone="orange" title={s.recentTitle} />
          <DuelList rows={data.recent} myTag={data.me?.tag ?? null} t={t} />
        </section>
      )}

      {data.me && (
        <details className="card dl-card dl-change">
          <summary><IconLink size={16} /> {s.changeLink}</summary>
          <LinkForm t={t} mode="change" onSaved={setData} />
        </details>
      )}
    </div>
  )
}

/** Кнопка «i» в шапке лиги: как устроены дуэли — вместо отдельной карточки внизу. */
function HowInfo({ t, lead }: { t: Translations; lead?: ReactNode }) {
  const s = t.duel
  return (
    <InfoButton className="mx-dl-hero-info" size={28} title={s.howTitle}>
      {lead}
      <HowList t={t} />
      <p className="muted small">{s.unofficial}</p>
    </InfoButton>
  )
}

/** Шапка участника: эмблема ранга, кубки (докручиваются), путь до следующего ранга, статистика. */
function Hero({ me, t, onChallenge }: { me: DuelProfileView; t: Translations; onChallenge: () => void }) {
  const s = t.duel
  const trophies = useCountUp(me.rating, true, 1100)
  const next = me.nextFloor
  const pct = next === null ? 100 : Math.max(5, Math.min(100, Math.round((me.rating - me.floor) / (next - me.floor) * 100)))
  const nextRank = next === null ? null : rankOf(next)
  const winrate = me.games > 0 ? Math.round(me.wins / me.games * 100) : null

  return (
    <section className={`dl-hero dl-hero-${me.league}`} style={leagueVars(me.league)}>
      <div className="dl-aurora" />
      <HowInfo t={t} lead={<p>{s.challengeHint}</p>} />
      <div className="dl-sparks">{Array.from({ length: 10 }, (_, i) => <i key={i} style={{ '--i': i } as CSSProperties} />)}</div>

      <div className="dl-hero-main">
        <div className="dl-hero-emblem">
          <RankEmblem league={me.league} division={me.division} size={118} />
        </div>
        <div className="dl-hero-info">
          <span className="dl-place"><IconTrophy size={13} /> #{me.rank}</span>
          <span className="dl-rankname">{rankName(me.league, me.division, t)}</span>
          <span className="dl-trophies">
            <IconTrophy size={26} className="dl-trophy-ic" />
            <b>{Math.round(trophies)}</b>
          </span>
          <span className="dl-player">{me.name}</span>
        </div>
      </div>

      <div className="dl-progress">
        <div className="dl-progress-bar"><i style={{ width: `${pct}%` }} /></div>
        <div className="dl-progress-text">
          {next === null || !nextRank
            ? <><IconFlame size={14} /> {s.maxLeague}</>
            : <>
                <span>{me.floor}</span>
                <span className="dl-progress-next">
                  <RankEmblem league={nextRank.league} division={nextRank.division} size={20} animated={false} />
                  {s.toNext.replace('{league}', rankName(nextRank.league, nextRank.division, t)).replace('{n}', String(next - me.rating))}
                </span>
                <span>{next}</span>
              </>}
        </div>
      </div>

      <div className="dl-stats">
        <Stat icon={<IconCheck size={16} />} value={me.wins} label={s.wins} tone="win" />
        <Stat icon={<IconX size={16} />} value={me.losses} label={s.losses} tone="loss" />
        <Stat icon={<IconTarget size={16} />} value={winrate === null ? '—' : `${winrate}%`} label={s.winrate} />
        <Stat icon={<IconTrendUp size={16} />} value={me.peak} label={s.peak} />
      </div>

      <button className="dl-cta" onClick={onChallenge}>
        <span className="dl-cta-shine" />
        <IconSwords size={22} />
        {s.challenge}
      </button>
    </section>
  )
}

function Stat({ icon, value, label, tone }: { icon: ReactNode; value: ReactNode; label: string; tone?: 'win' | 'loss' }) {
  return (
    <span className={`dl-stat ${tone ? `dl-stat-${tone}` : ''}`}>
      <span className="dl-stat-ic">{icon}</span>
      <b>{value}</b>
      <small>{label}</small>
    </span>
  )
}

/** Шапка для тех, кто ещё не в лиге: веер эмблем и что это такое. */
function Pitch({ t }: { t: Translations }) {
  const s = t.duel
  return (
    <section className="dl-hero dl-hero-pitch" style={leagueVars('master')}>
      <div className="dl-aurora" />
      <HowInfo t={t} />
      <div className="dl-sparks">{Array.from({ length: 10 }, (_, i) => <i key={i} style={{ '--i': i } as CSSProperties} />)}</div>
      <div className="dl-fan">
        <span className="dl-fan-l"><RankEmblem league="gold" division={1} size={74} /></span>
        <span className="dl-fan-c"><RankEmblem league="legend" division={0} size={112} /></span>
        <span className="dl-fan-r"><RankEmblem league="diamond" division={1} size={74} /></span>
      </div>
      <h2 className="dl-title">{s.title}</h2>
      <p className="dl-pitch">{s.pitch}</p>
    </section>
  )
}

/** Лестница рангов: пройденные — в цвете, текущий — крупно, впереди — приглушённые. */
function Ladder({ trophies, t }: { trophies: number | null; t: Translations }) {
  const s = t.duel
  const ranks = allRanks()
  const current = trophies === null ? -1 : (() => {
    const r = rankOf(trophies)
    return ranks.findIndex(x => x.league === r.league && x.division === r.division)
  })()
  const rowRef = useRef<HTMLDivElement>(null)
  const curRef = useRef<HTMLDivElement>(null)

  // Текущий ранг — в середину ленты, а не за краем экрана
  useLayoutEffect(() => {
    const row = rowRef.current, cur = curRef.current
    if (!row || !cur) return
    row.scrollLeft = cur.offsetLeft - row.clientWidth / 2 + cur.clientWidth / 2
  }, [current])

  return (
    <section className="card dl-card">
      <SectionHead icon="shieldCheck" tone="blue" title={s.ranksTitle} info={s.ranksHint} />
      <div className="dl-ladder" ref={rowRef}>
        {ranks.map((r, i) => {
          const state = current < 0 ? 'future' : i < current ? 'past' : i === current ? 'now' : 'future'
          return (
            <div key={`${r.league}${r.division}`} ref={i === current ? curRef : undefined}
              className={`dl-step dl-step-${state}`} style={leagueVars(r.league)}>
              <RankEmblem league={r.league} division={r.division} size={state === 'now' ? 64 : 46}
                animated={state === 'now'} dim={state === 'future'} />
              <span className="dl-step-name">{rankName(r.league, r.division, t)}</span>
              <span className="dl-step-floor"><IconTrophy size={10} /> {r.floor}</span>
            </div>
          )
        })}
      </div>
    </section>
  )
}

/** Ссылка «добавить в друзья»: вступление в лигу или смена ссылки. */
function LinkForm({ t, mode, onSaved }: { t: Translations; mode: 'join' | 'change'; onSaved: (d: DuelLeague) => void }) {
  const s = t.duel
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const paste = async () => {
    haptic('light')
    try {
      const text = await navigator.clipboard?.readText?.()
      if (text) { setValue(text.trim()); return }
    } catch { /* вебвью не дал прочитать буфер - вставят руками */ }
    tg?.readTextFromClipboard?.(text => { if (text) setValue(text.trim()) })
  }

  const save = async () => {
    if (!value.trim()) return
    haptic('medium')
    setSaving(true)
    setMsg(null)
    try {
      const d = await api.setDuelLink(value.trim(), t.dateLocale.slice(0, 2))
      hapticNotify('success')
      if (mode === 'change') { setMsg({ ok: true, text: s.saved }); setValue('') }
      onSaved(d)
    } catch (e) {
      hapticNotify('error')
      const text = e instanceof ApiError && e.code === 'bad_link' ? s.errBad
        : e instanceof ApiError && e.code === 'wrong_tag' ? s.errWrongTag
        : e instanceof ApiError && e.code === 'player_not_linked' ? s.notLinked
        : s.errGeneric
      setMsg({ ok: false, text })
    } finally {
      setSaving(false)
    }
  }

  const steps = s.joinPath.split('→').map(x => x.trim())

  return (
    <section className={mode === 'join' ? 'card dl-card dl-join' : 'dl-join dl-join-inline'}>
      {mode === 'join' && (
        <>
          <SectionHead icon="link" tone="violet" title={s.joinTitle} info={s.joinText} />
          <ol className="dl-path">
            {steps.map((step, i) => <li key={i}><b>{i + 1}</b>{step}</li>)}
          </ol>
        </>
      )}
      <div className="dl-input-row">
        <span className="dl-input-ic"><IconLink size={16} /></span>
        <input
          className="dl-input"
          inputMode="url"
          autoComplete="off"
          placeholder={s.placeholder}
          value={value}
          onChange={e => setValue(e.target.value)}
        />
        <button className="dl-paste" onClick={paste}><IconClipboard size={16} /> {s.paste}</button>
      </div>
      <button className={mode === 'join' ? 'dl-cta dl-cta-solo' : 'dl-save'} disabled={saving || !value.trim()} onClick={save}>
        {mode === 'join' && <span className="dl-cta-shine" />}
        {mode === 'join' && <IconSwords size={20} />}
        {saving ? s.saving : mode === 'join' ? s.joinBtn : s.saveLink}
      </button>
      {msg && <p className={`small ${msg.ok ? 'dl-ok' : 'dl-err'}`} style={{ margin: 0 }}>{msg.text}</p>}
    </section>
  )
}

/** Идущая дуэль: вращающаяся рамка, живая точка и крупный счёт. */
function ActiveCard({ d, t }: { d: DuelRow; t: Translations }) {
  const s = t.duel
  return (
    <section className="dl-active">
      <div className="dl-active-inner">
        <div className="dl-active-top">
          <span className="dl-live"><i /> {s.live}</span>
          <span className="dl-active-title">{s.activeTitle}</span>
          <span className="dl-bo">Bo{d.bestOf}</span>
        </div>
        <div className="dl-vs">
          <span className="dl-vs-name">{d.aName}</span>
          <span className="dl-vs-score">
            <b key={`a${d.scoreA}`}>{d.scoreA}</b>
            <em>{s.vs}</em>
            <b key={`b${d.scoreB}`}>{d.scoreB}</b>
          </span>
          <span className="dl-vs-name dl-right">{d.bName}</span>
        </div>
        <p className="dl-active-hint"><IconClock size={13} /> {s.activeHint}</p>
      </div>
    </section>
  )
}

/** Топ: пьедестал на троих и список остальных. */
function Top({ rows, t }: { rows: DuelTopRow[]; t: Translations }) {
  const s = t.duel
  const openPlayer = usePlayerSheet()
  const podium = rows.slice(0, 3)
  const rest = rows.slice(3)
  // Порядок на пьедестале: второй, первый, третий
  const order = [podium[1], podium[0], podium[2]].filter(Boolean) as DuelTopRow[]

  return (
    <>
      <div className="dl-podium">
        {order.map(r => (
          <button key={r.tag} className={`dl-pod dl-pod-${r.rank} ${r.me ? 'dl-pod-me' : ''}`} style={leagueVars(r.league)}
            onClick={() => { haptic('light'); openPlayer(r.tag) }}>
            <RankEmblem league={r.league} division={r.division} size={r.rank === 1 ? 64 : 50} animated={r.rank === 1} />
            <span className="dl-pod-name">{r.me ? s.you : r.name}</span>
            <span className="dl-pod-pts"><IconTrophy size={12} /> {r.rating}</span>
            <span className="dl-pod-base"><b>{r.rank}</b></span>
          </button>
        ))}
      </div>
      {rest.length > 0 && (
        <ol className="dl-board">
          {rest.map(r => (
            <li key={r.tag} className={r.me ? 'dl-me' : ''} onClick={() => { haptic('light'); openPlayer(r.tag) }}>
              <span className="dl-pos">{r.rank}</span>
              <RankEmblem league={r.league} division={r.division} size={34} animated={false} />
              <span className="dl-who">
                <b>{r.me ? `${r.name} · ${s.you}` : r.name}</b>
                <small>{rankName(r.league, r.division, t)} · {r.wins}–{r.losses}</small>
              </span>
              <span className="dl-pts"><IconTrophy size={13} /> {r.rating}</span>
            </li>
          ))}
        </ol>
      )}
    </>
  )
}

function DuelList({ rows, myTag, t }: { rows: DuelRow[]; myTag: string | null; t: Translations }) {
  const s = t.duel
  return (
    <ul className="dl-list">
      {rows.map(d => {
        const aWon = d.scoreA > d.scoreB
        const mineSide = myTag === d.aTag ? 'a' : myTag === d.bTag ? 'b' : null
        const delta = mineSide === 'a' ? d.deltaA : mineSide === 'b' ? d.deltaB : null
        const iWon = mineSide === null ? null : (mineSide === 'a') === aWon
        const note = d.state === 'expired' ? s.expired : d.state === 'cancelled' ? s.cancelled : !d.rated ? s.unrated : null
        const tone = d.state !== 'finished' ? 'off' : iWon === null ? 'neutral' : iWon ? 'win' : 'loss'
        return (
          <li key={d.id} className={`dl-row dl-row-${tone}`}>
            <span className={`dl-side ${d.state === 'finished' && aWon ? 'dl-win' : ''}`}>{d.aName}</span>
            <span className="dl-score">{d.scoreA}<i>:</i>{d.scoreB}</span>
            <span className={`dl-side dl-right ${d.state === 'finished' && !aWon ? 'dl-win' : ''}`}>{d.bName}</span>
            <span className="dl-meta">
              <span className="dl-chip">Bo{d.bestOf}</span>
              {note && <span className="dl-chip dl-chip-muted">{note}</span>}
              {delta !== null && d.state === 'finished' && d.rated && (
                <span className={`dl-chip ${delta >= 0 ? 'dl-up' : 'dl-down'}`}>
                  <IconTrophy size={11} /> {delta >= 0 ? `+${delta}` : `−${-delta}`}
                </span>
              )}
            </span>
          </li>
        )
      })}
    </ul>
  )
}

/** Как это работает: четыре шага с иконками на светящейся линии (в листе под «i»). */
function HowList({ t }: { t: Translations }) {
  const s = t.duel
  const icons = [<IconSend size={18} />, <IconUsers size={18} />, <IconSwords size={18} />, <IconTrophy size={18} />]
  return (
    <ol className="dl-how mx-dl-how">
      {s.how.map((line, i) => (
        <li key={i} style={{ animationDelay: `${i * 80}ms` }}>
          <span className="dl-how-ic">{icons[i]}</span>
          <span>{line}</span>
        </li>
      ))}
    </ol>
  )
}
