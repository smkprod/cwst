import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { GlobalTop, Plan, PlayerStatus, SeasonStats } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { PlayerInfoModal } from './PlayerInfoModal'
import { HistoryCard } from './HistoryCard'

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
  plan: Plan
}

type Mode = 'week' | 'season' | 'global'
type SeasonState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'locked' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: SeasonStats }
type GlobalState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'ready'; data: GlobalTop }

const MEDALS = ['🥇', '🥈', '🥉']

export function Leaderboard({ players, myPlayerTag, plan }: Props) {
  const [mode, setMode] = useState<Mode>('week')
  const [season, setSeason] = useState<SeasonState>({ kind: 'idle' })
  const [global, setGlobal] = useState<GlobalState>({ kind: 'idle' })
  const [selected, setSelected] = useState<PlayerStatus | null>(null)

  // Сезон грузим лениво — при первом переключении
  useEffect(() => {
    if (mode !== 'season' || season.kind !== 'idle') return
    if (plan !== 'pro') {
      setSeason({ kind: 'locked' })
      return
    }
    setSeason({ kind: 'loading' })
    api.getMyClanSeason()
      .then(data => setSeason(data.players.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(e => setSeason(
        e instanceof ApiError && e.code === 'pro_required' ? { kind: 'locked' } : { kind: 'empty' },
      ))
  }, [mode, season.kind, plan])

  // Глобальный топ тоже лениво
  useEffect(() => {
    if (mode !== 'global' || global.kind !== 'idle') return
    setGlobal({ kind: 'loading' })
    api.getGlobalTop()
      .then(data => setGlobal(data.players.length === 0 ? { kind: 'empty' } : { kind: 'ready', data }))
      .catch(() => setGlobal({ kind: 'empty' }))
  }, [mode, global.kind])

  const switchMode = (m: Mode) => {
    haptic('light')
    setMode(m)
  }

  const openByTag = (tag: string) => {
    const p = players.find(x => x.playerTag === tag)
    if (p) {
      haptic('light')
      setSelected(p)
    }
  }

  return (
    <div>
      <div className="rating-head">
        <h2 className="section-title" style={{ margin: 0 }}>🏆 Рейтинг</h2>
        <div className="segmented" role="tablist">
          <button
            role="tab" aria-selected={mode === 'week'}
            className={`segment ${mode === 'week' ? 'segment-active' : ''}`}
            onClick={() => switchMode('week')}
          >Неделя</button>
          <button
            role="tab" aria-selected={mode === 'season'}
            className={`segment ${mode === 'season' ? 'segment-active' : ''}`}
            onClick={() => switchMode('season')}
          >Сезон</button>
          <button
            role="tab" aria-selected={mode === 'global'}
            className={`segment ${mode === 'global' ? 'segment-active' : ''}`}
            onClick={() => switchMode('global')}
          >🌍 Топ</button>
        </div>
      </div>

      {mode === 'week' && <WeekBoard players={players} myPlayerTag={myPlayerTag} onOpen={openByTag} plan={plan} onShowSeason={() => switchMode('season')} />}
      {mode === 'season' && <SeasonBoard state={season} myPlayerTag={myPlayerTag} onOpen={openByTag} />}
      {mode === 'global' && <GlobalBoard state={global} />}

      {mode !== 'global' && (
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

/* --- Недельный зачёт --- */
function WeekBoard({ players, myPlayerTag, onOpen, plan, onShowSeason }: {
  players: PlayerStatus[]; myPlayerTag?: string; onOpen: (tag: string) => void; plan: Plan; onShowSeason: () => void
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
      <button className="season-shortcut-btn" onClick={onShowSeason}>
        📅 Рейтинг за весь сезон (W1 → Колизей) →
      </button>

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

/* --- Сезонный зачёт --- */
function SeasonBoard({ state, myPlayerTag, onOpen }: {
  state: SeasonState; myPlayerTag?: string; onOpen: (tag: string) => void
}) {
  if (state.kind === 'loading' || state.kind === 'idle') {
    return <div className="center"><div className="spinner" /></div>
  }

  if (state.kind === 'locked') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">🏆 Сезонный зачёт</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">Кто больше всех набил медалей за сезон (~месяц КВ) — доступно на тарифе Pro. 🔒</p>
      </section>
    )
  }

  if (state.kind === 'empty') {
    return (
      <p className="center muted">
        Данные сезона копятся с каждым днём войны — загляни после первого военного дня.
      </p>
    )
  }

  const { data } = state
  const mvp = data.players[0]

  return (
    <>
      <p className="muted small season-meta">
        Сезон #{data.seasonId} · недель с данными: {data.weeksTracked}
      </p>

      {mvp && mvp.totalFame > 0 && (
        <div className="mvp-banner">
          👑 MVP сезона: <strong>{mvp.name}</strong> — {fmt(mvp.totalFame)} медалей
        </div>
      )}

      <ul className="rating-list">
        {data.players.map(p => (
          <li key={p.playerTag}>
            <button
              className={`rating-row ${p.playerTag === myPlayerTag ? 'rating-me' : ''}`}
              onClick={() => onOpen(p.playerTag)}
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
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}
