import { useState } from 'react'
import type { PlayerStatus, PlayStatus } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { PlayerInfoModal } from './PlayerInfoModal'

const STATUS_META: Record<PlayStatus, { icon: string; cls: string }> = {
  played: { icon: '✅', cls: 'row-played' },
  timeLeft: { icon: '⏳', cls: 'row-timeleft' },
  notPlayed: { icon: '❌', cls: 'row-notplayed' },
}

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
}

export function PlayerList({ players, myPlayerTag }: Props) {
  const [selected, setSelected] = useState<PlayerStatus | null>(null)

  if (players.length === 0) {
    return <p className="center muted">Нет участников войны — возможно, идёт тренировка.</p>
  }

  const open = (p: PlayerStatus) => {
    haptic('light')
    setSelected(p)
  }

  return (
    <>
      <ul className="player-list">
        {players.map(p => {
          const meta = STATUS_META[p.status]
          const isMe = p.playerTag === myPlayerTag
          return (
            <li key={p.playerTag} className={`player-card ${meta.cls} ${isMe ? 'player-me' : ''}`}>
              <button className="player-row" onClick={() => open(p)}>
                <span className="status-icon" aria-label={p.status}>{meta.icon}</span>
                <div className="player-info">
                  <span className="player-name">
                    {isMe && <span className="me-badge">ты</span>}
                    {p.role && <span className="role-badge">{p.role}</span>}
                    {p.name}
                    {!p.isLinked && <span className="unlinked" title="Не привязан к Telegram"> · нет TG</span>}
                  </span>
                  <span className="player-fame">
                    #{p.rank} · {fmt(p.fame)} 🏅
                    {p.warDecksUsed > 0 && <span className="muted"> · {p.warDecksUsed} атак</span>}
                  </span>
                </div>
                <DeckDots used={p.decksUsedToday} />
                <span className="chevron">›</span>
              </button>
            </li>
          )
        })}
      </ul>

      {selected && (
        <PlayerInfoModal
          player={selected}
          isMe={selected.playerTag === myPlayerTag}
          onClose={() => setSelected(null)}
        />
      )}
    </>
  )
}

/** 4 точки = 4 колоды дня. Заполненные — сыгранные. */
function DeckDots({ used }: { used: number }) {
  return (
    <div className="deck-dots" aria-label={`${used} из 4 колод`}>
      {[0, 1, 2, 3].map(i => (
        <span key={i} className={`dot ${i < used ? 'dot-used' : ''}`} />
      ))}
      <span className="deck-count">{used}/4</span>
    </div>
  )
}
