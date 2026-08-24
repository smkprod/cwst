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

/**
 * Градиент на карточке — за заслугу, а не для красоты.
 *
 * Если подсветить всех, подсветка перестаёт что-либо значить: пятьдесят цветных
 * строк читаются как одна серая. Поэтому фон достаётся только трём случаям, а
 * рядовой состав остаётся на обычном фоне — тогда выделенных видно с первого взгляда.
 *
 * Король недели идёт первым по приоритету: лидера и так подписывает значок роли,
 * а первое место по медалям иначе не увидеть — оно меняется от недели к неделе.
 */
function tierClass(p: PlayerStatus, kingTag?: string): string {
  if (kingTag !== undefined && p.playerTag === kingTag) return 'pc-king'
  if (p.role === 'leader') return 'pc-gold'
  if (p.role === 'coLeader') return 'pc-silver'
  return ''
}

interface Props {
  players: PlayerStatus[]
  myPlayerTag?: string
  /** Первый по медалям недели — единственный, кто получает королевский фон. */
  kingTag?: string
}

export function PlayerList({ players, myPlayerTag, kingTag }: Props) {
  const [selected, setSelected] = useState<PlayerStatus | null>(null)
  const [sort, setSort] = useState<SortKey>('status')
  const [filter, setFilter] = useState<FilterKey>('all')
  const [query, setQuery] = useState('')
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

  // Ищем без учёта регистра: в CR имена пишут как угодно, и «Vasya» не должен
  // прятаться от запроса «vasya».
  const needle = query.trim().toLowerCase()

  const shown = players
    .filter(p =>
      filter === 'all' ? true
        : filter === 'notPlayed' ? p.status !== 'played'
        : filter === 'played' ? p.status === 'played'
        : roleRank(p.role) < 3)   // officers: лидер, соруки, старейшины
    .filter(p => needle === '' || p.name.toLowerCase().includes(needle))
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
      {/* Поиск по имени: в клане до 50 человек, и найти одного пролистыванием —
          отдельное занятие. Поле стоит выше фильтров: чаще всего ищут конкретного. */}
      <div className="pl-search">
        <input
          className="pl-search-input"
          type="search"
          value={query}
          placeholder={t.players.searchPlaceholder}
          onChange={e => setQuery(e.target.value)}
          aria-label={t.players.searchPlaceholder}
        />
        {needle !== '' && (
          <span className="muted small pl-search-count">
            {shown.length} {t.players.countSuffix} {players.length}
          </span>
        )}
      </div>

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
          const tier = tierClass(p, kingTag)
          return (
            <li key={p.playerTag} className={`player-card ${meta.cls} ${tier} ${isMe ? 'player-me' : ''}`}>
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
                    {/* Подпись к градиенту короля: иначе цвет читается как украшение,
                        а не как «этот человек первый по медалям» */}
                    {tier === 'pc-king' && <span className="king-badge">{t.players.kingOfWeek}</span>}
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
