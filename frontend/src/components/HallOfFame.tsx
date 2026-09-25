import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { HallClan, HallOfFame as Hall, HallPlayer } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

type Board = 'players' | 'clans'

/** Значки витрины — те же иконки, что в карточке наград: один значок, один смысл. */
const BADGE_ICONS: Record<string, string> = {
  streak: '🔥',
  dailyStreak: '📆',
  perfectDays: '💯',
  mvpWeeks: '👑',
  totalFame: '🏅',
  warsPlayed: '⚔️',
  perfectWeeks: '💎',
  perfectSeasons: '🏆',
  boatAttacks: '🚤',
}

const LEVEL_CLS = ['', 'badge-bronze', 'badge-silver', 'badge-gold']

/**
 * Аллея славы — единственный экран, где игрока видят люди не из его клана.
 *
 * Поэтому подиум здесь не украшение, а суть: место в своём клане видят полсотни
 * человек, место на Аллее — весь сервис. Ради этого и покупают фон.
 */
export function HallOfFame({ sponsorContact }: { sponsorContact: string }) {
  const { t } = useT()
  const [board, setBoard] = useState<Board>('players')
  const [data, setData] = useState<Hall | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'empty' | 'error'>('loading')

  useEffect(() => {
    let alive = true
    api.getHallOfFame()
      .then(d => {
        if (!alive) return
        setData(d)
        setState(d ? 'ready' : 'empty')
      })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [])

  if (state === 'loading') return <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
  if (state === 'error') return <p className="center muted" style={{ marginTop: 16 }}>{t.hall.error}</p>
  if (state === 'empty' || !data) {
    return (
      <section className="card fade-in">
        <div className="card-title">{t.hall.title}</div>
        <p className="muted small">{t.hall.collecting}</p>
      </section>
    )
  }

  const rows: (HallPlayer | HallClan)[] = board === 'players' ? data.players : data.clans
  const top3 = rows.slice(0, 3)
  const rest = rows.slice(3)

  return (
    <div className="fade-in">
      <div className="hall-switch">
        <button
          className={`chart-opt ${board === 'players' ? 'chart-opt-on' : ''}`}
          onClick={() => { haptic('light'); setBoard('players') }}
        >{t.hall.players}</button>
        <button
          className={`chart-opt ${board === 'clans' ? 'chart-opt-on' : ''}`}
          onClick={() => { haptic('light'); setBoard('clans') }}
        >{t.hall.clans}</button>
      </div>

      <Podium rows={top3} board={board} label={t.hall.season} seasonId={data.seasonId} />

      <ul className="hall-list">
        {rest.map(r => <Row key={rowKey(r)} row={r} board={board} />)}
      </ul>

      {sponsorContact && (
        <a
          className="btn hall-sponsor-btn"
          href={`https://t.me/${sponsorContact}`}
          target="_blank"
          rel="noreferrer"
          onClick={() => haptic('medium')}
        >
          {t.hall.becomeSponsor}
        </a>
      )}
      <p className="muted small hall-sponsor-hint">{t.hall.sponsorHint}</p>
    </div>
  )
}

/**
 * Подиум на фоне.
 *
 * Фон берём у первого места: это его витрина, и она же — то, что видят все
 * остальные. Нет фона — рисуем небесный по умолчанию, чтобы экран не выглядел
 * недоделанным у клана без спонсора.
 */
function Podium({ rows, board, label, seasonId }: {
  rows: (HallPlayer | HallClan)[]; board: Board; label: string; seasonId: number
}) {
  if (rows.length === 0) return null

  const bg = rows.find(r => r.backgroundKey)?.backgroundKey ?? 'sky'
  // Порядок на подиуме: второй, первый, третий — как на настоящем пьедестале.
  const order = [rows[1], rows[0], rows[2]].filter(Boolean)

  return (
    <section
      className="hall-podium"
      style={{ backgroundImage: `url(/bg/${bg}.webp)` }}
    >
      <div className="hall-podium-veil" />
      <div className="hall-podium-head">
        <span className="hall-podium-title">🏛 {label}</span>
        <span className="hall-podium-season">#{seasonId}</span>
      </div>

      <div className="hall-podium-row">
        {order.map(r => (
          <div key={rowKey(r)} className={`hall-step hall-step-${r.rank}`}>
            <span className="hall-medal">{r.rank === 1 ? '🥇' : r.rank === 2 ? '🥈' : '🥉'}</span>
            <span className="hall-step-name">
              {displayName(r)}
              <Marks row={r} />
            </span>
            <span className="hall-step-fame">{fmt(r.seasonFame)}</span>
            <div className="hall-step-block" />
          </div>
        ))}
      </div>
    </section>
  )
}

function Row({ row, board }: { row: HallPlayer | HallClan; board: Board }) {
  return (
    <li
      className={`hall-row ${row.backgroundKey ? 'hall-row-bg' : ''}`}
      style={row.backgroundKey ? { backgroundImage: `url(/bg/${row.backgroundKey}.webp)` } : undefined}
    >
      {/* Затемнение поверх фона: без него белое имя на облаках не читается */}
      {row.backgroundKey && <span className="hall-row-veil" />}
      <span className="hall-rank">#{row.rank}</span>
      <span className="hall-name">
        {displayName(row)}
        <Marks row={row} />
      </span>
      <span className="hall-sub muted small">{subtitle(row, board)}</span>
      <span className="hall-fame">{fmt(row.seasonFame)}</span>
    </li>
  )
}

/** Ярлыки рядом с именем: значок витрины и спонсорство. */
function Marks({ row }: { row: HallPlayer | HallClan }) {
  const player = 'playerTag' in row ? row : null
  const clan = 'clanId' in row ? row : null

  return (
    <>
      {player?.badgeKey && player.badgeLevel > 0 && (
        <span className={`hall-badge ${LEVEL_CLS[player.badgeLevel] ?? ''}`}>
          {BADGE_ICONS[player.badgeKey] ?? '🏅'}
        </span>
      )}
      {player?.isSponsor && <span className="hall-sponsor-tag">★</span>}
      {clan && clan.sponsorCount > 0 && <span className="hall-sponsor-tag">★</span>}
    </>
  )
}

const rowKey = (r: HallPlayer | HallClan) => ('playerTag' in r ? r.playerTag : `c${r.clanId}`)
const displayName = (r: HallPlayer | HallClan) => ('playerTag' in r ? r.name : r.clanName)

function subtitle(r: HallPlayer | HallClan, board: Board): string {
  if (board === 'players' && 'clanName' in r) return r.clanName
  return 'clanTag' in r ? (r.clanTag ?? '') : (r as HallClan).clanTag
}
