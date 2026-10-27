import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ClanDiscipline, ClanHq, DisciplinePlayer, HqPlayer, ScoutPlus } from '../types'
import { haptic } from '../lib/telegram'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { useT, type Translations } from '../lib/i18n'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'
import { ScoutPlusCard } from './ScoutPlusCard'
import { DisciplineCard } from './DisciplineCard'
import { AiBox } from './AiBox'
import { MyWarDecksCard, WarPrepPanel } from './WarPrep'

type Load<T> =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'locked' }
  | { kind: 'leaderOnly' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: T }

const fill = (s: string, vars: Record<string, string | number>) =>
  Object.entries(vars).reduce((acc, [k, v]) => acc.split(`{${k}}`).join(String(v)), s)

function toLoad<T>(e: unknown): Load<T> {
  if (e instanceof ApiError && e.code === 'not_pro') return { kind: 'locked' }
  if (e instanceof ApiError && e.code === 'not_leader') return { kind: 'leaderOnly' }
  if (e instanceof ApiError && e.status === 404) return { kind: 'empty' }
  return { kind: 'error' }
}

/**
 * Раздел «Штаб» вкладки клана: всё про клан как команду - для тех, кто им управляет.
 * Клан Про: «Разведка+» и «Штаб клана». Без спонсора - замок с тем, что внутри, и
 * обычная дисциплина, которая была бесплатной и такой остаётся.
 */
export function ClanHqView() {
  const { t } = useT()
  const p = t.clanPro
  const [scout, setScout] = useState<Load<ScoutPlus>>({ kind: 'loading' })
  const [hq, setHq] = useState<Load<ClanHq>>({ kind: 'loading' })

  const load = useCallback(() => {
    let alive = true
    api.getScoutPlus()
      .then(data => { if (alive) setScout({ kind: 'ready', data }) })
      .catch(e => { if (alive) setScout(toLoad(e)) })
    api.getClanHq()
      .then(data => { if (alive) setHq({ kind: 'ready', data }) })
      .catch(e => { if (alive) setHq(toLoad(e)) })
    return () => { alive = false }
  }, [])

  useEffect(load, [load])

  // Купили спонсорство из замка - открываем всё сразу, без перезахода
  useEffect(() => {
    const again = () => { setScout({ kind: 'loading' }); setHq({ kind: 'loading' }); load() }
    window.addEventListener(PLUS_CHANGED, again)
    return () => window.removeEventListener(PLUS_CHANGED, again)
  }, [load])

  const locked = scout.kind === 'locked' || hq.kind === 'locked'

  if (locked) {
    return (
      <div className="fade-in">
        <ProLock p={p} />
        <DisciplineCard />
      </div>
    )
  }

  // С понедельника по среду главное в штабе - подготовка к войне, в дни войны - разведка
  const prepFirst = [1, 2, 3].includes(new Date().getDay())
  const prep = hq.kind === 'leaderOnly' ? <MyWarDecksCard /> : <WarPrepPanel />

  return (
    <div className="fade-in">
      {prepFirst && prep}
      {scout.kind === 'loading' && <LoadingCard icon="radar" title={p.scoutTitle} />}
      {scout.kind === 'error' && <NoteCard icon="radar" title={p.scoutTitle} text={p.error} />}
      {scout.kind === 'empty' && <NoteCard icon="radar" title={p.scoutTitle} text={p.noWar} />}
      {scout.kind === 'ready' && <ScoutPlusCard data={scout.data} />}

      {hq.kind === 'loading' && <LoadingCard icon="shieldCheck" title={p.hqTitle} />}
      {hq.kind === 'error' && <NoteCard icon="shieldCheck" title={p.hqTitle} text={p.error} />}
      {hq.kind === 'leaderOnly' && <NoteCard icon="lock" title={p.hqTitle} text={p.leaderOnly} />}
      {hq.kind === 'ready' && <HqCard data={hq.data} t={t} />}
      {!prepFirst && prep}
    </div>
  )
}

function LoadingCard({ icon, title }: { icon: 'radar' | 'shieldCheck'; title: string }) {
  return (
    <section className="card">
      <SectionHead icon={icon} tone="gold" title={title} />
      <div className="center" style={{ padding: 12 }}><div className="spinner" /></div>
    </section>
  )
}

function NoteCard({ icon, title, text }: { icon: 'radar' | 'shieldCheck' | 'lock'; title: string; text: string }) {
  return (
    <section className="card">
      <SectionHead icon={icon} tone="gold" title={title} />
      <p className="muted small" style={{ margin: 0 }}>{text}</p>
    </section>
  )
}

/** Замок Клан Про: что внутри и одна кнопка - в окно покупки на спонсорство. */
function ProLock({ p }: { p: Translations['clanPro'] }) {
  const openPlus = usePlusSheet()
  return (
    <section className="card pro-lock fade-in">
      <div className="pro-lock-glow" aria-hidden />
      <div className="pro-lock-head">
        <span className="pro-lock-icon"><Icon name="crown" size={22} /></span>
        <div>
          <span className="pro-badge">{p.badge}</span>
          <h3>{p.scoutTitle} · {p.hqTitle}</h3>
        </div>
      </div>
      <p className="small pro-lock-text">{p.lockedText}</p>
      <ul className="pro-lock-perks">
        {p.lockedPerks.split('|').map(x => (
          <li key={x}><Icon name="checkCircle" size={14} /> {x}</li>
        ))}
      </ul>
      {/* Размытый пример - чтобы было видно, что за замком настоящие данные, а не обещание */}
      <div className="pro-lock-sample" aria-hidden>
        {[92, 74, 58, 81, 40, 66, 98, 70, 36, 52, 88, 61].map((h, i) => (
          <span key={i} style={{ height: `${h}%` }} className={i === 6 || i === 7 ? 'peak' : ''} />
        ))}
      </div>
      <button className="btn pro-lock-btn" onClick={() => { haptic('medium'); openPlus('sponsor') }}>
        <Icon name="crown" size={16} /> {p.lockedBtn}
      </button>
    </section>
  )
}

function HqCard({ data, t }: { data: ClanHq; t: Translations }) {
  const p = t.clanPro
  const { lang } = useT()
  return (
    <section className="card hq-card fade-in">
      <SectionHead icon="shieldCheck" tone="gold" title={p.hqTitle}
        aside={data.weeksAnalyzed > 0 ? fill(p.weeks, { n: data.weeksAnalyzed }) : undefined} />
      <p className="muted small sp-lead">{p.hqLead}</p>
      {data.weeksAnalyzed >= 2 && <AiBox label={t.ai.hqBtn} hint={t.ai.hqHint} load={() => api.getHqBrief(lang)} />}

      {data.weeksAnalyzed < 2 && data.attention.length === 0 && data.pillars.length === 0
        ? <p className="muted small hq-empty">{p.noData}</p>
        : (
          <>
            <h4 className="sp-sub hq-sub-bad"><Icon name="alert" size={14} /> {p.attention}
              {data.clanFamePerDeck > 0 && <span className="muted small hq-median">{fill(p.clanFpd, { n: data.clanFamePerDeck })}</span>}
            </h4>
            {data.attention.length === 0
              ? <p className="muted small hq-empty hq-empty-good"><Icon name="checkCircle" size={14} /> {p.attentionEmpty}</p>
              : <ul className="hq-list">{data.attention.map(pl => <AttentionRow key={pl.tag} player={pl} median={data.clanFamePerDeck} t={t} />)}</ul>}

            {data.pillars.length > 0 && (
              <>
                <h4 className="sp-sub hq-sub-good"><Icon name="shield" size={14} /> {p.pillars}</h4>
                <ul className="hq-pillars">
                  {data.pillars.map(pl => (
                    <li key={pl.tag}>
                      <span className="hq-pillar-name">{pl.name}</span>
                      <span className="hq-pillar-val">{pl.famePerDeck} <span className="muted small">{p.fpd}</span></span>
                    </li>
                  ))}
                </ul>
              </>
            )}
            {data.excluded > 0 && <p className="muted small hq-excluded"><Icon name="info" size={12} /> {fill(p.excluded, { n: data.excluded })}</p>}
          </>
        )}

      <DisciplineBoard data={data.discipline} t={t} />
    </section>
  )
}

function AttentionRow({ player, median, t }: { player: HqPlayer; median: number; t: Translations }) {
  const p = t.clanPro
  const { lang } = useT()
  // Шкала «медалей за колоду» от 100 (одни поражения) до 200+ (одни победы), с меткой медианы клана
  const pos = (v: number) => Math.min(100, Math.max(0, ((v - 100) / 125) * 100))
  const role = player.role === 'leader' ? t.roles.leader : player.role === 'coLeader' ? t.roles.coleader : player.role === 'elder' ? t.roles.elder : undefined
  return (
    <li className="hq-row">
      <div className="hq-row-top">
        <span className="hq-avatar">{player.name.slice(0, 1).toUpperCase()}</span>
        <span className="hq-name">{player.name}{role && <span className="muted small"> · {role}</span>}</span>
        <span className="hq-fpd">{player.famePerDeck} <span className="muted small">{p.fpd}</span></span>
      </div>
      <div className="hq-scale" aria-hidden>
        <span className="hq-scale-fill" style={{ width: `${pos(player.famePerDeck)}%` }} />
        {median > 0 && <span className="hq-scale-median" style={{ left: `${pos(median)}%` }} />}
      </div>
      <div className="hq-reasons">
        {player.reasons.map(r => <span key={r} className={`hq-reason hq-reason-${r}`}>{p.reasons[r]}</span>)}
      </div>
      <div className="hq-meta muted small">
        <span>{fill(p.decksOf, { d: player.decks, p: player.decksPossible })}</span>
        {player.winRate != null && <span>{fill(p.winRate, { n: player.winRate })}</span>}
        {player.lastWeekFamePerDeck > 0 && <span>{fill(p.lastWeek, { n: player.lastWeekFamePerDeck })}</span>}
      </div>
      <AiBox compact copy label={t.ai.talkBtn} load={() => api.getHqTalk(player.tag, lang)} />
    </li>
  )
}

type DiscSection = 'skippers' | 'nudged' | 'late'

/** Дисциплина в штабе: те же три списка, что и раньше, но с полосами - кто дальше всех от нормы. */
function DisciplineBoard({ data, t }: { data: ClanDiscipline; t: Translations }) {
  const d = t.discipline
  const [section, setSection] = useState<DiscSection>('skippers')
  const rows = section === 'skippers' ? data.skippers : section === 'nudged' ? data.nudged : data.lastMinute
  const value = (r: DisciplinePlayer) => section === 'skippers' ? r.missedDecks : section === 'nudged' ? r.nudgeCount : r.lastMinuteBattles
  const max = Math.max(1, ...rows.map(value))
  const unit = section === 'skippers' ? d.missedDecks : section === 'nudged' ? d.nudges : d.lateBattles
  const empty = data.weeksAnalyzed === 0 && section === 'skippers' ? d.noData
    : section === 'skippers' ? d.emptySkippers : section === 'nudged' ? d.emptyNudged : d.emptyLate

  const tabs: { id: DiscSection; label: string; icon: 'xCircle' | 'bell' | 'hourglass' }[] = [
    { id: 'skippers', label: d.tabSkippers, icon: 'xCircle' },
    { id: 'nudged', label: d.tabNudged, icon: 'bell' },
    { id: 'late', label: d.tabLate, icon: 'hourglass' },
  ]

  return (
    <div className="hq-disc">
      <h4 className="sp-sub"><Icon name="shieldCheck" size={14} /> {d.title}</h4>
      <div className="hq-disc-tabs">
        {tabs.map(x => (
          <button key={x.id} className={`hq-disc-tab ${section === x.id ? 'on' : ''}`}
            onClick={() => { haptic('light'); setSection(x.id) }}>
            <Icon name={x.icon} size={13} /> {x.label}
          </button>
        ))}
      </div>
      {rows.length === 0
        ? <p className="muted small hq-empty">{empty}</p>
        : (
          <ul className="hq-disc-list" key={section}>
            {rows.map(r => (
              <li key={r.playerTag} className="hq-disc-row fade-in">
                <span className="hq-disc-name">{r.name}</span>
                <span className="hq-disc-bar"><span style={{ width: `${(value(r) / max) * 100}%` }} className={`hq-disc-fill-${section}`} /></span>
                <span className="hq-disc-val"><b>{value(r)}</b> <span className="muted small">{unit}</span></span>
              </li>
            ))}
          </ul>
        )}
    </div>
  )
}
