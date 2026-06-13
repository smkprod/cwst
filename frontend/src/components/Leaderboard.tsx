import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { GlobalTop, Plan, PlayerStatus, SeasonBreakdown, SeasonPlayer } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { PlayerInfoModal } from './PlayerInfoModal'
import { HistoryCard } from './HistoryCard'

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
  plan: Plan
}

type Selection = 'current' | 'season' | 'global'

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

const MEDALS = ['🥇', '🥈', '🥉']

export function Leaderboard({ players, myPlayerTag, plan }: Props) {
  const [sel, setSel] = useState<Selection>('current')
  const [breakdown, setBreakdown] = useState<BreakdownState>({ kind: 'idle' })
  const [global, setGlobal] = useState<GlobalState>({ kind: 'idle' })
  const [selected, setSelected] = useState<PlayerStatus | null>(null)

  // Разбивку сезона грузим сразу для всех — нужна для сезонного зачёта
  useEffect(() => {
    if (breakdown.kind !== 'idle') return
    setBreakdown({ kind: 'loading' })
    api.getSeasonBreakdown()
      .then(data => setBreakdown(data.weeks.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setBreakdown({ kind: 'empty' }))
  }, [breakdown.kind])

  // Глобальный топ — лениво при первом выборе
  useEffect(() => {
    if (sel !== 'global' || global.kind !== 'idle') return
    setGlobal({ kind: 'loading' })
    api.getGlobalTop()
      .then(data => setGlobal(data.players.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setGlobal({ kind: 'empty' }))
  }, [sel, global.kind])

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
        <h2 className="section-title" style={{ margin: 0 }}>🏆 Рейтинг</h2>
        <select
          className="rating-select"
          value={sel}
          onChange={e => onPick(e.target.value)}
          aria-label="Период рейтинга"
        >
          <option value="current">📅 Текущая война</option>
          <option value="season">🏆 Весь сезон</option>
          <option value="global">🌍 Топ бота</option>
        </select>
      </div>

      {sel === 'current' && (
        <WeekBoard players={players} myPlayerTag={myPlayerTag} onOpen={openByTag} plan={plan} />
      )}
      {sel === 'season' && <SeasonBoard state={breakdown} myPlayerTag={myPlayerTag} rosterTags={new Set(players.map(p => p.playerTag))} onOpen={openByTag} />}
      {sel === 'global' && <GlobalBoard state={global} />}

      {sel !== 'global' && (
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

/* --- Недельный зачёт (текущая неделя, live-данные) --- */
function WeekBoard({ players, myPlayerTag, onOpen, plan }: {
  players: PlayerStatus[]; myPlayerTag?: string; onOpen: (tag: string) => void; plan: Plan
}) {
  const sorted = [...players].sort((a, b) => a.rank - b.rank)
  const podium = sorted.slice(0, 3)
  const rest = sorted.slice(3)
  const mvp = podium[0]

  if (sorted.length === 0) {
    return <p className="center muted">Пока нет данных для рейтинга.</p>
  }

  return (
    <>
      {mvp && mvp.fame > 0 && (
        <div className="mvp-banner">
          👑 MVP недели: <strong>{mvp.name}</strong> — {fmt(mvp.fame)} медалей
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
                {p.playerTag === myPlayerTag && <span className="me-badge">ты</span>}
                {plan === 'pro' && p.consecutiveWars >= 3 && (
                  <span className="streak-badge" title={`${p.consecutiveWars} недель подряд`}>
                    🔥{p.consecutiveWars}
                  </span>
                )}
              </span>
              <span className="rating-avg muted">{p.avgFamePerAttack > 0 ? `${Math.round(p.avgFamePerAttack)}/атака` : ''}</span>
              <span className="rating-fame">{fmt(p.fame)} 🏅</span>
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

/* --- Глобальный топ бота: привязанные игроки из всех кланов --- */
function GlobalBoard({ state }: { state: GlobalState }) {
  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }

  if (state.kind === 'empty') {
    return (
      <p className="center muted">
        Здесь соревнуются игроки из всех кланов бота, привязавшие аккаунт через /link.
        Данные копятся с каждой неделей войны.
      </p>
    )
  }

  const { data } = state
  const mvp = data.players[0]

  return (
    <>
      <p className="muted small season-meta">
        Игроки всех кланов бота · окно: {data.weeksWindow} недель · участников: {data.playersTracked}
      </p>

      {mvp && mvp.totalFame > 0 && (
        <div className="mvp-banner">
          👑 Чемпион бота: <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} медалей
        </div>
      )}

      <ul className="rating-list">
        {data.players.map(p => (
          <li key={p.playerTag}>
            <div className={`rating-row ${p.isMe ? 'rating-me' : ''}`}>
              <span className="rating-rank">{p.rank <= 3 ? MEDALS[p.rank - 1] : `#${p.rank}`}</span>
              <span className="rating-name">
                {p.name}
                {p.isMe && <span className="me-badge">ты</span>}
                <span className="muted small" style={{ display: 'block' }}>{p.clanName}</span>
              </span>
              <span className="rating-avg muted">
                {p.weeksParticipated} нед{p.avgFamePerAttack > 0 ? ` · ${Math.round(p.avgFamePerAttack)}/атака` : ''}
              </span>
              <span className="rating-fame">{fmt(p.totalFame)} 🏅</span>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}

/* --- Сезонный зачёт: сумма по всем неделям сезона (W1 → Колизей) --- */
function SeasonBoard({ state, myPlayerTag, rosterTags, onOpen }: {
  state: BreakdownState; myPlayerTag?: string; rosterTags: Set<string>; onOpen: (tag: string) => void
}) {
  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }
  if (state.kind === 'empty') {
    return (
      <p className="center muted">
        Данные сезона появятся после первого военного дня.
      </p>
    )
  }

  const { data } = state
  const total: SeasonPlayer[] = data.seasonTotal
  const mvp = total[0]

  return (
    <>
      <p className="muted small season-meta">
        Сезон #{data.seasonId} · недель в зачёте: {data.weeks.length} (W1 → текущая)
      </p>

      {mvp && mvp.totalFame > 0 && (
        <div className="mvp-banner">
          👑 MVP сезона: <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} медалей
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
                  {p.playerTag === myPlayerTag && <span className="me-badge">ты</span>}
                </span>
                <span className="rating-avg muted">
                  {p.weeksParticipated} нед · луч. {fmt(p.bestWeekFame)}
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

