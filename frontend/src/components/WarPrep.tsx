import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'
import { usePlusSheet } from '../lib/plusSheet'
import { roleLabel, useT, type Translations } from '../lib/i18n'
import type { BuilderDeck, WarPrepMember, WarPrepRow, WarPrepStatus, WarPrepSummary } from '../types'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

/**
 * «Военный штаб» (Клан Про): готовность клана к КВ. Главе - сводка и список, у
 * каждого игрока его лучшие 4 колоды из своих карт против того, чем он играл на
 * войне. Игроку - его колоды на КВ с кнопками «в игру».
 */

const fill = (s: string, vars: Record<string, string | number>) =>
  Object.entries(vars).reduce((acc, [k, v]) => acc.split(`{${k}}`).join(String(v)), s)

const lvl = (n: number) => n.toFixed(1)

type Filter = 'all' | WarPrepStatus

function reasonText(t: Translations, r: WarPrepRow): string | null {
  const s = t.warPrep.reasons
  switch (r.reason) {
    case 'played_lower': return fill(s.played_lower, { a: lvl(r.playedLevel), b: lvl(r.bestLevel) })
    case 'losing': return fill(s.losing, { w: r.warWins, g: r.warGames })
    case 'no_cards': return s.no_cards
    case 'low_levels': return s.low_levels
    case 'no_war_games': return s.no_war_games
    case 'no_profile': return s.no_profile
    default: return null
  }
}

/** Панель главы в «Штабе»: сводка готовности, слабые карты, список и рассылка. */
export function WarPrepPanel() {
  const { t } = useT()
  const s = t.warPrep
  const [data, setData] = useState<WarPrepSummary | null | 'loading' | 'error' | 'nometa' | 'hidden'>('loading')
  const [filter, setFilter] = useState<Filter>('all')
  const [open, setOpen] = useState<string | null>(null)
  const [send, setSend] = useState<'idle' | 'confirm' | 'busy' | { sent: number; skipped: number }>('idle')

  const load = useCallback((fresh: boolean) => {
    setData('loading')
    api.getWarPrep(fresh)
      .then(d => setData(d ?? 'nometa'))
      // Не глава и не Клан Про - у него своя карточка, панели нет
      .catch(e => setData(e instanceof ApiError && (e.code === 'not_leader' || e.code === 'not_pro') ? 'hidden' : 'error'))
  }, [])
  useEffect(() => load(false), [load])

  if (data === 'hidden') return null
  if (data === 'loading') {
    return (
      <section className="card wp">
        <SectionHead icon="shieldCheck" tone="gold" title={s.title} />
        <div className="wp-loading"><div className="spinner" /><span className="muted small">{s.loading}</span></div>
      </section>
    )
  }
  if (data === 'error' || data === 'nometa' || data === null) {
    return (
      <section className="card wp">
        <SectionHead icon="shieldCheck" tone="gold" title={s.title} />
        <p className="muted small">{data === 'nometa' ? s.noMeta : s.error}</p>
        <button className="btn-mini" onClick={() => load(true)}><Icon name="refresh" size={14} /> {s.refresh}</button>
      </section>
    )
  }

  const known = Math.max(1, data.total - data.unknown)
  const pct = Math.round((100 * data.ready) / known)
  const rows = data.members.filter(m => filter === 'all' || m.status === filter)

  const doSend = async () => {
    if (send === 'idle') { haptic('light'); setSend('confirm'); return }
    if (send !== 'confirm') return
    haptic('medium')
    setSend('busy')
    try {
      const r = await api.sendWarPrep()
      hapticNotify('success')
      setSend(r)
    } catch {
      hapticNotify('error')
      setSend('idle')
    }
  }

  return (
    <section className="card wp">
      <div className="wp-head">
        <SectionHead icon="shieldCheck" tone="gold" title={s.title} />
        <button className="wp-refresh" aria-label={s.refresh} onClick={() => { haptic('light'); load(true) }}><Icon name="refresh" size={15} /></button>
      </div>
      <p className="muted small wp-lead">{s.lead}</p>

      <div className="wp-gauge">
        <div className="wp-gauge-num"><b>{pct}%</b><span>{s.percentLabel}</span></div>
        <div className="wp-bar">
          <i className="wp-ready" style={{ width: `${(100 * data.ready) / known}%` }} />
          <i className="wp-better" style={{ width: `${(100 * data.better) / known}%` }} />
          <i className="wp-weak" style={{ width: `${(100 * data.weak) / known}%` }} />
        </div>
        <div className="wp-chips">
          {(['all', 'weak', 'better', 'ready'] as Filter[]).map(f => {
            const n = f === 'all' ? data.total : f === 'weak' ? data.weak : f === 'better' ? data.better : data.ready
            const label = f === 'all' ? s.all : f === 'weak' ? s.weak : f === 'better' ? s.better : s.ready
            return (
              <button key={f} className={`wp-chip wp-chip-${f} ${filter === f ? 'on' : ''}`} onClick={() => { haptic('light'); setFilter(f) }}>
                {label} <b>{n}</b>
              </button>
            )
          })}
        </div>
      </div>

      {data.weakCards.length > 0 && (
        <div className="wp-weakcards">
          <span className="small">{s.weakCards}</span>
          <div>
            {data.weakCards.map(w => (
              <span key={w.name} className="wp-wcard" title={w.name}>
                {w.iconUrl ? <img src={w.iconUrl} alt={w.name} loading="lazy" /> : null}
                <i>{fill(s.players, { n: w.players })}</i>
              </span>
            ))}
          </div>
        </div>
      )}

      <ul className="wp-list">
        {rows.map(m => (
          <li key={m.tag} className={`wp-row wp-${m.status} ${open === m.tag ? 'open' : ''}`}>
            <button className="wp-row-main" onClick={() => { haptic('light'); setOpen(open === m.tag ? null : m.tag) }}>
              <span className={`wp-dot wp-dot-${m.status}`} />
              <span className="wp-row-text">
                <span className="wp-row-name">
                  {m.name}
                  {roleLabel(m.role, t) && <em>{roleLabel(m.role, t)}</em>}
                </span>
                <span className="wp-row-why">
                  {reasonText(t, m) ?? (m.status === 'ready' ? `${lvl(m.bestLevel)} ${s.levelShort}` : '')}
                  {m.missedDecks > 0 && <span className="wp-miss"> · ⚠️ {fill(s.missed, { n: m.missedDecks })}</span>}
                </span>
              </span>
              <span className="wp-row-lvl">{m.bestLevel > 0 ? lvl(m.bestLevel) : '—'}</span>
              <Icon name={open === m.tag ? 'chevronUp' : 'chevronDown'} size={16} />
            </button>
            {open === m.tag && <MemberDetail tag={m.tag} />}
          </li>
        ))}
      </ul>

      <div className="wp-send">
        {typeof send === 'object'
          ? <p className="small wp-sent"><Icon name="checkCircle" size={14} /> {fill(s.sent, { n: send.sent, m: send.skipped })}</p>
          : (
            <>
              <button className={`btn wp-send-btn ${send === 'confirm' ? 'confirm' : ''}`} disabled={send === 'busy'} onClick={doSend}>
                <Icon name="send" size={16} /> {send === 'busy' ? s.sending : send === 'confirm' ? s.sendConfirm : s.send}
              </button>
              <p className="muted small">{s.sendHint}</p>
            </>
          )}
      </div>
    </section>
  )
}

/** Колоды одного игрока - грузятся по нажатию на строку. */
function MemberDetail({ tag }: { tag: string }) {
  const { t } = useT()
  const s = t.warPrep
  const [m, setM] = useState<WarPrepMember | null | 'error'>(null)
  useEffect(() => {
    let alive = true
    api.getWarPrepMember(tag).then(d => { if (alive) setM(d) }).catch(() => { if (alive) setM('error') })
    return () => { alive = false }
  }, [tag])

  if (m === null) return <div className="wp-detail"><div className="spinner" /></div>
  if (m === 'error') return <div className="wp-detail"><p className="muted small">{s.error}</p></div>
  return (
    <div className="wp-detail fade-in">
      <DeckSet title={s.best} decks={m.best} t={t} />
      {m.played.length > 0 && (
        <>
          <p className="wp-sub">{s.played}</p>
          {m.played.map(p => (
            <DeckLine key={p.deck.key} deck={p.deck} t={t}
              note={fill(s.playedWins, { w: p.wins, g: p.games })} />
          ))}
        </>
      )}
      {m.upgrade.length > 0 && <Upgrade items={m.upgrade} t={t} />}
    </div>
  )
}

function DeckSet({ title, decks, t }: { title: string; decks: BuilderDeck[]; t: Translations }) {
  return (
    <>
      <p className="wp-sub">{title}</p>
      {decks.map(d => (
        <DeckLine key={d.key} deck={d} t={t} copy note={d.games > 0 ? t.warPrep.metaDeck : t.warPrep.ownDeck} />
      ))}
    </>
  )
}

/** Колода строкой: восемь карт с уровнями, средний уровень и кнопка «в игру». */
function DeckLine({ deck, note, copy = false, t }: { deck: BuilderDeck; note?: string; copy?: boolean; t: Translations }) {
  return (
    <div className="wp-deck">
      <div className="wp-deck-cards">
        {deck.cards.map((c, i) => (
          <span key={`${c.cardId}-${i}`} className={`wp-card ${c.evo ? 'evo' : ''}`}>
            {c.iconUrl ? <img src={c.iconUrl} alt={c.name} loading="lazy" onError={e => { if (c.baseIconUrl) (e.target as HTMLImageElement).src = c.baseIconUrl }} /> : null}
            {c.level > 0 && <i>{c.level}</i>}
          </span>
        ))}
      </div>
      <div className="wp-deck-foot">
        <span className="small"><b>{lvl(deck.avgLevel)}</b> {t.warPrep.levelShort}{note ? ` · ${note}` : ''}</span>
        {copy && deck.copyLink && (
          <a className="wp-copy" href={deck.copyLink} target="_blank" rel="noreferrer" onClick={() => haptic('medium')}>
            <Icon name="copy" size={13} /> {t.warPrep.copy}
          </a>
        )}
      </div>
    </div>
  )
}

function Upgrade({ items, t }: { items: WarPrepMember['upgrade']; t: Translations }) {
  return (
    <div className="wp-upgrade">
      <span className="small">{t.warPrep.upgrade}:</span>
      {items.map(u => (
        <span key={u.name} className="wp-up">
          {u.iconUrl ? <img src={u.iconUrl} alt={u.name} loading="lazy" /> : null}
          <i>{u.level}</i>
        </span>
      ))}
    </div>
  )
}

/**
 * «Твои колоды на КВ» - любому игроку клана с Клан Про. Без Клан Про - замок с тем,
 * что это даёт, и кнопкой в витрину: рядовой игрок тоже может попросить главу.
 */
export function MyWarDecksCard() {
  const { t } = useT()
  const s = t.warPrep
  const openPlus = usePlusSheet()
  const [m, setM] = useState<WarPrepMember | null | 'loading' | 'locked' | 'hidden'>('loading')

  useEffect(() => {
    let alive = true
    api.getMyWarPrep()
      .then(d => { if (alive) setM(d ?? 'hidden') })
      .catch(e => { if (alive) setM(e instanceof ApiError && e.code === 'not_pro' ? 'locked' : 'hidden') })
    return () => { alive = false }
  }, [])

  if (m === 'hidden' || m === null) return null
  if (m === 'loading') {
    return (
      <section className="card wp wp-my">
        <SectionHead icon="shieldCheck" tone="gold" title={s.myTitle} />
        <div className="wp-loading"><div className="spinner" /></div>
      </section>
    )
  }
  if (m === 'locked') {
    return (
      <section className="card wp wp-my wp-locked">
        <SectionHead icon="shieldCheck" tone="gold" title={s.lockedTitle} />
        <p className="small">{s.lockedText}</p>
        <button className="btn wp-pro-btn" onClick={() => { haptic('medium'); openPlus('sponsor') }}>
          <Icon name="crown" size={16} /> {s.lockedBtn}
        </button>
      </section>
    )
  }

  const lead = m.status === 'better' && m.reason === 'played_lower'
    ? fill(s.myBetter, { a: lvl(m.playedLevel), b: lvl(m.bestLevel) })
    : m.status === 'better' ? s.myLosing
    : m.status === 'weak' ? s.myWeak
    : s.myReady

  return (
    <section className={`card wp wp-my wp-my-${m.status}`}>
      <SectionHead icon="shieldCheck" tone="gold" title={s.myTitle} />
      <p className="small wp-my-lead">{lead}</p>
      {m.best.map(d => (
        <DeckLine key={d.key} deck={d} t={t} copy note={d.games > 0 ? s.metaDeck : s.ownDeck} />
      ))}
      {m.upgrade.length > 0 && <Upgrade items={m.upgrade} t={t} />}
    </section>
  )
}
