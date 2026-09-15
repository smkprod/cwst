import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TopBattle, TopPlayerDetail } from '../types'
import { fmt } from '../lib/format'
import { copyText, haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { DeckStrip } from './DeckStrip'

/**
 * Карточка игрока из мирового топа: профиль, текущая колода и последние бои.
 *
 * Данные берутся живьём, а не из снимка: колода и журнал боёв имеют смысл только
 * свежими — вчерашняя колода топа никому не интересна. Место при этом подставляется
 * из снимка, потому что живой рейтинг по одному тегу API не отдаёт.
 */
export function TopPlayerModal({ tag, onClose }: { tag: string; onClose: () => void }) {
  const { t } = useT()
  const [data, setData] = useState<TopPlayerDetail | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    setState('loading')
    api.getTopPlayerDetail(tag)
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [tag])

  const copy = async () => {
    haptic('light')
    if (await copyText(data?.playerTag ?? tag)) {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()}>
        <div className="modal-grip" />

        <div className="modal-head">
          <div className="modal-title-wrap">
            <span className="modal-name">{data?.name ?? '…'}</span>
            <button className="tag-copy muted small" onClick={copy}>
              {data?.playerTag ?? tag}
              <span className={`tag-copy-icon ${copied ? 'tag-copy-done' : ''}`}>{copied ? '✓' : '⧉'}</span>
            </button>
          </div>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>

        {state === 'loading' && <div className="center" style={{ padding: 24 }}><div className="spinner" /></div>}
        {state === 'error' && <p className="center muted">{t.worldTop.error}</p>}

        {state === 'ready' && data && (
          <>
            <div className="modal-grid">
              <Stat value={fmt(data.trophies)} label={t.worldTop.trophies} accent />
              <Stat value={data.rank ? `#${data.rank}` : '—'} label={t.worldTop.rank} />
              <Stat value={fmt(data.bestTrophies)} label={t.worldTop.bestTrophies} />
              <Stat value={String(data.expLevel)} label={t.worldTop.level} />
              <Stat value={fmt(data.wins)} label={t.worldTop.wins} />
              <Stat value={fmt(data.threeCrownWins)} label={t.worldTop.threeCrowns} />
            </div>

            {data.clanName && (
              <p className="muted small" style={{ margin: '10px 0 0' }}>🛡 {data.clanName}</p>
            )}

            <div className="wtop-section-title">{t.worldTop.currentDeck}</div>
            {data.currentDeck.length > 0
              ? <DeckStrip deck={data.currentDeck} />
              : <p className="muted small" style={{ margin: 0 }}>{t.worldTop.deckUnknown}</p>}

            <div className="wtop-section-title">{t.worldTop.battles}</div>
            {data.battles.length === 0 ? (
              <p className="muted small" style={{ margin: 0 }}>{t.worldTop.noBattles}</p>
            ) : (
              data.battles.map((b, i) => <Battle key={i} b={b} t={t} />)
            )}
          </>
        )}
      </div>
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

/**
 * Один бой: счёт, обе колоды. Колода соперника здесь не украшение — по ней видно,
 * чем именно топа обыграли, а это как раз то, что в игре посмотреть нельзя.
 */
function Battle({ b, t }: { b: TopBattle; t: Translations }) {
  return (
    <div className={`wtop-battle ${b.won ? 'wtop-battle-win' : 'wtop-battle-loss'}`}>
      <div className="wtop-battle-head">
        <span className="wtop-battle-score">
          {b.won ? '🏅' : '💀'} {b.crownsFor}:{b.crownsAgainst}
        </span>
        <span className="muted small wtop-battle-opp">
          {t.worldTop.vs} {b.opponentName || '—'}
        </span>
        <span className="muted small wtop-battle-when">{when(b.battleTimeUtc)}</span>
      </div>
      <div className="wtop-battle-decks">
        <span className="wtop-deck-label">{t.worldTop.deckMine}</span>
        <DeckStrip deck={b.myDeck} />
        <span className="wtop-deck-label">{t.worldTop.deckOpponent}</span>
        <DeckStrip deck={b.opponentDeck} />
      </div>
    </div>
  )
}

/**
 * «2 ч назад» вместо даты: в журнале боёв важно не когда именно, а насколько давно.
 * Непарсящееся время лучше не показывать вовсе, чем печатать Invalid Date.
 */
function when(iso: string): string {
  const ts = Date.parse(iso)
  if (Number.isNaN(ts)) return ''
  const mins = Math.max(0, Math.round((Date.now() - ts) / 60000))
  if (mins < 60) return `${mins}м`
  const hours = Math.round(mins / 60)
  if (hours < 24) return `${hours}ч`
  return `${Math.round(hours / 24)}д`
}
