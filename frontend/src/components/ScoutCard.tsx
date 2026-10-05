import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { RaceScout, ScoutClan } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { useOpenClan } from '../lib/clanModal'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; data: RaceScout }

/**
 * «Разведка гонки».
 *
 * Таблица гонки отвечает, кто впереди сейчас. К четвергу этот ответ бесполезен:
 * клан, вырвавшийся в среду, может оказаться слабее того, кто раскачивается
 * к воскресенью. Здесь — чего соперники стоят вообще: обычный результат,
 * стабильность, дисциплина и то, идут ли они сейчас выше или ниже себя самих.
 */
export function ScoutCard() {
  const openClan = useOpenClan()
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
        <SectionHead icon="radar" tone="blue" title={t.scout.title} />
        <div className="center" style={{ padding: 12 }}><div className="spinner" /></div>
      </section>
    )
  }

  if (state.kind === 'error') {
    return (
      <section className="card">
        <SectionHead icon="radar" tone="blue" title={t.scout.title} />
        <p className="muted small">{t.scout.error}</p>
      </section>
    )
  }

  const { data } = state

  return (
    <section className="card scout-card">
      <SectionHead
        icon="radar"
        tone="blue"
        title={t.scout.title}
        aside={data.weeksAnalyzed > 0 ? `${t.scout.basedOn} ${data.weeksAnalyzed} ${t.scout.weeks}` : undefined}
      />
      {data.weeksAnalyzed === 0 && <p className="muted small scout-hint">{t.scout.noHistory}</p>}

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
  const openClan = useOpenClan()
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
            {/* Строка раскрывает досье, имя открывает страницу клана */}
            <span className="scout-name clan-name-link" role="button" tabIndex={0}
                  onClick={e => { e.stopPropagation(); openClan(clan.tag, clan.name) }}>
              {clan.name} <Icon name="external" size={11} />
            </span>
            {clan.isOurClan && <span className="me-badge">{t.scout.us}</span>}
            {isRealRival && <span className="scout-rival-badge">{t.scout.realRival}</span>}
          </span>
          <span className="muted small">
            {noData
              ? t.scout.noHistoryClan
              : <span className="cl-ic">{t.scout.usually} {fmt(clan.avgWeekFame)} <Icon name="medal" size={11} className="cl-fame" /> · {clan.avgRank.toFixed(1)}{t.scout.placeSuffix}</span>}
          </span>
        </span>

        <span className="scout-right">
          <span className="scout-fame cl-ic">{fmt(clan.currentFame)} <Icon name="medal" size={13} className="cl-fame" /></span>
          {paceKind && (
            <span className={`scout-pace scout-pace-${paceKind} cl-ic`}>
              {paceKind === 'up' ? <Icon name="trendUp" size={12} /> : paceKind === 'down' ? <Icon name="trendDown" size={12} /> : '='}
              {paceKind !== 'even' && ` ${Math.abs(pace)}%`}
            </span>
          )}
        </span>
        <span className={`cl-chev ${open ? 'cl-chev-open' : ''}`}><Icon name="chevronRight" size={16} /></span>
      </button>

      {open && !noData && (
        <div className="scout-details fade-in">
          <div className="scout-facts">
            <Fact label={t.scout.avgWeek} value={fmt(clan.avgWeekFame)} />
            <Fact label={t.scout.bestWeek} value={fmt(clan.bestWeekFame)} />
            <Fact label={t.scout.decks} value={`${clan.avgDecksPerPlayer}/16`} />
            <Fact label={t.scout.fighters} value={String(clan.avgParticipants)} />
          </div>

          <p className="muted small scout-verdict cl-verdict">
            {clan.volatility >= 50
              ? <span className="cl-ic"><Icon name="dice" size={13} className="cl-wait" />{t.scout.unstable}</span>
              : <span className="cl-ic"><Icon name="shield" size={13} className="cl-ok" />{t.scout.stable}</span>}
            {clan.avgDecksPerPlayer >= 14 && <span className="cl-ic"><Icon name="checkCircle" size={13} className="cl-ok" />{t.scout.disciplined}</span>}
            {clan.avgDecksPerPlayer > 0 && clan.avgDecksPerPlayer < 11 && <span className="cl-ic"><Icon name="moon" size={13} className="cl-bad" />{t.scout.sloppy}</span>}
            {clan.fadesLate && <span className="cl-ic"><Icon name="trendDown" size={13} className="cl-bad" />{t.scout.fading}</span>}
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
