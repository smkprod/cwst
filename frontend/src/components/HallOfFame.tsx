import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { AppConfig, BackgroundKey, HallClan, HallOfFame as Hall, HallPlayer } from '../types'
import { fmt } from '../lib/format'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { ClanPageView, PlayerPageView, type HallTarget } from './HallPages'

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
 * человек, место на Аллее — весь сервис. Ради этого и покупают оформление.
 */
export function HallOfFame({ config, onConfigChanged }: {
  config: AppConfig | null
  onConfigChanged: () => void
}) {
  const { t } = useT()
  const [board, setBoard] = useState<Board>('players')
  const [data, setData] = useState<Hall | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'empty' | 'error'>('loading')
  const [pickerOpen, setPickerOpen] = useState(false)

  // Страница открывается поверх Аллеи, а не рядом с ней: список остаётся
  // смонтированным, поэтому возврат не перезагружает сезон заново.
  const [target, setTarget] = useState<HallTarget | null>(null)
  const openClan = (clanId: number) => { haptic('light'); setTarget({ kind: 'clan', clanId }) }
  const openPlayer = (playerTag: string) => { haptic('light'); setTarget({ kind: 'player', playerTag }) }
  const openRow = (r: HallPlayer | HallClan) =>
    'playerTag' in r ? openPlayer(r.playerTag) : openClan(r.clanId)

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

  // Страницы отвечают раньше состояний самой Аллеи: на них попадают и тогда,
  // когда список ещё грузится после возврата.
  if (target?.kind === 'clan') {
    return (
      <ClanPageView
        clanId={target.clanId}
        onOpenPlayer={openPlayer}
        onBack={() => setTarget(null)}
      />
    )
  }
  if (target?.kind === 'player') {
    return (
      <PlayerPageView
        playerTag={target.playerTag}
        onOpenClan={openClan}
        onBack={() => setTarget(null)}
      />
    )
  }

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
  const isSponsor = config?.isSponsor ?? false

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

      <Podium rows={top3} label={t.hall.season} seasonId={data.seasonId} onOpen={openRow} />

      {/* Своя строка сразу под подиумом.
          Внизу списка её не видит никто: до сотого места долистывают единицы,
          а своё место смотрят все и смотрят первым делом — прямо после чужих
          красивых фонов на подиуме. Здесь же и единственный призыв к покупке. */}
      {board === 'players' && data.me && (
        <MyRow
          me={data.me}
          total={data.totalPlayers}
          isSponsor={isSponsor}
          contact={config?.sponsorContact ?? ''}
          onCustomize={() => { haptic('medium'); setPickerOpen(true) }}
          t={t}
        />
      )}

      <ul className="hall-list">
        {rest.map(r => (
          <Row key={rowKey(r)} row={r} board={board} onPick={() => openRow(r)} />
        ))}
      </ul>

      {pickerOpen && config && (
        <BackgroundPicker
          config={config}
          onClose={() => setPickerOpen(false)}
          onSaved={onConfigChanged}
          t={t}
        />
      )}
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
function Podium({ rows, label, seasonId, onOpen }: {
  rows: (HallPlayer | HallClan)[]; label: string; seasonId: number
  onOpen: (row: HallPlayer | HallClan) => void
}) {
  if (rows.length === 0) return null

  // Порядок на подиуме: второй, первый, третий — как на настоящем пьедестале.
  const order = [rows[1], rows[0], rows[2]].filter(Boolean)
  const anyOwn = order.some(r => r.backgroundKey)

  // Своя полоса под каждой ступенью, а не одна картинка на весь подиум.
  //
  // Одна картинка бралась у первого места, и второй с третьим не видели своего
  // фона вообще: двое из трёх платили и не получали ничего. Полосами видно всех
  // троих, а вертикальным видам клана узкая колонка подходит даже лучше широкой
  // сцены — пропорции почти совпадают.
  //
  // Небесный оставлен сцене: выбрать его нельзя, поэтому он появляется ровно
  // тогда, когда ни у кого из тройки своего фона нет.
  return (
    <section
      className={`hall-podium ${anyOwn ? 'hall-podium-bands' : ''}`}
      style={anyOwn ? undefined : { backgroundImage: 'url(/bg/sky.webp)' }}
    >
      <div className="hall-podium-veil" />
      <div className="hall-podium-head">
        <span className="hall-podium-title">🏛 {label}</span>
        <span className="hall-podium-season">#{seasonId}</span>
      </div>

      <div className="hall-podium-row">
        {/* Ступень — кнопка. Пока ею не была, первые три строки списка оставались
            единственными, по которым нельзя нажать: они не лежат в списке, они
            стоят здесь. То есть закрыты были ровно верхние клан и игрок. */}
        {order.map(r => (
          <button
            key={rowKey(r)}
            className={`hall-step hall-step-${r.rank} ${r.backgroundKey ? 'hall-step-bg' : ''}`}
            style={r.backgroundKey ? { backgroundImage: `url(/bg/${r.backgroundKey}.webp)` } : undefined}
            onClick={() => onOpen(r)}
          >
            {r.backgroundKey && <span className="hall-step-veil" />}
            <span className="hall-medal">{r.rank === 1 ? '🥇' : r.rank === 2 ? '🥈' : '🥉'}</span>
            <span className="hall-step-name">
              {displayName(r)}
              <Marks row={r} />
            </span>
            <span className="hall-step-fame">{fmt(r.seasonFame)}</span>
            <div className="hall-step-block" />
          </button>
        ))}
      </div>
    </section>
  )
}

/** Своя строка: место, медали и единственная кнопка покупки на всём экране. */
function MyRow({ me, total, isSponsor, contact, onCustomize, t }: {
  me: HallPlayer; total: number; isSponsor: boolean; contact: string
  onCustomize: () => void; t: Translations
}) {
  return (
    <section
      className={`hall-me ${me.backgroundKey ? 'hall-me-bg' : ''}`}
      style={me.backgroundKey ? { backgroundImage: `url(/bg/${me.backgroundKey}.webp)` } : undefined}
    >
      {me.backgroundKey && <span className="hall-row-veil" />}
      <div className="hall-me-body">
        <span className="hall-me-label muted small">{t.hall.yourPlace}</span>
        <span className="hall-me-rank">
          #{me.rank} <span className="hall-me-of muted small">{t.hall.outOf} {total}</span>
          <Marks row={me} />
        </span>
        <span className="hall-me-fame">{fmt(me.seasonFame)}</span>
      </div>

      {isSponsor ? (
        <button className="btn-mini hall-me-btn" onClick={onCustomize}>{t.hall.customize}</button>
      ) : contact ? (
        <a
          className="btn-mini btn-mini-primary hall-me-btn"
          href={`https://t.me/${contact}`}
          target="_blank"
          rel="noreferrer"
          onClick={() => haptic('medium')}
        >
          {t.hall.standOut}
        </a>
      ) : null}
    </section>
  )
}

function Row({ row, board, onPick }: {
  row: HallPlayer | HallClan; board: Board; onPick?: () => void
}) {
  const inner = (
    <>
      {row.backgroundKey && <span className="hall-row-veil" />}
      <span className="hall-rank">#{row.rank}</span>
      <span className="hall-name">
        {displayName(row)}
        <Marks row={row} />
      </span>
      <span className="hall-sub muted small">{subtitle(row, board)}</span>
      <span className="hall-fame">{fmt(row.seasonFame)}</span>
    </>
  )

  const cls = `hall-row ${row.backgroundKey ? 'hall-row-bg' : ''}`
  const style = row.backgroundKey ? { backgroundImage: `url(/bg/${row.backgroundKey}.webp)` } : undefined

  // Нажимаются теперь обе таблицы: у клана открывается его страница, у игрока —
  // его. Раньше кнопкой была только строка клана, и то потому, что на ней висело
  // «написать»; страницы сняли этот перекос — смотреть есть на что у обоих.
  return onPick
    ? <li><button className={`${cls} hall-row-btn`} style={style} onClick={onPick}>{inner}</button></li>
    : <li className={cls} style={style}>{inner}</li>
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

/** Выбор фона: себе из одного набора, клану из другого. */
function BackgroundPicker({ config, onClose, onSaved, t }: {
  config: AppConfig; onClose: () => void; onSaved: () => void; t: Translations
}) {
  const [mine, setMine] = useState<BackgroundKey | null>(config.myBackground)
  const [clan, setClan] = useState<BackgroundKey | null>(config.myClanBackground)
  const [busy, setBusy] = useState(false)

  const pick = async (key: BackgroundKey | null, scope: 'player' | 'clan') => {
    haptic('light')
    setBusy(true)
    try {
      await api.setMyBackground(key, scope)
      if (scope === 'player') setMine(key)
      else setClan(key)
      hapticNotify('success')
      onSaved()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="card modal-card" onClick={e => e.stopPropagation()}>
        <div className="card-title">{t.hall.bgTitle}</div>

        <p className="adm-block-title">{t.hall.bgMine}</p>
        <div className="bg-grid">
          {config.playerBackgrounds.map(k => (
            <button
              key={k}
              className={`bg-tile ${mine === k ? 'bg-tile-on' : ''}`}
              style={{ backgroundImage: `url(/bg/${k}.webp)` }}
              disabled={busy}
              onClick={() => pick(mine === k ? null : k, 'player')}
              aria-label={k}
            />
          ))}
        </div>

        <p className="adm-block-title">{t.hall.bgClan}</p>
        <p className="muted small">{t.hall.bgClanHint}</p>
        <div className="bg-grid">
          {config.clanBackgrounds.map(k => (
            <button
              key={k}
              className={`bg-tile bg-tile-tall ${clan === k ? 'bg-tile-on' : ''}`}
              style={{ backgroundImage: `url(/bg/${k}.webp)` }}
              disabled={busy}
              onClick={() => pick(clan === k ? null : k, 'clan')}
              aria-label={k}
            />
          ))}
        </div>

        <button className="btn" onClick={onClose}>{t.hall.close}</button>
      </div>
    </div>
  )
}

const rowKey = (r: HallPlayer | HallClan) => ('playerTag' in r ? r.playerTag : `c${r.clanId}`)
const displayName = (r: HallPlayer | HallClan) => ('playerTag' in r ? r.name : r.clanName)

function subtitle(r: HallPlayer | HallClan, board: Board): string {
  if (board === 'players' && 'clanName' in r) return r.clanName
  return 'clanTag' in r ? (r.clanTag ?? '') : (r as HallClan).clanTag
}
