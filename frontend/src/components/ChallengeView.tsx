import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Challenge, ChallengeRow } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { REDUCED, useCountUp } from '../lib/anim'
import { usePlusSheet } from '../lib/plusSheet'
import { usePlayerSheet } from '../lib/playerSheet'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'
import { InfoButton } from './ui/Info'

/** Пока челлендж идёт — таблица обновляется каждые 15 секунд, иначе раз в минуту. */
const LIVE_POLL = 15_000
const IDLE_POLL = 60_000

/**
 * «🎟 Уикенд-челлендж»: живая таблица билетов.
 *
 * Победа в ладдере или Пути легенд — билет, серия из трёх побед — ещё один. Билеты
 * бот считает сам по журналу боёв, поэтому таблица меняется прямо во время игры:
 * сыграл — открыл — твоя строка уже выросла.
 */
export function ChallengeView({ code = null }: {
  /** Код челленджа блогера (ссылка со стрима). Без кода — общий уикенд-челлендж. */
  code?: string | null
} = {}) {
  const { t } = useT()
  const s = t.ch
  const openPlus = usePlusSheet()
  const [data, setData] = useState<Challenge | null>(null)
  const [error, setError] = useState<'load' | 'missing' | null>(null)
  const [joining, setJoining] = useState(false)
  const [now, setNow] = useState(() => Date.now())

  const load = useCallback(() => {
    api.getChallenge(code).then(d => { setData(d); setError(null) })
      // Блогер удалил челлендж — это не сбой сети, и «попробуй позже» тут соврало бы
      .catch(e => setError(e instanceof ApiError && e.code === 'challenge_not_found' ? 'missing' : 'load'))
  }, [code])
  useEffect(load, [load])

  // Живой опрос: чаще, пока идёт, и только когда страница на экране
  const live = data?.event.status === 'live'
  useEffect(() => {
    const id = window.setInterval(() => { if (!document.hidden) load() }, live ? LIVE_POLL : IDLE_POLL)
    return () => window.clearInterval(id)
  }, [load, live])

  // Секундный тик для таймера и «обновлено N с назад»
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [])

  const join = async () => {
    haptic('medium')
    setJoining(true)
    try {
      setData(await api.joinChallenge(code))
      hapticNotify('success')
    } catch (e) {
      hapticNotify('error')
      if (e instanceof ApiError && e.status === 409) load()
      else if (e instanceof ApiError && e.code === 'challenge_not_found') { setData(null); setError('missing') }
    } finally {
      setJoining(false)
    }
  }

  if (!data) {
    return error
      ? <p className="center muted" style={{ marginTop: 24 }}>{error === 'missing' ? s.notFound : t.trk.loadError}</p>
      : <div className="center"><div className="spinner" /></div>
  }

  const ev = data.event
  const start = new Date(ev.startUtc).getTime()
  const end = new Date(ev.endUtc).getTime()
  // Статус с сервера может отстать на опрос — считаем по часам, чтобы таймер не уходил в минус
  const status = now < start ? 'upcoming' : now <= end ? 'live' : 'ended'
  const target = status === 'upcoming' ? start : end
  const winner = status === 'ended' ? data.leaders[0] : null
  // Плюс в подарок и его реклама — часть только общего события: у блогера свой
  // приз и свои зрители, и чужое предложение посреди его челленджа было бы лишним.
  const isCreator = Boolean(ev.code)

  return (
    <div className="fade-in ch">
      <section className={`ch-hero ch-${status}`}>
        <div className="ch-hero-glow" />
        <div className="ch-hero-top">
          <span className={`ch-pill ch-pill-${status}`}>
            {status === 'live' && <i className="ch-live-dot" />}
            {status === 'live' ? s.liveDot : status === 'upcoming' ? s.upcoming : s.ended}
          </span>
          <span className="mx-ch-hero-aside">
            <span className="ch-prize mx-ch-prize"><Icon name="trophy" size={15} /> {ev.prize ?? s.defaultPrize}</span>
            <InfoButton className="mx-ch-info" title={ev.title ?? s.title}>
              <ul className="mx-ch-rules">
                <li><span>{s.rule1}</span> <Icon name="ticket" size={15} /></li>
                <li><span>{s.rule2}</span> <Icon name="ticket" size={15} /></li>
                <li><span>{s.rule3}</span> <Icon name="trophy" size={15} /></li>
              </ul>
              <p className="muted small">{s.unofficial}</p>
            </InfoButton>
          </span>
        </div>
        <h2 className="ch-title">{ev.title ?? s.title}</h2>
        {ev.host && <p className="ch-host"><Icon name="user" size={14} /> {s.host.replace('{name}', ev.host)}</p>}
        {status !== 'ended' ? (
          <>
            <span className="ch-count-label">{status === 'upcoming' ? s.startsIn : s.endsIn}</span>
            <Countdown ms={target - now} t={t} />
          </>
        ) : winner ? (
          <p className="ch-winner">{s.winner.replace('{name}', winner.name).replace('{t}', String(winner.tickets))}</p>
        ) : null}
        <div className="ch-dates muted small">
          {fmtRange(ev.startUtc, ev.endUtc, t)}
        </div>
      </section>

      {status !== 'ended' && !isCreator && (
        ev.giftPlus && data.joined
          ? <section className="card ch-plus ch-plus-gift">{s.chGiftOn}</section>
          : (
            <section className="card ch-plus">
              <span>{s.chPlusPitch}</span>
              <button className="btn-mini" onClick={() => { haptic('light'); openPlus() }}>{s.chPlusBtn}</button>
            </section>
          )
      )}

      {!data.joined ? (
        <section className="card ch-join">
          {status === 'ended' ? (
            <p className="muted small" style={{ margin: 0 }}>{s.endedJoin}</p>
          ) : !data.linked ? (
            <p className="muted small" style={{ margin: 0 }}>{s.notLinked}</p>
          ) : (
            <button className="ch-join-btn" disabled={joining} onClick={join}>
              <span className="ch-join-shine" />
              {joining ? s.joining : s.join}
            </button>
          )}
        </section>
      ) : data.me && <MyCard me={data.me} leader={data.leaders[0]?.tickets ?? 0} t={t} />}

      <section className="card ch-board">
        <SectionHead className="mx-ch-board-head" icon="list" tone="gold" title={s.board} info={s.honest} aside={
          <span className="muted small">
            {s.participants.replace('{n}', String(data.participants))}
            {status === 'live' && <> · <i className="ch-live-dot" /> {s.updated.replace('{s}', String(Math.max(0, Math.round((now - new Date(data.updatedUtc).getTime()) / 1000))))}</>}
          </span>
        } />
        {data.leaders.length === 0
          ? <p className="muted small" style={{ margin: '8px 0 0' }}>{s.empty}</p>
          : <Board rows={data.leaders} me={data.me} />}
      </section>
    </div>
  )
}

/**
 * Челлендж блогера, открытый по ссылке со стрима, — отдельным экраном поверх
 * вкладок: зритель пришёл за ним, но должен и вернуться в обычное приложение.
 */
export function CreatorChallengeScreen({ code, onBack }: { code: string; onBack: () => void }) {
  const { t } = useT()
  return (
    <div className="fade-in">
      <button className="btn-mini more-back" onClick={() => { haptic('light'); onBack() }}>
        <Icon name="chevronLeft" size={15} /> {t.ch.toApp}
      </button>
      <ChallengeView code={code} />
    </div>
  )
}

function fmtRange(a: string, b: string, t: Translations) {
  const o: Intl.DateTimeFormatOptions = { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }
  return `${new Date(a).toLocaleString(t.dateLocale, o)} — ${new Date(b).toLocaleString(t.dateLocale, o)}`
}

/** Таймер «дд : чч : мм : сс» — цифры в плитках, секунды тикают. */
function Countdown({ ms, t }: { ms: number; t: Translations }) {
  const total = Math.max(0, Math.floor(ms / 1000))
  const d = Math.floor(total / 86400)
  const h = Math.floor((total % 86400) / 3600)
  const m = Math.floor((total % 3600) / 60)
  const sec = total % 60
  const parts: [string, string][] = [
    ...(d > 0 ? [[String(d), t.ch.daysShort] as [string, string]] : []),
    [String(h).padStart(2, '0'), t.ch.hoursShort], [String(m).padStart(2, '0'), t.ch.minShort], [String(sec).padStart(2, '0'), t.ch.secShort],
  ]
  return (
    <div className="ch-count">
      {parts.map(([v, u], i) => (
        <span key={u + i} className="ch-count-cell">
          <b key={v}>{v}</b>
          <small>{u}</small>
        </span>
      ))}
    </div>
  )
}

/** Своя карточка: место, билеты (докручиваются и вспыхивают при прибавке), серия до бонуса. */
function MyCard({ me, leader, t }: { me: ChallengeRow; leader: number; t: Translations }) {
  const s = t.ch
  const prev = useRef(me.tickets)
  const [bump, setBump] = useState(false)
  useEffect(() => {
    if (me.tickets > prev.current) {
      setBump(true)
      hapticNotify('success')
      const id = window.setTimeout(() => setBump(false), 900)
      prev.current = me.tickets
      return () => window.clearTimeout(id)
    }
    prev.current = me.tickets
  }, [me.tickets])
  const shown = useCountUp(me.tickets, true, 900)
  const inStreak = me.streak % 3
  const toBonus = 3 - inStreak

  return (
    <section className={`card ch-me ${bump ? 'ch-bump' : ''}`}>
      <div className="ch-me-rank">
        <span className="ch-me-place">#{me.rank}</span>
        <span className="muted small">{s.place}</span>
      </div>
      <div className="ch-me-main">
        <div className="ch-me-tickets">
          <span className="ch-ticket-ic mx-ch-ticket"><Icon name="ticket" size={26} /></span>
          <b>{Math.round(shown)}</b>
          <span className="muted small">{s.tickets}</span>
        </div>
        <span className="muted small">{s.you} · {s.wl.replace('{w}', String(me.wins)).replace('{l}', String(me.losses))}</span>
        <div className="ch-streak">
          {[0, 1, 2].map(i => <i key={i} className={`ch-streak-dot ${i < inStreak ? 'ch-streak-on' : ''}`} />)}
          <span className="small">{me.streak > 0 ? s.streak.replace('{n}', String(me.streak)) : ''}</span>
          <span className="muted small">{s.nextBonus.replace('{n}', String(toBonus))}</span>
        </div>
        {leader > 0 && (
          <span className="ch-me-bar"><span style={{ width: `${Math.min(100, (me.tickets / leader) * 100)}%` }} /></span>
        )}
      </div>
    </section>
  )
}

/**
 * Таблица, которая «живёт»: строки плавно переезжают на новые места (FLIP), а
 * у кого прибавились билеты — вспыхивает.
 */
function Board({ rows, me }: { rows: ChallengeRow[]; me: ChallengeRow | null }) {
  const openPlayer = usePlayerSheet()
  const leader = Math.max(1, rows[0]?.tickets ?? 1)
  const refs = useRef(new Map<string, HTMLDivElement>())
  const lastTop = useRef(new Map<string, number>())
  const lastTickets = useRef(new Map<string, number>())
  const [flash, setFlash] = useState<Set<string>>(new Set())

  useLayoutEffect(() => {
    const grew = new Set<string>()
    refs.current.forEach((el, tag) => {
      const top = el.getBoundingClientRect().top
      const before = lastTop.current.get(tag)
      if (!REDUCED && before !== undefined && Math.abs(before - top) > 1) {
        el.style.transition = 'none'
        el.style.transform = `translateY(${before - top}px)`
        requestAnimationFrame(() => {
          el.style.transition = 'transform 0.6s cubic-bezier(0.22, 1, 0.36, 1)'
          el.style.transform = ''
        })
      }
      lastTop.current.set(tag, top)
    })
    for (const r of rows) {
      const was = lastTickets.current.get(r.tag)
      if (was !== undefined && r.tickets > was) grew.add(r.tag)
      lastTickets.current.set(r.tag, r.tickets)
    }
    if (grew.size) {
      setFlash(grew)
      const id = window.setTimeout(() => setFlash(new Set()), 1200)
      return () => window.clearTimeout(id)
    }
  }, [rows])

  const showMeBelow = me && !rows.some(r => r.isMe)

  return (
    <div className="ch-rows">
      {rows.map(r => (
        <div key={r.tag} ref={el => { if (el) refs.current.set(r.tag, el); else refs.current.delete(r.tag) }}
          role="button" tabIndex={0} onClick={() => openPlayer(r.tag)}
          className={`ch-row ch-row-click ${r.isMe ? 'ch-row-me' : ''} ${r.rank <= 3 ? `ch-top ch-top-${r.rank}` : ''} ${flash.has(r.tag) ? 'ch-flash' : ''}`}>
          <span className="ch-rank">{r.rank <= 3 ? <span className={`mx-medal mx-medal-${r.rank}`}>{r.rank}</span> : r.rank}</span>
          <span className="ch-name">
            <b>{r.name}</b>
            <span className="ch-bar"><span style={{ width: `${(r.tickets / leader) * 100}%` }} /></span>
          </span>
          <span className="ch-sub muted small">{r.wins}–{r.losses}{r.streak >= 2 && <> · <Icon name="flame" size={12} className="mx-flame" />{r.streak}</>}</span>
          <span className="ch-tk">{r.tickets}<small><Icon name="ticket" size={13} /></small></span>
        </div>
      ))}
      {showMeBelow && me && (
        <>
          <div className="ch-gap">⋯</div>
          <div className="ch-row ch-row-me ch-row-click" role="button" tabIndex={0} onClick={() => openPlayer(me.tag)}>
            <span className="ch-rank">{me.rank}</span>
            <span className="ch-name"><b>{me.name}</b></span>
            <span className="ch-sub muted small">{me.wins}–{me.losses}</span>
            <span className="ch-tk">{me.tickets}<small><Icon name="ticket" size={13} /></small></span>
          </div>
        </>
      )}
    </div>
  )
}
