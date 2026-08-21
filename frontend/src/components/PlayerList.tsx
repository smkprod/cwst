import { useState } from 'react'
import type { PlayerStatus, PlayStatus } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, roleLabel } from '../lib/i18n'
import { PlayerInfoModal } from './PlayerInfoModal'

const STATUS_META: Record<PlayStatus, { icon: string; cls: string }> = {
  played: { icon: '✅', cls: 'row-played' },
  timeLeft: { icon: '⏳', cls: 'row-timeleft' },
  notPlayed: { icon: '❌', cls: 'row-notplayed' },
}

const ROLE_ICON: Record<string, string> = {
  leader: '👑',
  coLeader: '⚜️',
  elder: '⭐',
}

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
}

export function PlayerList({ players, myPlayerTag }: Props) {
  const [selected, setSelected] = useState<PlayerStatus | null>(null)
  const { t } = useT()

  if (players.length === 0) {
    return <p className="center muted">{t.players.empty}</p>
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
          const roleName = roleLabel(p.role, t)
          return (
            <li key={p.playerTag} className={`player-card ${meta.cls} ${isMe ? 'player-me' : ''}`}>
              <button className="player-row" onClick={() => open(p)}>
                <span className="status-icon" aria-label={p.status}>{meta.icon}</span>
                <div className="player-info">
                  <span className="player-name">
                    {isMe && <span className="me-badge">{t.leaderboard.you}</span>}
                    {/* Обрезается только само имя: значки рядом не должны съедаться многоточием */}
                    <span className="player-name-text">{p.name}</span>
                    {!p.isLinked && <span className="unlinked">{t.players.noTg}</span>}
                  </span>
                  {/* Отдельными элементами, чтобы строка переносилась, а не вылезала за карточку */}
                  <span className="player-fame">
                    {roleName && (
                      <span className={`role-badge role-${p.role}`}>
                        {ROLE_ICON[p.role!]} {roleName}
                      </span>
                    )}
                    <span>#{p.rank}</span>
                    <span>{fmt(p.fame)} 🏅</span>
                    {p.warDecksUsed > 0 && <span>{p.warDecksUsed} атак</span>}
                  </span>
                </div>
                <DeckDots used={p.decksUsedToday} label={t.players.decksDots} />
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

function DeckDots({ used, label }: { used: number; label: string }) {
  return (
    <div className="deck-dots" aria-label={`${used} ${label}`}>
      {[0, 1, 2, 3].map(i => (
        <span key={i} className={`dot ${i < used ? 'dot-used' : ''}`} />
      ))}
      <span className="deck-count">{used}/4</span>
    </div>
  )
}
