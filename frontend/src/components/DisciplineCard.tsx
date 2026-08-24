import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { ClanDiscipline, DisciplinePlayer, Plan } from '../types'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; data: ClanDiscipline }

type Section = 'skippers' | 'nudged' | 'late'

interface Props {
  plan: Plan
}

/**
 * «Дисциплина клана» — что пришло на смену шансу победы.
 *
 * Шанс победы был числом, с которым нечего делать: он менялся сам по себе, и повлиять
 * на него глава не мог — разве что смотреть. Здесь наоборот, каждая строка называет
 * человека и подсказывает разговор: этот не доигрывает, этого приходится пинать
 * каждую войну, а этот отыгрывает за час до конца и однажды не успеет.
 */
export function DisciplineCard({ plan }: Props) {
  const { t } = useT()
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [section, setSection] = useState<Section>('skippers')

  useEffect(() => {
    if (plan !== 'pro') return
    let alive = true
    api.getClanDiscipline()
      .then(data => { if (alive) setState({ kind: 'ready', data }) })
      .catch(() => { if (alive) setState({ kind: 'error' }) })
    return () => { alive = false }
  }, [plan])

  if (plan !== 'pro') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">{t.discipline.title}</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">{t.discipline.lockedNote}</p>
      </section>
    )
  }

  if (state.kind === 'loading') {
    return (
      <section className="card">
        <div className="card-title">{t.discipline.title}</div>
        <div className="center" style={{ padding: 12 }}><div className="spinner" /></div>
      </section>
    )
  }

  if (state.kind === 'error') {
    return (
      <section className="card">
        <div className="card-title">{t.discipline.title}</div>
        <p className="muted small">{t.discipline.error}</p>
      </section>
    )
  }

  const { data } = state
  const nothingTracked = data.weeksAnalyzed === 0

  const rows = section === 'skippers' ? data.skippers
    : section === 'nudged' ? data.nudged
    : data.lastMinute

  const hint = section === 'skippers' ? t.discipline.skippersHint
    : section === 'nudged' ? t.discipline.nudgedHint
    : t.discipline.lateHint

  const empty = section === 'skippers' ? t.discipline.emptySkippers
    : section === 'nudged' ? t.discipline.emptyNudged
    : t.discipline.emptyLate

  const switchTo = (next: Section) => { haptic('light'); setSection(next) }

  return (
    <section className="card discipline-card">
      <div className="card-title-row">
        <div className="card-title">{t.discipline.title}</div>
        {data.weeksAnalyzed > 0 && (
          <span className="muted small">
            {t.discipline.weeksPrefix} {data.weeksAnalyzed} {t.discipline.weeksSuffix}
          </span>
        )}
      </div>

      <div className="disc-tabs">
        <button
          className={`disc-tab ${section === 'skippers' ? 'disc-tab-on' : ''}`}
          onClick={() => switchTo('skippers')}
        >{t.discipline.tabSkippers}</button>
        <button
          className={`disc-tab ${section === 'nudged' ? 'disc-tab-on' : ''}`}
          onClick={() => switchTo('nudged')}
        >{t.discipline.tabNudged}</button>
        <button
          className={`disc-tab ${section === 'late' ? 'disc-tab-on' : ''}`}
          onClick={() => switchTo('late')}
        >{t.discipline.tabLate}</button>
      </div>

      <p className="muted small disc-hint">{hint}</p>

      {/* «Мало данных» и «все молодцы» — разные новости, и путать их нельзя:
          первое означает «приходи позже», второе — заслуженную похвалу. */}
      {rows.length === 0 && (
        <p className="muted small disc-empty">
          {nothingTracked && section === 'skippers' ? t.discipline.noData : empty}
        </p>
      )}

      <ul className="disc-list">
        {rows.map((p, i) => (
          <DisciplineRow key={p.playerTag} player={p} place={i + 1} section={section} t={t} />
        ))}
      </ul>
    </section>
  )
}

function DisciplineRow({ player, place, section, t }: {
  player: DisciplinePlayer
  place: number
  section: Section
  t: Translations
}) {
  const value = section === 'skippers' ? player.missedDecks
    : section === 'nudged' ? player.nudgeCount
    : player.lastMinuteBattles

  const unit = section === 'skippers' ? t.discipline.missedDecks
    : section === 'nudged' ? t.discipline.nudges
    : t.discipline.lateBattles

  // Вторая строка — контекст, без которого число врёт: 8 пропусков за 8 недель
  // и 8 за две — это разные люди.
  const detail = section === 'skippers'
    ? (player.missedWeeks > 0
        ? `${player.missedWeeks} ${t.discipline.missedWeeks} ${t.discipline.ofWeeks} ${player.weeksTracked}`
        : `${player.weeksTracked} ${t.discipline.weeksShort}`)
    : section === 'nudged'
      ? `${player.weeksTracked} ${t.discipline.weeksShort}`
      : `${t.discipline.avgLeftPrefix} ${player.avgHoursBeforeEnd} ${t.discipline.avgLeftSuffix}`

  return (
    <li className="disc-row">
      <span className="disc-place">{place}</span>
      <span className="disc-avatar">{player.avatarEmoji ?? '👤'}</span>
      <span className="disc-info">
        <span className="disc-name">{player.name}</span>
        <span className="muted small">{detail}</span>
      </span>
      <span className="disc-value">
        <b>{value}</b>
        <span className="muted small">{unit}</span>
      </span>
    </li>
  )
}
