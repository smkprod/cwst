import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { GlobalTop, Plan, PlayerStatus, SeasonArchive, SeasonBreakdown, SeasonPlayer } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { PlayerInfoModal } from './PlayerInfoModal'
import { HistoryCard } from './HistoryCard'

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
  plan: Plan
}

type Selection = 'current' | 'season' | 'archive' | 'global'

type BreakdownState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: SeasonBreakdown }
type GlobalState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: GlobalTop }
type ArchiveState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: SeasonArchive }

const MEDALS = ['🥇', '🥈', '🥉']

export function Leaderboard({ players, myPlayerTag, plan }: Props) {
  const [sel, setSel] = useState<Selection>('current')
  const [breakdown, setBreakdown] = useState<BreakdownState>({ kind: 'idle' })
  const [global, setGlobal] = useState<GlobalState>({ kind: 'idle' })
  const [archive, setArchive] = useState<ArchiveState>({ kind: 'idle' })
  const [selected, setSelected] = useState<PlayerStatus | null>(null)
  const { t } = useT()

  useEffect(() => {
    if (breakdown.kind !== 'idle') return
    setBreakdown({ kind: 'loading' })
    api.getSeasonBreakdown()
      .then(data => setBreakdown(data.weeks.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setBreakdown({ kind: 'empty' }))
  }, [breakdown.kind])

  useEffect(() => {
    if (sel !== 'global' || global.kind !== 'idle') return
    setGlobal({ kind: 'loading' })
    api.getGlobalTop()
      .then(data => setGlobal(data.players.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setGlobal({ kind: 'empty' }))
  }, [sel, global.kind])

  useEffect(() => {
    if (sel !== 'archive' || archive.kind !== 'idle') return
    setArchive({ kind: 'loading' })
    api.getSeasonArchive()
      .then(data => setArchive(data.seasons.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setArchive({ kind: 'empty' }))
  }, [sel, archive.kind])

  const openByTag = (tag: string) => {
    const p = players.find(x => x.playerTag === tag)
    if (p) {
      haptic('light')
      setSelected(p)
    }
  }

  const onPick = (value: string) => {
    haptic('light')
    setSel(value as Selection)
  }

  return (
    <div>
      <div className="rating-head">
        <h2 className="section-title" style={{ margin: 0 }}>{t.leaderboard.title}</h2>
        <select
          className="rating-select"
          value={sel}
          onChange={e => onPick(e.target.value)}
          aria-label={t.leaderboard.period}
        >
          <option value="current">{t.leaderboard.current}</option>
          <option value="season">{t.leaderboard.season}</option>
          <option value="archive">{t.leaderboard.archive}</option>
          <option value="global">{t.leaderboard.global}</option>
        </select>
      </div>

      {sel === 'current' && (
        <WeekBoard players={players} myPlayerTag={myPlayerTag} onOpen={openByTag} plan={plan} />
      )}
      {sel === 'season' && <SeasonBoard state={breakdown} myPlayerTag={myPlayerTag} rosterTags={new Set(players.map(p => p.playerTag))} onOpen={openByTag} />}
      {sel === 'archive' && <ArchiveBoard state={archive} myPlayerTag={myPlayerTag} />}
      {sel === 'global' && <GlobalBoard state={global} />}

      {sel !== 'global' && sel !== 'archive' && (
        <>
          <div style={{ height: 12 }} />
          <HistoryCard plan={plan} />
        </>
      )}

      {selected && (
        <PlayerInfoModal
          player={selected}
          isMe={selected.playerTag === myPlayerTag}
          onClose={() => setSelected(null)}
        />
      )}
    </div>
  )
}

function WeekBoard({ players, myPlayerTag, onOpen, plan }: {
  players: PlayerStatus[]; myPlayerTag?: string; onOpen: (tag: string) => void; plan: Plan
}) {
  const { t } = useT()
  const sorted = [...players].sort((a, b) => a.rank - b.rank)
  const podium = sorted.slice(0, 3)
  const rest = sorted.slice(3)
  const mvp = podium[0]

  if (sorted.length === 0) {
    return <p className="center muted">{t.leaderboard.noDataCurrent}</p>
  }

  return (
    <>
      {mvp && mvp.fame > 0 && (
        <div className="mvp-banner">
          {t.leaderboard.mvpWeek} <strong>{mvp.name}</strong> — {fmt(mvp.fame)} {t.leaderboard.medals}
        </div>
      )}

      <div className="podium">
        {[podium[1], podium[0], podium[2]].map((p, i) =>
          p ? (
            <button
              key={p.playerTag}
              onClick={() => onOpen(p.playerTag)}
              className={`podium-spot podium-${i === 1 ? 1 : i === 0 ? 2 : 3} ${p.playerTag === myPlayerTag ? 'podium-me' : ''}`}
            >
              <span className="podium-medal">{MEDALS[p.rank - 1] ?? ''}</span>
              <span className="podium-name">{p.name}</span>
              <span className="podium-fame">{fmt(p.fame)}</span>
              <div className="podium-bar" />
            </button>
          ) : (
            <div key={`empty-${i}`} className="podium-spot" />
          ),
        )}
      </div>

      <ul className="rating-list">
        {rest.map(p => (
          <li key={p.playerTag}>
            <button
              className={`rating-row ${p.playerTag === myPlayerTag ? 'rating-me' : ''}`}
              onClick={() => onOpen(p.playerTag)}
            >
              <span className="rating-rank">#{p.rank}</span>
              <span className="rating-name">
                {p.name}
                {p.playerTag === myPlayerTag && <span className="me-badge">{t.leaderboard.you}</span>}
                {plan === 'pro' && p.consecutiveWars >= 3 && (
                  <span className="streak-badge" title={`${p.consecutiveWars} ${t.leaderboard.streakTitle}`}>
                    🔥{p.consecutiveWars}
                  </span>
                )}
              </span>
              <span className="rating-avg muted">{p.avgFamePerAttack > 0 ? `${Math.round(p.avgFamePerAttack)}${t.leaderboard.perAttack}` : ''}</span>
              <span className="rating-fame">{fmt(p.fame)} 🏅</span>
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

function GlobalBoard({ state }: { state: GlobalState }) {
  const { t } = useT()

  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }

  if (state.kind === 'empty') {
    return <p className="center muted">{t.leaderboard.globalEmpty}</p>
  }

  const { data } = state
  const mvp = data.players[0]

  return (
    <>
      <p className="muted small season-meta">
        {t.leaderboard.globalMeta} {data.weeksWindow} {t.leaderboard.globalMetaWeeks} {data.playersTracked}
      </p>

      {mvp && mvp.totalFame > 0 && (
        <div className="mvp-banner">
          {t.leaderboard.mvpBot} <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} {t.leaderboard.medals}
        </div>
      )}

      <ul className="rating-list">
        {data.players.map(p => (
          <li key={p.playerTag}>
            <div className={`rating-row ${p.isMe ? 'rating-me' : ''}`}>
              <span className="rating-rank">{p.rank <= 3 ? MEDALS[p.rank - 1] : `#${p.rank}`}</span>
              <span className="rating-name">
                {p.name}
                {p.isMe && <span className="me-badge">{t.leaderboard.you}</span>}
                <span className="muted small" style={{ display: 'block' }}>{p.clanName}</span>
              </span>
              <span className="rating-avg muted">
                {p.weeksParticipated} {t.leaderboard.weeks}{p.avgFamePerAttack > 0 ? ` · ${Math.round(p.avgFamePerAttack)}${t.leaderboard.perAttack}` : ''}
              </span>
              <span className="rating-fame">{fmt(p.totalFame)} 🏅</span>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}

function ArchiveBoard({ state, myPlayerTag }: { state: ArchiveState; myPlayerTag?: string }) {
  const { t } = useT()

  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }
  if (state.kind === 'empty') {
    return <p className="center muted">{t.leaderboard.archiveEmpty}</p>
  }

  return (
    <div className="archive-list">
      {state.data.seasons.map((s, si) => {
        const mvp = s.topPlayers[0]
        return (
          <details key={s.seasonId} className="card collapse-card" open={si === 0}>
            <summary className="card-title-row collapse-summary">
              <div className="card-title">{t.leaderboard.seasonMeta}{s.seasonId}</div>
              <span className="muted small">
                {s.weeksTracked} {t.leaderboard.archiveWeeks} · {fmt(s.clanTotalFame)} 🏅
              </span>
            </summary>

            {mvp && mvp.totalFame > 0 && (
              <div className="mvp-banner">
                {t.leaderboard.mvpSeason} <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} {t.leaderboard.medals}
              </div>
            )}

            <ul className="rating-list">
              {s.topPlayers.map(p => (
                <li key={p.playerTag}>
                  <div className={`rating-row ${p.playerTag === myPlayerTag ? 'rating-me' : ''}`}>
                    <span className="rating-rank">{p.rank <= 3 ? MEDALS[p.rank - 1] : `#${p.rank}`}</span>
                    <span className="rating-name">
                      {p.name}
                      {p.playerTag === myPlayerTag && <span className="me-badge">{t.leaderboard.you}</span>}
                    </span>
                    <span className="rating-avg muted">{p.weeksParticipated} {t.leaderboard.weeks}</span>
                    <span className="rating-fame">{fmt(p.totalFame)} 🏅</span>
                  </div>
                </li>
              ))}
            </ul>
          </details>
        )
      })}
    </div>
  )
}

function SeasonBoard({ state, myPlayerTag, rosterTags, onOpen }: {
  state: BreakdownState; myPlayerTag?: string; rosterTags: Set<string>; onOpen: (tag: string) => void
}) {
  const { t } = useT()

  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }
  if (state.kind === 'empty') {
    return <p className="center muted">{t.leaderboard.seasonEmpty}</p>
  }

  const { data } = state
  const total: SeasonPlayer[] = data.seasonTotal
  const mvp = total[0]

  return (
    <>
      <p className="muted small season-meta">
        {t.leaderboard.seasonMeta}{data.seasonId} {t.leaderboard.seasonMetaWeeks} {data.weeks.length} {t.leaderboard.seasonMetaSuffix}
      </p>

      {mvp && mvp.totalFame > 0 && (
        <div className="mvp-banner">
          {t.leaderboard.mvpSeason} <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} {t.leaderboard.medals}
        </div>
      )}

      <ul className="rating-list">
        {total.map(p => {
          const clickable = rosterTags.has(p.playerTag)
          const Tag = clickable ? 'button' : 'div'
          return (
            <li key={p.playerTag}>
              <Tag
                className={`rating-row ${p.playerTag === myPlayerTag ? 'rating-me' : ''}`}
                {...(clickable ? { onClick: () => onOpen(p.playerTag) } : {})}
              >
                <span className="rating-rank">{p.rank <= 3 ? MEDALS[p.rank - 1] : `#${p.rank}`}</span>
                <span className="rating-name">
                  {p.name}
                  {p.playerTag === myPlayerTag && <span className="me-badge">{t.leaderboard.you}</span>}
                </span>
                <span className="rating-avg muted">
                  {p.weeksParticipated} {t.leaderboard.weeks} · {t.leaderboard.best} {fmt(p.bestWeekFame)}
                </span>
                <span className="rating-fame">{fmt(p.totalFame)} 🏅</span>
              </Tag>
            </li>
          )
        })}
      </ul>
    </>
  )
}
