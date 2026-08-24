import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { Plan, RaceScout, ScoutClan } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; data: RaceScout }

interface Props {
  plan: Plan
}

/**
 * «Разведка гонки» (Pro).
 *
 * Таблица гонки отвечает, кто впереди сейчас. К четвергу этот ответ бесполезен:
 * клан, вырвавшийся в среду, может оказаться слабее того, кто раскачивается
 * к воскресенью. Здесь — чего соперники стоят вообще: обычный результат,
 * стабильность, дисциплина и то, идут ли они сейчас выше или ниже себя самих.
 *
 * На Free карточка показывает то же, что и таблица гонки (имена и места), и одну
 * честную строчку о том, что разведка нашла. Дразнилка приходит с сервера кодом:
 * обещать в ней можно только посчитанное, иначе человек купит Pro и не найдёт
 * внутри того, что ему показали.
 */
export function ScoutCard({ plan }: Props) {
  const { t } = useT()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [openTag, setOpenTag] = useState<string | null>(null)

  useEffect(() => {
    let alive = true
    api.getRaceScout()
      .then(data => { if (alive) setState({ kind: 'ready', data }) })
      .catch(() => { if (alive) setState({ kind: 'error' }) })
    return () => { alive = false }
  }, [])

  if (state.kind === 'loading') {
    return (
      <section className="card">
        <div className="card-title">{t.scout.title}</div>
        <div className="center" style={{ padding: 12 }}><div className="spinner" /></div>
      </section>
    )
  }

  if (state.kind === 'error') {
    return (
      <section className="card">
        <div className="card-title">{t.scout.title}</div>
        <p className="muted small">{t.scout.error}</p>
      </section>
    )
  }

  const { data } = state
  const locked = !data.isPro

  const teaser = data.freeTeaser === 'rivalBelowUsual' ? t.scout.teaserBelowUsual
    : data.freeTeaser === 'rivalUnstable' ? t.scout.teaserUnstable
    : data.freeTeaser === 'weAreStronger' ? t.scout.teaserStronger
    : data.freeTeaser === 'generic' ? t.scout.teaserGeneric
    : null

  return (
    <section className={`card scout-card ${locked ? 'forecast-locked' : ''}`}>
      <div className="card-title-row">
        <div className="card-title">{t.scout.title}</div>
        <span className="pro-chip">PRO</span>
      </div>

      {locked ? (
        <>
          <p className="muted small">{t.scout.lockedNote}</p>
          {teaser && <p className="scout-teaser">🔒 {teaser}</p>}
          <ul className="scout-list scout-list-locked">
            {data.clans.map(c => (
              <li key={c.tag} className={`scout-row ${c.isOurClan ? 'scout-row-ours' : ''}`}>
                <span className="scout-place">{c.position}</span>
                <span className="scout-name">{c.name}</span>
                <span className="muted small">{fmt(c.currentFame)} 🏅</span>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <>
          <p className="muted small scout-hint">
            {data.weeksAnalyzed > 0
              ? `${t.scout.basedOn} ${data.weeksAnalyzed} ${t.scout.weeks}`
              : t.scout.noHistory}
          </p>

          <ul className="scout-list">
            {data.clans.map(c => (
              <ScoutRow
                key={c.tag}
                clan={c}
                isRealRival={c.tag === data.realRivalTag}
                open={openTag === c.tag}
                onToggle={() => { haptic('light'); setOpenTag(openTag === c.tag ? null : c.tag) }}
                t={t}
              />
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

function ScoutRow({ clan, isRealRival, open, onToggle, t }: {
  clan: ScoutClan
  isRealRival: boolean
  open: boolean
  onToggle: () => void
  t: Translations
}) {
  const noData = clan.weeksTracked === 0

  // Знак темпа важнее величины: «идёт выше себя» и «просел» — разные новости,
  // а разница в пару процентов — шум, который не стоит подсвечивать.
  const pace = clan.paceVsUsualPercent
  const paceKind = noData || pace === 0 ? null : pace >= 10 ? 'up' : pace <= -10 ? 'down' : 'even'

  return (
    <li className={`scout-row-wrap ${clan.isOurClan ? 'scout-row-ours' : ''}`}>
      <button className="scout-row scout-row-btn" onClick={onToggle} aria-expanded={open}>
        <span className="scout-place">{clan.position}</span>

        <span className="scout-info">
          <span className="scout-name-row">
            <span className="scout-name">{clan.name}</span>
            {clan.isOurClan && <span className="me-badge">{t.scout.us}</span>}
            {isRealRival && <span className="scout-rival-badge">{t.scout.realRival}</span>}
          </span>
          <span className="muted small">
            {noData
              ? t.scout.noHistoryClan
              : `${t.scout.usually} ${fmt(clan.avgWeekFame)} 🏅 · ${clan.avgRank.toFixed(1)}${t.scout.placeSuffix}`}
          </span>
        </span>

        <span className="scout-right">
          <span className="scout-fame">{fmt(clan.currentFame)} 🏅</span>
          {paceKind && (
            <span className={`scout-pace scout-pace-${paceKind}`}>
              {paceKind === 'up' ? '▲' : paceKind === 'down' ? '▼' : '='}
              {paceKind !== 'even' && ` ${Math.abs(pace)}%`}
            </span>
          )}
        </span>
        <span className="chevron">{open ? '⌄' : '›'}</span>
      </button>

      {open && !noData && (
        <div className="scout-details fade-in">
          <div className="scout-facts">
            <Fact label={t.scout.avgWeek} value={fmt(clan.avgWeekFame)} />
            <Fact label={t.scout.bestWeek} value={fmt(clan.bestWeekFame)} />
            <Fact label={t.scout.decks} value={`${clan.avgDecksPerPlayer}/16`} />
            <Fact label={t.scout.fighters} value={String(clan.avgParticipants)} />
          </div>

          <p className="muted small scout-verdict">
            {clan.volatility >= 50 ? `🎲 ${t.scout.unstable}` : `🛡 ${t.scout.stable}`}
            {clan.avgDecksPerPlayer >= 14 && ` · ✅ ${t.scout.disciplined}`}
            {clan.avgDecksPerPlayer > 0 && clan.avgDecksPerPlayer < 11 && ` · 😴 ${t.scout.sloppy}`}
            {clan.fadesLate && ` · 📉 ${t.scout.fading}`}
          </p>

          {clan.dayPoints.length > 0 && (
            <div className="scout-days">
              <span className="muted small">{t.scout.byDay}</span>
              <div className="scout-days-row">
                {clan.dayPoints.map((p, i) => (
                  <span key={i} className="scout-day">
                    <b>{fmt(p)}</b>
                    <span className="muted small">{t.scout.dayShort}{i + 1}</span>
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {open && noData && (
        <div className="scout-details fade-in">
          <p className="muted small" style={{ margin: 0 }}>{t.scout.noHistoryHint}</p>
        </div>
      )}
    </li>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="scout-fact">
      <span className="scout-fact-value">{value}</span>
      <span className="scout-fact-label">{label}</span>
    </div>
  )
}
