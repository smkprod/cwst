import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { MetaCard, PlayerHistory, PlayerProfile, PlayerSheet, PlayerStatus, PlayStatus, SheetBattle } from '../types'
import { fmt } from '../lib/format'
import { copyText, haptic, hapticNotify, openExternalLink, shareToTelegram } from '../lib/telegram'
import { useT, roleLabel, type Translations } from '../lib/i18n'
import { SponsorMarks } from '../lib/sponsorMarks'
import { AchievementsCard } from './AchievementsCard'
import { ClanModal } from './ClanModal'
import { CardIcon } from './MetaDecksView'
import { PlayerProfileCard } from './PlayerProfileCard'
import { DuelSheetBadge } from './duel/DuelSheetBadge'

export type SheetTab = 'overview' | 'war' | 'battles' | 'cards'

const ROLE_ICON: Record<string, string> = { leader: '👑', coLeader: '⚜️', elder: '⭐' }

interface BodyProps {
  tag: string
  warRow?: PlayerStatus
  isMe?: boolean
  canManage?: boolean
  tab?: SheetTab
  /** Открыть карточку другого игрока — соперника из журнала боёв. */
  onOpenPlayer?: (tag: string) => void
  /** Есть — рисуем крестик в шапке. */
  onClose?: () => void
  /** Уже загруженная карточка (поиск): второй раз не запрашиваем. */
  preloaded?: PlayerSheet
}

/**
 * Единая карточка игрока — одна на весь бот.
 *
 * Шапка отвечает на «кто это» одним взглядом: ник, клан, фон спонсора, рейтинг,
 * кубки и место в мире. Всё остальное разложено по вкладкам, а не простынёй:
 * в КВ смотрят медали, в поиске — колоду и бои, и мешать одно другому незачем.
 */
export function PlayerSheetModal(props: BodyProps & { onClose: () => void }) {
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  const close = () => { haptic('light'); props.onClose() }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet psheet-modal fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />
        <PlayerSheetBody {...props} onClose={close} />
      </div>
    </div>
  )
}

export function PlayerSheetBody({ tag, warRow, isMe = false, canManage = false, tab: initialTab, onOpenPlayer, onClose, preloaded }: BodyProps) {
  const { t } = useT()
  const [data, setData] = useState<PlayerSheet | null>(preloaded ?? null)
  const [state, setState] = useState<'loading' | 'ready' | 'notFound' | 'error'>(preloaded ? 'ready' : 'loading')
  const [tab, setTab] = useState<SheetTab>(initialTab ?? (warRow ? 'war' : 'overview'))
  const [clanOpen, setClanOpen] = useState(false)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (preloaded && preloaded.playerTag.toUpperCase() === tag.toUpperCase()) return
    let alive = true
    setState('loading')
    api.getPlayerSheet(tag)
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(e => {
        if (!alive) return
        setState(e instanceof ApiError && e.status === 404 ? 'notFound' : 'error')
      })
    return () => { alive = false }
  }, [tag, preloaded])

  const s = t.sheet
  const copyTag = async () => {
    haptic('light')
    const ok = await copyText(data?.playerTag ?? tag)
    hapticNotify(ok ? 'success' : 'error')
    if (ok) { setCopied(true); setTimeout(() => setCopied(false), 1500) }
  }

  const name = data?.name ?? warRow?.name ?? '…'
  const role = data?.role ?? warRow?.role ?? null
  const roleName = roleLabel(role ?? undefined, t)
  const bg = data?.backgroundKey ?? warRow?.backgroundKey ?? null

  const switchTab = (next: SheetTab) => { haptic('light'); setTab(next) }

  return (
    <div className="psheet">
      {/* Шапка: фон спонсора на всю ширину, поверх — кто это и чем знаменит */}
      <div
        className={`psheet-hero ${bg ? 'psheet-hero-bg' : ''}`}
        style={bg ? { backgroundImage: `url(/bg/${bg}.webp)` } : undefined}
      >
        {bg && <span className="psheet-hero-veil" />}
        <div className="psheet-hero-top">
          <div className="psheet-id">
            <h3 className="psheet-name">
              {isMe && <span className="me-badge">{t.leaderboard.you}</span>}
              {name}
              <SponsorMarks of={{
                isSponsor: data?.isSponsor ?? warRow?.isSponsor,
                badgeKey: data?.badgeKey ?? warRow?.badgeKey,
                badgeLevel: data?.badgeLevel ?? warRow?.badgeLevel,
              }} />
            </h3>
            <button type="button" className={`tag-copy psheet-tag ${copied ? 'tag-copy-done' : ''}`} onClick={copyTag}>
              {data?.playerTag ?? tag}
              <span className="tag-copy-icon">{copied ? '✓' : '⧉'}</span>
            </button>
          </div>
          {onClose && <button className="modal-close" onClick={onClose} aria-label={t.playerModal.close}>✕</button>}
        </div>

        <div className="psheet-chips">
          {data?.clanName && (
            <button
              type="button"
              className="psheet-chip psheet-chip-clan"
              onClick={() => { haptic('light'); if (data.clanTag) setClanOpen(true) }}
            >
              🛡 {data.clanName}{roleName ? ` · ${role ? ROLE_ICON[role] ?? '' : ''} ${roleName}` : ''} ›
            </button>
          )}
          {data?.worldRank && <span className="psheet-chip psheet-chip-gold">🌍 #{data.worldRank} {s.worldRank}</span>}
          {(data?.isSponsor ?? warRow?.isSponsor) && <span className="psheet-chip psheet-chip-gold">★ {s.sponsor}</span>}
          {data?.inBot && <span className="psheet-chip">🤖 {s.inBot}</span>}
        </div>

        {data && (
          <div className="psheet-hero-stats">
            <HeroStat
              value={data.rating ? fmt(data.rating) : '—'}
              label={s.rating}
              sub={data.ratingLeague ? `${s.league} ${data.ratingLeague}${data.ratingRank ? ` · #${data.ratingRank}` : ''}` : undefined}
              accent
            />
            <HeroStat value={fmt(data.trophies)} label={`🏆 ${s.trophies}`} />
            <HeroStat value={fmt(data.bestTrophies)} label={s.best} />
          </div>
        )}
      </div>

      {state === 'loading' && <div className="center" style={{ padding: 24 }}><div className="spinner" /></div>}
      {state === 'notFound' && <p className="center muted">{s.notFound}</p>}
      {state === 'error' && <p className="center muted">{s.loadError}</p>}

      {state === 'ready' && data && (
        <>
          <div className="psheet-tabs">
            {(['overview', 'war', 'battles', 'cards'] as const).map(k => (
              <button
                key={k}
                className={`psheet-tab ${tab === k ? 'psheet-tab-on' : ''}`}
                onClick={() => switchTab(k)}
              >
                {k === 'overview' ? s.tabOverview : k === 'war' ? s.tabWar : k === 'battles' ? s.tabBattles : s.tabCards}
              </button>
            ))}
          </div>

          {tab === 'overview' && <Overview data={data} isMe={isMe} t={t} />}
          {tab === 'war' && <WarTab data={data} warRow={warRow} isMe={isMe} canManage={canManage} t={t} />}
          {tab === 'battles' && <BattlesTab data={data} onOpenPlayer={onOpenPlayer} t={t} />}
          {tab === 'cards' && <CardsTab tag={data.playerTag} t={t} />}

          <button className="btn-mini psheet-royaleapi" onClick={() => openExternalLink(data.royaleApiUrl)}>
            {s.royaleApi}
          </button>
        </>
      )}

      {clanOpen && data?.clanTag && (
        <ClanModal tag={data.clanTag} name={data.clanName ?? undefined} onClose={() => setClanOpen(false)} />
      )}
    </div>
  )
}

function HeroStat({ value, label, sub, accent }: { value: string; label: string; sub?: string; accent?: boolean }) {
  return (
    <div className={`psheet-hero-stat ${accent ? 'psheet-hero-stat-accent' : ''}`}>
      <span className="psheet-hero-value">{value}</span>
      <span className="psheet-hero-label">{label}</span>
      {sub && <span className="psheet-hero-sub">{sub}</span>}
    </div>
  )
}

function Stat({ value, label, accent }: { value: string; label: string; accent?: boolean }) {
  return (
    <div className={`modal-stat ${accent ? 'modal-stat-accent' : ''}`}>
      <span className="modal-stat-value">{value}</span>
      <span className="modal-stat-label">{label}</span>
    </div>
  )
}

function DeckRow({ cards }: { cards: MetaCard[] }) {
  return (
    <div className="wtop-deck">
      {cards.map((c, i) => <CardIcon key={`${c.cardId}-${i}`} card={c} />)}
    </div>
  )
}

/** Обзор: колода, карьера и то, что про игрока знает бот. */
function Overview({ data, isMe, t }: { data: PlayerSheet; isMe: boolean; t: Translations }) {
  const s = t.sheet
  const decided = data.wins + data.losses
  const winRate = decided > 0 ? Math.round((data.wins / decided) * 1000) / 10 : 0

  return (
    <div className="fade-in">
      {data.duel && <DuelSheetBadge rank={data.duel} t={t} />}

      {data.deck.length > 0 && (
        <section className="psheet-block">
          <div className="psheet-block-head">
            <span className="psheet-block-title">{s.deck}</span>
            <span className="muted small">💧{data.deckElixir} · {s.deckFromBattle}</span>
          </div>
          <DeckRow cards={data.deck} />
          {data.deckLink && (
            <a className="btn-mini mdeck-open" href={data.deckLink} target="_blank" rel="noreferrer"
               onClick={() => haptic('medium')}>
              {s.openInGame}
            </a>
          )}
        </section>
      )}

      {data.games30 > 0 && (
        <p className="psheet-note">📈 {s.last30.replace('{x}', String(data.winPercent30)).replace('{n}', String(data.games30))}</p>
      )}

      <div className="modal-grid">
        <Stat value={`${winRate}%`} label={s.winRate} accent />
        <Stat value={fmt(data.wins)} label={s.wins} />
        <Stat value={fmt(data.losses)} label={s.losses} />
        <Stat value={fmt(data.threeCrownWins)} label={s.threeCrowns} />
        <Stat value={String(data.expLevel)} label={s.level} />
        <Stat value={fmt(data.warDayWins)} label={s.warDayWins} />
        {data.bestRating ? <Stat value={fmt(data.bestRating)} label={s.bestRating} /> : null}
        {data.clanWarTrophies > 0 ? <Stat value={fmt(data.clanWarTrophies)} label={s.clanWarTrophies} /> : null}
      </div>

      {(data.favouriteCard || data.currentStreak !== 0) && (
        <div className="psheet-facts">
          {data.favouriteCard && (
            <span className="psheet-fact">
              <CardIcon card={data.favouriteCard} />
              <span className="muted small">{s.favourite}<br /><b>{data.favouriteCard.name}</b></span>
            </span>
          )}
          {data.currentStreak !== 0 && (
            <span className={`psheet-fact psheet-streak ${data.currentStreak > 0 ? 'wtop-up' : 'wtop-down'}`}>
              <b>{data.currentStreak > 0 ? '🔥' : '🧊'} {Math.abs(data.currentStreak)}</b>
              <span className="muted small">{data.currentStreak > 0 ? s.streakWin : s.streakLoss}</span>
            </span>
          )}
        </div>
      )}

      {data.inBot && <Respect tag={data.playerTag} isMe={isMe} t={t} />}
      {data.inBot && <AchievementsCard playerTag={data.playerTag} compact />}
    </div>
  )
}

function Respect({ tag, isMe, t }: { tag: string; isMe: boolean; t: Translations }) {
  const [respect, setRespect] = useState<'loading' | 'idle' | 'sent' | 'used' | 'off'>('loading')

  useEffect(() => {
    if (isMe) return
    let alive = true
    api.getRespectStatus()
      .then(s => { if (alive) setRespect(s.givenToday ? 'used' : 'idle') })
      .catch(() => { if (alive) setRespect('off') })   // не привязан сам — давать нечем
    return () => { alive = false }
  }, [isMe])

  if (isMe || respect === 'loading' || respect === 'off') return null

  const send = async () => {
    haptic('medium')
    setRespect('sent')
    try { await api.giveRespect(tag) } catch { setRespect('used') }
  }

  return (
    <button
      className={`btn-respect ${respect === 'idle' ? '' : 'btn-respect-done'}`}
      onClick={send}
      disabled={respect !== 'idle'}
    >
      {respect === 'idle' && <>👏 {t.respect.give}</>}
      {respect === 'sent' && <>✅ {t.respect.sent}</>}
      {respect === 'used' && <>👏 {t.respect.usedToday}</>}
    </button>
  )
}

/**
 * КВ: неделя клана и прошлые войны. Строка недели приходит из списка, откуда
 * открыли карточку; если открыли из поиска или топа — берём живой статус клана.
 */
function WarTab({ data, warRow, isMe, canManage, t }: {
  data: PlayerSheet; warRow?: PlayerStatus; isMe: boolean; canManage: boolean; t: Translations
}) {
  const s = t.sheet
  const [row, setRow] = useState<PlayerStatus | null>(warRow ?? null)
  const [rowState, setRowState] = useState<'loading' | 'ready' | 'none'>(warRow ? 'ready' : 'loading')
  const [history, setHistory] = useState<PlayerHistory | null>(null)
  const [historyState, setHistoryState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    if (warRow) return
    if (!data.clanTag) { setRowState('none'); return }
    let alive = true
    api.getClanStatus(data.clanTag)
      .then(st => {
        if (!alive) return
        const found = st.players.find(p => p.playerTag.toUpperCase() === data.playerTag.toUpperCase())
        setRow(found ?? null)
        setRowState(found ? 'ready' : 'none')
      })
      .catch(() => { if (alive) setRowState('none') })
    return () => { alive = false }
  }, [warRow, data.clanTag, data.playerTag])

  useEffect(() => {
    let alive = true
    api.getPlayerHistory(data.playerTag)
      .then(h => { if (alive) { setHistory(h); setHistoryState('ready') } })
      .catch(() => { if (alive) setHistoryState('error') })
    return () => { alive = false }
  }, [data.playerTag])

  return (
    <div className="fade-in">
      <section className="psheet-block">
        <div className="psheet-block-head"><span className="psheet-block-title">{s.war}</span></div>
        {rowState === 'loading' && <p className="muted small">{s.warLoading}</p>}
        {rowState === 'none' && <p className="muted small">{s.warNone}</p>}
        {rowState === 'ready' && row && <WarWeek p={row} isMe={isMe} canManage={canManage} t={t} />}
      </section>

      <section className="psheet-block">
        <div className="psheet-block-head"><span className="psheet-block-title">{t.playerModal.pastWars}</span></div>
        {historyState === 'loading' && <p className="muted small">{t.playerModal.loadingHistory}</p>}
        {historyState === 'error' && <p className="muted small">{t.playerModal.historyError}</p>}
        {historyState === 'ready' && history && history.weeks.length === 0 && (
          <p className="muted small">{t.playerModal.noHistory}</p>
        )}
        {historyState === 'ready' && history && history.weeks.length > 0 && <WarHistory history={history} t={t} />}
      </section>
    </div>
  )
}

function WarWeek({ p, isMe, canManage, t }: { p: PlayerStatus; isMe: boolean; canManage: boolean; t: Translations }) {
  const s = t.sheet
  const STATUS: Record<PlayStatus, { icon: string; label: string; cls: string }> = {
    played: { icon: '✅', label: t.playerModal.played, cls: 'status-played' },
    timeLeft: { icon: '⏳', label: t.playerModal.timeLeft, cls: 'status-timeleft' },
    notPlayed: { icon: '❌', label: t.playerModal.notPlayed, cls: 'status-notplayed' },
  }
  const meta = STATUS[p.status]

  return (
    <>
      <div className={`modal-status ${meta.cls}`}>
        {meta.icon} {meta.label} · {s.todayDecks} <strong>{p.decksUsedToday}/4</strong>
        {' · '}#{p.rank} {s.rankInClan}
      </div>

      {canManage && !isMe && !p.isLinked && <Invite tag={p.playerTag} t={t} />}

      {p.dnaLabel && (
        <div className="dna-row">
          <span className="dna-chip">{p.dnaLabel}</span>
          {p.reliabilityScore > 0 && (
            <span className="dna-reliability">
              {t.playerModal.reliability}
              <span className="dna-track">
                <span
                  className={`dna-fill ${p.reliabilityScore >= 70 ? 'fill-good' : p.reliabilityScore >= 45 ? 'fill-mid' : 'fill-bad'}`}
                  style={{ width: `${p.reliabilityScore}%` }}
                />
              </span>
              <strong>{p.reliabilityScore}</strong>
            </span>
          )}
        </div>
      )}

      <div className="modal-grid">
        <Stat value={fmt(p.fame)} label={t.playerModal.weekMedals} accent />
        <Stat value={p.avgFamePerAttack > 0 ? String(Math.round(p.avgFamePerAttack)) : '—'} label={t.playerModal.avgAttack} />
        <Stat value={`${p.warDecksUsed}/16`} label={t.playerModal.totalAttacks} />
        <Stat value={p.boatAttacks > 0 ? String(p.boatAttacks) : '—'} label={t.playerModal.boatAttacks} />
        <Stat value={fmt(p.projectedDayFame)} label={t.playerModal.dayForecast} />
        <Stat value={fmt(p.projectedWeekFame)} label={t.playerModal.weekForecast} />
      </div>
      {p.repairPoints > 0 && (
        <p className="muted small modal-extra">{t.playerModal.repairPoints} {fmt(p.repairPoints)}</p>
      )}
    </>
  )
}

/** Прошлые войны столбиками: форму видно сразу, цифры — под пальцем. */
function WarHistory({ history, t }: { history: PlayerHistory; t: Translations }) {
  const weeks = history.weeks.slice(0, 12)
  const max = Math.max(1, ...weeks.map(w => w.fame))
  return (
    <>
      <div className="psheet-war-bars">
        {[...weeks].reverse().map(w => (
          <div key={`${w.seasonId}-${w.sectionIndex}-${w.clanTag}`} className="psheet-war-bar" title={`${w.clanName}: ${w.fame}`}>
            <span className="psheet-war-fill" style={{ height: `${Math.max(4, (w.fame / max) * 100)}%` }} />
          </div>
        ))}
      </div>
      <ul className="history-week-list">
        {weeks.map(w => (
          <li key={`${w.seasonId}-${w.sectionIndex}-${w.clanTag}`} className="history-week-row">
            <span className="history-week-badge">{w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}</span>
            <div className="history-week-info">
              <span className="history-week-clan">{w.clanName}</span>
              <span className="muted small">
                {t.playerModal.season} {w.seasonId} · ⚡ {w.avgFamePerAttack > 0 ? Math.round(w.avgFamePerAttack) : '—'} · {w.decksUsed}/16
              </span>
            </div>
            <span className="history-week-fame">{fmt(w.fame)} 🏅</span>
          </li>
        ))}
      </ul>
    </>
  )
}

function Invite({ tag, t }: { tag: string; t: Translations }) {
  const [state, setState] = useState<'idle' | 'loading' | 'error'>('idle')
  const [link, setLink] = useState<string | null>(null)

  const make = async () => {
    haptic('medium')
    setState('loading')
    try {
      const { link } = await api.getClaimLink(tag)
      setLink(link)
      setState('idle')
    } catch {
      setState('error')
    }
  }

  return (
    <div className="invite-box">
      {link === null ? (
        <>
          <button className="btn-invite" onClick={make} disabled={state === 'loading'}>
            🔗 {state === 'loading' ? t.playerModal.inviteLoading : t.playerModal.invite}
          </button>
          <p className="invite-note">{state === 'error' ? t.playerModal.inviteError : t.playerModal.inviteHint}</p>
        </>
      ) : (
        <>
          <div className="invite-link">{link}</div>
          <div className="invite-actions">
            <button className="btn-invite" onClick={() => { haptic('light'); shareToTelegram(t.playerModal.inviteShareText, link) }}>
              ➤ {t.playerModal.inviteSend}
            </button>
            <button className="btn-invite btn-invite-ghost" onClick={async () => {
              haptic('light')
              hapticNotify(await copyText(link) ? 'success' : 'error')
            }}>
              ⧉ {t.playerModal.inviteCopy}
            </button>
          </div>
          <p className="invite-note">{t.playerModal.inviteReady}</p>
        </>
      )}
    </div>
  )
}

/** Бои: компактная строка на бой, по тапу — обе колоды. */
function BattlesTab({ data, onOpenPlayer, t }: {
  data: PlayerSheet; onOpenPlayer?: (tag: string) => void; t: Translations
}) {
  const s = t.sheet
  const [open, setOpen] = useState<number | null>(0)

  if (data.battles.length === 0) return <p className="center muted small">{s.noBattles}</p>

  return (
    <div className="fade-in">
      <p className="muted small" style={{ margin: '0 0 8px' }}>{s.tapToExpand}</p>
      {data.battles.map((b, i) => (
        <BattleRow
          key={`${b.timeUtc}-${i}`}
          b={b}
          open={open === i}
          onToggle={() => { haptic('light'); setOpen(open === i ? null : i) }}
          onOpenPlayer={onOpenPlayer}
          t={t}
        />
      ))}
    </div>
  )
}

function BattleRow({ b, open, onToggle, onOpenPlayer, t }: {
  b: SheetBattle; open: boolean; onToggle: () => void; onOpenPlayer?: (tag: string) => void; t: Translations
}) {
  const s = t.sheet
  const cls = b.result > 0 ? 'psheet-battle-win' : b.result < 0 ? 'psheet-battle-loss' : 'psheet-battle-draw'
  const mode = s.modes[b.type] ?? b.type

  return (
    <div className={`psheet-battle ${cls}`}>
      <button type="button" className="psheet-battle-head" onClick={onToggle}>
        <span className="psheet-battle-score">{b.crownsFor}:{b.crownsAgainst}</span>
        <span className="psheet-battle-main">
          <span className="psheet-battle-result">
            {b.result > 0 ? s.win : b.result < 0 ? s.loss : s.draw}
            {b.trophyChange ? <span className={b.trophyChange > 0 ? 'wtop-up' : 'wtop-down'}> {b.trophyChange > 0 ? '+' : ''}{b.trophyChange}</span> : null}
          </span>
          <span className="muted small psheet-battle-sub">{mode} · {when(b.timeUtc)}</span>
        </span>
        <span className="psheet-battle-chevron">{open ? '▾' : '▸'}</span>
      </button>

      <div className="psheet-battle-opp">
        <span className="muted small">{s.vs}</span>{' '}
        {b.opponentTag && onOpenPlayer ? (
          <button type="button" className="psheet-link" onClick={() => onOpenPlayer(b.opponentTag!)}>
            {b.opponentName || b.opponentTag} ›
          </button>
        ) : <span>{b.opponentName || '—'}</span>}
      </div>

      {open && (
        <div className="psheet-battle-decks">
          <span className="wtop-deck-label">{s.myDeck}</span>
          <DeckRow cards={b.myDeck} />
          <span className="wtop-deck-label">{s.oppDeck}</span>
          <DeckRow cards={b.opponentDeck} />
        </div>
      )}
    </div>
  )
}

/** Коллекция и подробный профиль — отдельным запросом, только когда открыли вкладку. */
function CardsTab({ tag, t }: { tag: string; t: Translations }) {
  const [profile, setProfile] = useState<PlayerProfile | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    let alive = true
    api.getPlayerProfile(tag)
      .then(p => { if (alive) { setProfile(p); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [tag])

  if (state === 'loading') return <p className="center muted small">{t.sheet.cardsLoading}</p>
  if (state === 'error' || !profile) return <p className="center muted small">{t.sheet.cardsError}</p>
  return <div className="fade-in psheet-cards"><PlayerProfileCard profile={profile} embedded /></div>
}

/** «2ч» вместо даты: в журнале боёв важно, насколько давно, а не когда именно. */
function when(iso: string): string {
  const ts = Date.parse(iso)
  if (Number.isNaN(ts)) return ''
  const mins = Math.max(0, Math.round((Date.now() - ts) / 60000))
  if (mins < 60) return `${mins}м`
  const hours = Math.round(mins / 60)
  if (hours < 24) return `${hours}ч`
  return `${Math.round(hours / 24)}д`
}
