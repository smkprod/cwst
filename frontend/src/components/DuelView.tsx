import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { DuelLeague, DuelLeagueKey, DuelProfileView, DuelRow, DuelTopRow } from '../types'
import { haptic, hapticNotify, switchInline, openTelegramLink, botStartLink, tg } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { useCountUp } from '../lib/anim'
import { usePlayerSheet } from '../lib/playerSheet'

/** Пока идёт дуэль — опрашиваем чаще: счёт меняется прямо во время игры. */
const ACTIVE_POLL = 15_000
const IDLE_POLL = 60_000

export const LEAGUE_ICONS: Record<DuelLeagueKey, string> = {
  bronze: '🥉', silver: '🥈', gold: '🥇', diamond: '💎', master: '👑', legend: '🔥',
}

/**
 * «⚔️ Лига дуэлей»: свой рейтинг и лига, вызов в любой чат, топ и история.
 *
 * Вызов уходит карточкой через инлайн-режим бота, а счёт бот берёт из журнала боёв
 * сам. Отсюда - только смотреть и звать: сама дуэль живёт в чате, где её приняли.
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
          : <section className="card"><p className="muted small" style={{ margin: 0 }}>{s.notLinked}</p></section>
      )}

      {data.active && <ActiveCard d={data.active} t={t} />}

      <section className="card dl-how">
        <div className="card-title">{s.howTitle}</div>
        <ol>
          {s.how.map((line, i) => <li key={i}>{line}</li>)}
        </ol>
      </section>

      <section className="card dl-top">
        <div className="dl-head">
          <span className="card-title" style={{ margin: 0 }}>🏆 {s.topTitle}</span>
          <span className="muted small">{s.players.replace('{n}', String(data.players))}</span>
        </div>
        {data.top.length === 0
          ? <p className="muted small" style={{ margin: '8px 0 0' }}>{s.emptyTop}</p>
          : <Top rows={data.top} t={t} />}
      </section>

      {data.me && (
        <section className="card">
          <div className="card-title">{s.myTitle}</div>
          {data.mine.length === 0
            ? <p className="muted small" style={{ margin: 0 }}>{s.noDuels}</p>
            : <DuelList rows={data.mine} myTag={data.me.tag} t={t} />}
        </section>
      )}

      {data.recent.length > 0 && (
        <section className="card">
          <div className="card-title">{s.recentTitle}</div>
          <DuelList rows={data.recent} myTag={data.me?.tag ?? null} t={t} />
        </section>
      )}

      {data.me && (
        <details className="card dl-change">
          <summary>{s.changeLink}</summary>
          <LinkForm t={t} mode="change" onSaved={setData} />
        </details>
      )}

      <p className="muted small ch-foot">{s.unofficial}</p>
    </div>
  )
}

/** Шапка участника: лига, рейтинг (докручивается), прогресс до следующей лиги, статистика. */
function Hero({ me, t, onChallenge }: { me: DuelProfileView; t: Translations; onChallenge: () => void }) {
  const s = t.duel
  const rating = useCountUp(me.rating, true, 900)
  const next = me.nextFloor
  const pct = next === null ? 100 : Math.max(4, Math.min(100, Math.round((me.rating - me.floor) / (next - me.floor) * 100)))
  const nextKey = next === null ? null : (['bronze', 'silver', 'gold', 'diamond', 'master', 'legend'] as DuelLeagueKey[])[me.leagueIndex + 1]
  const winrate = me.games > 0 ? Math.round(me.wins / me.games * 100) : null

  return (
    <section className={`dl-hero dl-l-${me.league}`}>
      <div className="dl-hero-glow" />
      <div className="dl-hero-top">
        <span className="dl-badge"><span className="dl-badge-icon">{LEAGUE_ICONS[me.league]}</span>{s.leagues[me.league]}</span>
        <span className="dl-rank">#{me.rank}</span>
      </div>
      <div className="dl-name">{me.name}</div>
      <div className="dl-rating">
        <b>{Math.round(rating)}</b>
        <span>{s.rating}</span>
      </div>
      <div className="dl-bar"><i style={{ width: `${pct}%` }} /></div>
      <div className="dl-next">
        {next === null || !nextKey
          ? s.maxLeague
          : s.toNext.replace('{league}', `${LEAGUE_ICONS[nextKey]} ${s.leagues[nextKey]}`).replace('{n}', String(next - me.rating))}
      </div>
      <div className="dl-stats">
        <span><b>{me.wins}</b>{s.wins}</span>
        <span><b>{me.losses}</b>{s.losses}</span>
        <span><b>{winrate === null ? '—' : `${winrate}%`}</b>{s.winrate}</span>
        <span><b>{me.peak}</b>{s.peak}</span>
      </div>
      <button className="dl-cta" onClick={onChallenge}>
        <span className="ch-join-shine" />
        {s.challenge}
      </button>
      <span className="dl-cta-hint">{s.challengeHint}</span>
    </section>
  )
}

/** Шапка для тех, кто ещё не в лиге: что это и зачем. */
function Pitch({ t }: { t: Translations }) {
  const s = t.duel
  return (
    <section className="dl-hero dl-l-silver">
      <div className="dl-hero-glow" />
      <div className="dl-ladder">
        {(['bronze', 'silver', 'gold', 'diamond', 'master', 'legend'] as DuelLeagueKey[]).map((k, i) => (
          <span key={k} style={{ animationDelay: `${i * 90}ms` }}>{LEAGUE_ICONS[k]}</span>
        ))}
      </div>
      <h2 className="dl-title">⚔️ {s.title}</h2>
      <p className="dl-pitch">{s.pitch}</p>
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

  return (
    <section className={mode === 'join' ? 'card dl-join' : 'dl-join dl-join-inline'}>
      {mode === 'join' && (
        <>
          <div className="card-title">{s.joinTitle}</div>
          <p className="muted small" style={{ margin: 0 }}>{s.joinText}</p>
          <p className="dl-path">{s.joinPath}</p>
        </>
      )}
      <div className="dl-input-row">
        <input
          className="dl-input"
          inputMode="url"
          autoComplete="off"
          placeholder={s.placeholder}
          value={value}
          onChange={e => setValue(e.target.value)}
        />
        <button className="btn-mini" onClick={paste}>{s.paste}</button>
      </div>
      <button className={mode === 'join' ? 'ch-join-btn' : 'btn-mini'} disabled={saving || !value.trim()} onClick={save}>
        {mode === 'join' && <span className="ch-join-shine" />}
        {saving ? s.saving : mode === 'join' ? s.joinBtn : s.saveLink}
      </button>
      {msg && <p className={`small ${msg.ok ? 'dl-ok' : 'dl-err'}`} style={{ margin: 0 }}>{msg.text}</p>}
    </section>
  )
}

/** Идущая дуэль: крупный счёт и живая точка. */
function ActiveCard({ d, t }: { d: DuelRow; t: Translations }) {
  const s = t.duel
  return (
    <section className="card dl-active">
      <div className="dl-head">
        <span className="card-title" style={{ margin: 0 }}><i className="ch-live-dot" /> {s.activeTitle}</span>
        <span className="dl-bo">Bo{d.bestOf}</span>
      </div>
      <div className="dl-vs">
        <span className="dl-vs-name">{d.aName}</span>
        <span className="dl-vs-score"><b>{d.scoreA}</b>:<b>{d.scoreB}</b></span>
        <span className="dl-vs-name dl-right">{d.bName}</span>
      </div>
      <p className="muted small" style={{ margin: 0 }}>{s.activeHint}</p>
    </section>
  )
}

function Top({ rows, t }: { rows: DuelTopRow[]; t: Translations }) {
  const s = t.duel
  const openPlayer = usePlayerSheet()
  const medals = ['🥇', '🥈', '🥉']
  return (
    <ol className="dl-board">
      {rows.map(r => (
        <li key={r.tag} className={r.me ? 'dl-me' : ''} onClick={() => { haptic('light'); openPlayer(r.tag) }}>
          <span className="dl-pos">{r.rank <= 3 ? medals[r.rank - 1] : r.rank}</span>
          <span className="dl-who">
            <b>{r.me ? `${r.name} · ${s.you}` : r.name}</b>
            <small>{LEAGUE_ICONS[r.league]} {s.leagues[r.league]} · {r.wins}–{r.losses}</small>
          </span>
          <span className="dl-pts">{r.rating}</span>
        </li>
      ))}
    </ol>
  )
}

function DuelList({ rows, myTag, t }: { rows: DuelRow[]; myTag: string | null; t: Translations }) {
  const s = t.duel
  return (
    <ul className="dl-list">
      {rows.map(d => {
        const aWon = d.scoreA > d.scoreB
        const mine = myTag === d.aTag ? d.deltaA : myTag === d.bTag ? d.deltaB : null
        const note = d.state === 'expired' ? s.expired : d.state === 'cancelled' ? s.cancelled : !d.rated ? s.unrated : null
        return (
          <li key={d.id}>
            <span className={`dl-side ${d.state === 'finished' && aWon ? 'dl-win' : ''}`}>{d.aName}</span>
            <span className="dl-score">{d.scoreA}:{d.scoreB}</span>
            <span className={`dl-side dl-right ${d.state === 'finished' && !aWon ? 'dl-win' : ''}`}>{d.bName}</span>
            <span className="dl-meta">
              Bo{d.bestOf}
              {note ? ` · ${note}` : ''}
              {mine !== null && d.state === 'finished' && d.rated && (
                <b className={mine >= 0 ? 'dl-up' : 'dl-down'}>{mine >= 0 ? `+${mine}` : `−${-mine}`}</b>
              )}
            </span>
          </li>
        )
      })}
    </ul>
  )
}
