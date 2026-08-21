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

type SortKey = 'status' | 'fame' | 'trophies' | 'role' | 'name'
type FilterKey = 'all' | 'notPlayed' | 'played' | 'officers'

/** Старшинство для сортировки по роли: лидер один, дальше по убыванию прав. */
const ROLE_RANK: Record<string, number> = { leader: 0, coLeader: 1, elder: 2 }
const roleRank = (r?: string) => (r ? ROLE_RANK[r] ?? 3 : 3)

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
}

export function PlayerList({ players, myPlayerTag }: Props) {
  const [selected, setSelected] = useState<PlayerStatus | null>(null)
  const [sort, setSort] = useState<SortKey>('status')
  const [filter, setFilter] = useState<FilterKey>('all')
  const { t } = useT()

  if (players.length === 0) {
    return <p className="center muted">{t.players.empty}</p>
  }

  const open = (p: PlayerStatus) => {
    haptic('light')
    setSelected(p)
  }

  // Кубки приходят из состава клана; если он не отдался, они нулевые —
  // тогда сортировать по ним нечего и вариант прячем, чтобы не обманывать.
  const hasTrophies = players.some(p => p.trophies > 0)

  const shown = players
    .filter(p =>
      filter === 'all' ? true
        : filter === 'notPlayed' ? p.status !== 'played'
        : filter === 'played' ? p.status === 'played'
        : roleRank(p.role) < 3)   // officers: лидер, соруки, старейшины
    .slice()
    .sort((a, b) => {
      switch (sort) {
        case 'fame': return b.fame - a.fame
        case 'trophies': return b.trophies - a.trophies
        case 'role': return roleRank(a.role) - roleRank(b.role) || b.fame - a.fame
        case 'name': return a.name.localeCompare(b.name)
        default:
          // По умолчанию — сначала те, кого надо пинать
          return (a.status === 'played' ? 1 : 0) - (b.status === 'played' ? 1 : 0)
            || b.fame - a.fame
      }
    })

  return (
    <>
      <div className="pl-controls">
        <select
          className="rating-select"
          value={sort}
          onChange={e => { haptic('light'); setSort(e.target.value as SortKey) }}
          aria-label={t.players.sortLabel}
        >
          <option value="status">{t.players.sortStatus}</option>
          <option value="fame">{t.players.sortFame}</option>
          {hasTrophies && <option value="trophies">{t.players.sortTrophies}</option>}
          <option value="role">{t.players.sortRole}</option>
          <option value="name">{t.players.sortName}</option>
        </select>

        <select
          className="rating-select"
          value={filter}
          onChange={e => { haptic('light'); setFilter(e.target.value as FilterKey) }}
          aria-label={t.players.filterLabel}
        >
          <option value="all">{t.players.filterAll} ({players.length})</option>
          <option value="notPlayed">{t.players.filterNotPlayed}</option>
          <option value="played">{t.players.filterPlayed}</option>
          <option value="officers">{t.players.filterOfficers}</option>
        </select>
      </div>

      {shown.length === 0 && <p className="center muted">{t.players.emptyFiltered}</p>}

      <ul className="player-list">
        {shown.map(p => {
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
