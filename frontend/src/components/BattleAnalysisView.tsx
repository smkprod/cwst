import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { BattleAnalysis, MyDeck, PlusStatus, ReviewLocked, TimeSlot } from '../types'
import { haptic, hapticNotify, requestWriteAccess } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { CardIcon } from './MetaDecksView'

/** Меньше боёв в срезе — процент ничего не значит, и сравнивать его не с чем. */
const MIN_SLOT_GAMES = 5

type PartKey = 'morning' | 'day' | 'evening' | 'night'

/**
 * Личный разбор боёв: главное сверху, подробности ниже.
 *
 * Выводы собираются здесь, а не на сервере: сервер отдаёт цифры, а формулировки
 * живут рядом с переводами. Выводов не больше четырёх — длинный список советов
 * не читают, а один верный запоминают.
 */
export function BattleAnalysisView() {
  const { t } = useT()
  const [data, setData] = useState<BattleAnalysis | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error' | 'unlinked'>('loading')
  const openPlus = usePlusSheet()

  const load = useCallback(() => {
    let alive = true
    api.getMyBattles()
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(e => {
        if (!alive) return
        setState(e instanceof ApiError && e.code === 'player_not_linked' ? 'unlinked' : 'error')
      })
    return () => { alive = false }
  }, [])

  useEffect(load, [load])

  // Купил Плюс в окне поверх разбора — перечитываем разбор, чтобы замки пропали сразу
  useEffect(() => {
    const reload = () => { load() }
    window.addEventListener(PLUS_CHANGED, reload)
    return () => window.removeEventListener(PLUS_CHANGED, reload)
  }, [load])

  const b = t.battles
  if (state === 'loading') return <div className="center" style={{ marginTop: 16 }}><div className="spinner" /></div>
  if (state === 'unlinked') return <p className="center muted" style={{ marginTop: 16 }}>{b.notLinked}</p>
  if (state === 'error' || !data) return <p className="center muted" style={{ marginTop: 16 }}>{b.loadError}</p>

  const insights = buildInsights(data, t)

  return (
    <div className="fade-in">
      <section className="card">
        <div className="card-title">{b.section}</div>
        <p className="muted small" style={{ margin: '0 0 10px' }}>{b.summary}</p>
        <div className="wtop-tiles">
          <div className="wtop-tile wtop-tile-accent">
            <span className="wtop-tile-value">{data.winPercent}%</span>
            <span className="wtop-tile-label">{b.winRate}</span>
          </div>
          <div className="wtop-tile">
            <span className="wtop-tile-value">{data.games}</span>
            <span className="wtop-tile-label">{b.games}</span>
          </div>
          <div className="wtop-tile">
            <span className="wtop-tile-value ba-wld">{data.wins}/{data.losses}/{data.draws}</span>
            <span className="wtop-tile-label">{b.wld}</span>
          </div>
        </div>
        {data.sinceUtc && (
          <p className="muted small" style={{ margin: '8px 0 0' }}>
            {b.since} {new Date(data.sinceUtc).toLocaleDateString()}
          </p>
        )}
        <p className="muted small" style={{ margin: '4px 0 0', opacity: 0.75 }}>{b.accumulating}</p>
        <AccessLine data={data} onOpen={openPlus} t={t} />
      </section>

      {data.games < MIN_SLOT_GAMES ? (
        <section className="card"><p className="muted small" style={{ margin: 0 }}>{b.fewGames}</p></section>
      ) : (
        <section className="card ba-insights">
          <div className="card-title">{b.insightsTitle}</div>
          {insights.length === 0
            ? <p className="muted small" style={{ margin: 0 }}>{b.noInsights}</p>
            : <ul className="ba-insight-list">{insights.map((x, i) => <li key={i}>{x}</li>)}</ul>}
        </section>
      )}

      <TiltAlertsCard unlocked={data.access.unlocked} onOpenPlus={openPlus} t={t} />
      <ElixirCard data={data} t={t} />
      <DecksCard decks={data.decks} locked={data.locked} onOpenPlus={openPlus} t={t} />
      <TimeCard data={data} onOpenPlus={openPlus} t={t} />
      <TiltCard data={data} t={t} />
      <ToughCard data={data} onOpenPlus={openPlus} t={t} />
      {data.locked && (
        <button className="btn plus-cta" onClick={openPlus}>{b.unlockBtn}</button>
      )}
    </div>
  )
}

/**
 * Строка доступа под шапкой: только что выданный триал, срок Плюса или сколько
 * боёв осталось до бесплатного триала. Без Плюса и без надежды на триал — молчим:
 * замки ниже скажут всё сами.
 */
function AccessLine({ data, onOpen, t }: { data: BattleAnalysis; onOpen: () => void; t: Translations }) {
  const b = t.battles
  const a = data.access
  if (a.trialStarted) {
    return <p className="ba-access ba-access-gift">{b.trialStarted.replace('{days}', String(a.trialDays))}</p>
  }
  if (a.paywall && a.active && a.until) {
    return (
      <button className="ba-access ba-access-plus" onClick={onOpen}>
        {b.plusActive.replace('{date}', new Date(a.until).toLocaleDateString())}
      </button>
    )
  }
  if (!a.unlocked && !a.trialUsed && a.trialDays > 0 && data.games < a.trialMinBattles) {
    return (
      <p className="ba-access">
        🎁 {b.trialHint.replace('{n}', String(a.trialMinBattles - data.games)).replace('{days}', String(a.trialDays))}
      </p>
    )
  }
  return null
}

/**
 * «Стоп-тильт» — главное, за что платят, поэтому стоит выше подробностей. С Плюсом
 * это переключатель, без него — объяснение и кнопка. Перед включением просим у
 * Telegram право писать в личку: без него бот не достучится до того, кто открыл
 * приложение, но ни разу не нажимал «Старт» в чате.
 */
function TiltAlertsCard({ unlocked, onOpenPlus, t }: { unlocked: boolean; onOpenPlus: () => void; t: Translations }) {
  const b = t.battles
  const [plus, setPlus] = useState<PlusStatus | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!unlocked) return
    let alive = true
    api.getPlus().then(s => { if (alive) setPlus(s) }).catch(() => { /* переключатель просто не появится */ })
    return () => { alive = false }
  }, [unlocked])

  if (!unlocked) {
    return (
      <section className="card ba-alerts ba-alerts-locked">
        <div className="card-title">{b.alertsTitle}</div>
        <p className="muted small" style={{ margin: '0 0 10px' }}>{b.alertsPlusOnly}</p>
        <button className="btn-mini" onClick={onOpenPlus}>{b.unlockBtn}</button>
      </section>
    )
  }
  if (!plus) return null

  const on = plus.tiltAlerts && !plus.dmBlocked
  const toggle = async () => {
    haptic('medium')
    setBusy(true)
    try {
      const next = !on
      if (next && !(await requestWriteAccess())) {
        hapticNotify('error')
        setPlus({ ...plus, dmBlocked: true })
        return
      }
      await api.setTiltAlerts(next)
      hapticNotify('success')
      setPlus({ ...plus, tiltAlerts: next, dmBlocked: false })
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card ba-alerts">
      <div className="ba-alerts-row">
        <div className="ba-alerts-text">
          <div className="card-title" style={{ margin: 0 }}>{b.alertsTitle}</div>
          <span className="muted small">{on ? b.alertsOn : b.alertsOff}</span>
        </div>
        <button
          className={`ba-switch ${on ? 'ba-switch-on' : ''}`}
          role="switch"
          aria-checked={on}
          disabled={busy}
          onClick={toggle}
        >
          <span className="ba-switch-knob" />
        </button>
      </div>
      {plus.dmBlocked && <p className="muted small" style={{ margin: '8px 0 0' }}>{b.alertsBlocked}</p>}
    </section>
  )
}

/** Замок с цифрой самого игрока: «ещё 4 карты» продаёт лучше, чем «больше аналитики». */
function LockedRow({ text, onOpen }: { text: string; onOpen: () => void }) {
  return (
    <button className="ba-locked" onClick={onOpen}>
      <span className="ba-locked-icon">🔒</span>
      <span className="ba-locked-text">{text}</span>
      <span className="ba-locked-arrow">›</span>
    </button>
  )
}

function lockedLines(locked: ReviewLocked | null, t: Translations) {
  const b = t.battles
  return {
    decks: locked && locked.decks > 0 ? b.lockedDecks.replace('{n}', String(locked.decks)) : null,
    counters: locked && locked.counters > 0 ? b.lockedCounters.replace('{n}', String(locked.counters)) : null,
    tough: locked && locked.toughCards > 0 ? b.lockedTough.replace('{n}', String(locked.toughCards)) : null,
    weekdays: locked?.weekdays ? b.lockedWeekdays : null,
  }
}

/** Выводы по порядку важности; каждый — только когда за ним достаточно боёв. */
function buildInsights(d: BattleAnalysis, t: Translations): string[] {
  const b = t.battles
  const out: string[] = []
  const fill = (s: string, v: Record<string, string | number>) =>
    Object.entries(v).reduce((acc, [k, x]) => acc.replace(`{${k}}`, String(x)), s)

  const tilt = d.tilt
  if (tilt && tilt.afterTwoLossesGames >= 4) {
    if (tilt.afterTwoLossesWinPercent <= d.winPercent - 10)
      out.push('🧊 ' + fill(b.insightTilt, { a: tilt.afterTwoLossesWinPercent, b: d.winPercent }))
    else if (tilt.afterTwoLossesWinPercent >= d.winPercent)
      out.push('💪 ' + fill(b.insightNoTilt, { a: tilt.afterTwoLossesWinPercent }))
  }

  const e = d.elixir
  if (e && e.games >= MIN_SLOT_GAMES) {
    if (e.avgLeakLosses >= 1.5 && e.avgLeakLosses - e.avgLeakWins >= 0.8)
      out.push('💧 ' + fill(b.insightLeak, { x: e.avgLeakLosses, y: e.avgLeakWins }))
    else if (e.avgLeakAll < 1)
      out.push('💧 ' + fill(b.insightLeakGood, { x: e.avgLeakAll }))
  }

  const parts = d.dayParts.filter(p => p.games >= MIN_SLOT_GAMES)
  if (parts.length >= 2) {
    const sorted = [...parts].sort((x, y) => y.winPercent - x.winPercent)
    const best = sorted[0]
    const worst = sorted[sorted.length - 1]
    if (best.winPercent - worst.winPercent >= 10)
      out.push('🕐 ' + fill(b.insightTime, {
        best: b.parts[best.key as PartKey], x: best.winPercent,
        worst: b.parts[worst.key as PartKey], y: worst.winPercent,
      }))
  }

  const tough = d.toughCards[0]
  if (tough) out.push('🎯 ' + fill(b.insightTough, { card: tough.card.name, x: tough.winPercent, n: tough.games }))

  const main = d.decks[0]
  if (main && main.games >= MIN_SLOT_GAMES && main.metaWinPercent !== null) {
    if (main.winPercent >= main.metaWinPercent + 3)
      out.push('🃏 ' + fill(b.insightDeckBetter, { x: main.winPercent, y: main.metaWinPercent }))
    else if (main.winPercent <= main.metaWinPercent - 5)
      out.push('🃏 ' + fill(b.insightDeckWorse, { x: main.winPercent, y: main.metaWinPercent }))
  }

  return out.slice(0, 4)
}

function ElixirCard({ data, t }: { data: BattleAnalysis; t: Translations }) {
  const b = t.battles
  const e = data.elixir
  const max = e ? Math.max(e.avgLeakWins, e.avgLeakLosses, 1) : 1
  return (
    <section className="card">
      <div className="card-title">{b.elixirTitle}</div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>{b.elixirHint}</p>
      {!e ? <p className="muted small" style={{ margin: 0 }}>{b.elixirNone}</p> : (
        <>
          <Bar label={b.elixirWins} value={e.avgLeakWins} max={max} text={`${e.avgLeakWins}`} cls="ba-bar-good" />
          <Bar label={b.elixirLosses} value={e.avgLeakLosses} max={max} text={`${e.avgLeakLosses}`} cls="ba-bar-bad" />
        </>
      )}
    </section>
  )
}

function DecksCard({ decks, locked, onOpenPlus, t }: {
  decks: MyDeck[]; locked: ReviewLocked | null; onOpenPlus: () => void; t: Translations
}) {
  const b = t.battles
  const lines = lockedLines(locked, t)
  return (
    <section className="card">
      <div className="card-title">{b.decksTitle}</div>
      {decks.length === 0 && <p className="muted small" style={{ margin: 0 }}>{b.noDecks}</p>}
      {decks.map((d, i) => (
        <div key={i} className="mdeck">
          <div className="mdeck-head">
            <span className="mdeck-win">{d.winPercent}% <span className="mdeck-unit">{b.deckYou}</span></span>
            {d.metaWinPercent !== null && (
              <span className="mdeck-win ba-top-win">{d.metaWinPercent}% <span className="mdeck-unit">{b.deckTop}</span></span>
            )}
            <span className="muted small mdeck-wdl">{d.games} {b.games}</span>
          </div>
          <div className="wtop-deck">
            {d.cards.map((c, j) => <CardIcon key={`${c.cardId}-${j}`} card={c} />)}
          </div>
          <p className="muted small" style={{ margin: '6px 0 0' }}>
            {d.metaWinPercent === null
              ? b.deckNotInMeta
              : d.metaSharedCards >= 8 ? b.deckSame : b.deckSimilar.replace('{n}', String(d.metaSharedCards))}
          </p>
          {d.metaCounters.length > 0 && (
            <div className="mdeck-counters">
              <span className="muted small">{b.deckCounters}</span>
              {d.metaCounters.map(c => (
                <span key={`${c.card.cardId}-${c.card.evo}`} className="mdeck-counter" title={c.card.name}>
                  <CardIcon card={c.card} />
                  <span className="wtop-down mdeck-counter-pct">{c.deltaPercent}</span>
                </span>
              ))}
            </div>
          )}
          {d.copyLink && (
            <a className="btn-mini mdeck-open" href={d.copyLink} target="_blank" rel="noreferrer"
               onClick={() => haptic('medium')}>
              {t.worldTop.deckOpen}
            </a>
          )}
        </div>
      ))}
      {lines.counters && <LockedRow text={lines.counters} onOpen={onOpenPlus} />}
      {lines.decks && <LockedRow text={lines.decks} onOpen={onOpenPlus} />}
    </section>
  )
}

function TimeCard({ data, onOpenPlus, t }: { data: BattleAnalysis; onOpenPlus: () => void; t: Translations }) {
  const lines = lockedLines(data.locked, t)
  const b = t.battles
  const slot = (s: TimeSlot, label: string) => (
    <Bar
      key={s.key}
      label={label}
      value={s.games >= MIN_SLOT_GAMES ? s.winPercent : 0}
      max={100}
      text={s.games === 0 ? '—' : `${s.winPercent}% · ${s.games}`}
      cls={s.games < MIN_SLOT_GAMES ? 'ba-bar-dim' : s.winPercent >= data.winPercent ? 'ba-bar-good' : 'ba-bar-bad'}
    />
  )
  return (
    <section className="card">
      <div className="card-title">{b.timeTitle}</div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>{b.timeHint}</p>
      {data.dayParts.map(s => slot(s, b.partsTitle[s.key as PartKey] ?? s.key))}
      {lines.weekdays && <LockedRow text={lines.weekdays} onOpen={onOpenPlus} />}
      <div className="ba-week">
        {data.weekdays.map(s => (
          <div key={s.key} className="ba-day">
            <span className={`ba-day-pct ${s.games < MIN_SLOT_GAMES ? 'muted' : s.winPercent >= data.winPercent ? 'wtop-up' : 'wtop-down'}`}>
              {s.games === 0 ? '—' : `${Math.round(s.winPercent)}%`}
            </span>
            <span className="muted small">{b.weekdays[Number(s.key)]}</span>
          </div>
        ))}
      </div>
    </section>
  )
}

function TiltCard({ data, t }: { data: BattleAnalysis; t: Translations }) {
  const b = t.battles
  const tilt = data.tilt
  if (!tilt) return null
  return (
    <section className="card">
      <div className="card-title">{b.tiltTitle}</div>
      <Bar label={b.tiltOverall} value={data.winPercent} max={100} text={`${data.winPercent}%`} cls="ba-bar-dim" />
      <Bar
        label={b.tiltAfterWin}
        value={tilt.afterWinWinPercent}
        max={100}
        text={tilt.afterWinGames ? `${tilt.afterWinWinPercent}% · ${tilt.afterWinGames}` : '—'}
        cls="ba-bar-good"
      />
      <Bar
        label={b.tiltAfter2}
        value={tilt.afterTwoLossesWinPercent}
        max={100}
        text={tilt.afterTwoLossesGames ? `${tilt.afterTwoLossesWinPercent}% · ${tilt.afterTwoLossesGames}` : '—'}
        cls={tilt.afterTwoLossesWinPercent < data.winPercent ? 'ba-bar-bad' : 'ba-bar-good'}
      />
      <p className="muted small" style={{ margin: '8px 0 0' }}>{b.tiltStreak}: <b>{tilt.longestLossStreak}</b></p>
    </section>
  )
}

function ToughCard({ data, onOpenPlus, t }: { data: BattleAnalysis; onOpenPlus: () => void; t: Translations }) {
  const b = t.battles
  const lines = lockedLines(data.locked, t)
  return (
    <section className="card">
      <div className="card-title">{b.toughTitle}</div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>{b.toughHint}</p>
      {data.toughCards.length === 0
        ? <p className="muted small" style={{ margin: 0 }}>{b.toughNone}</p>
        : (
          <div className="ba-tough">
            {data.toughCards.map(c => (
              <div key={`${c.card.cardId}-${c.card.evo}`} className="ba-tough-item">
                <CardIcon card={c.card} />
                <span className="wtop-down ba-tough-pct">{c.winPercent}%</span>
                <span className="muted small">{c.games} {b.toughGames}</span>
              </div>
            ))}
          </div>
        )}
      {lines.tough && <LockedRow text={lines.tough} onOpen={onOpenPlus} />}
    </section>
  )
}

function Bar({ label, value, max, text, cls }: { label: string; value: number; max: number; text: string; cls: string }) {
  const pct = Math.max(0, Math.min(100, (value / max) * 100))
  return (
    <div className="ba-bar-row">
      <span className="ba-bar-label">{label}</span>
      <span className="ba-bar-track"><span className={`ba-bar-fill ${cls}`} style={{ width: `${pct}%` }} /></span>
      <span className="ba-bar-text">{text}</span>
    </div>
  )
}
