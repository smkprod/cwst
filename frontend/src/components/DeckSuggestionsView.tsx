import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { DeckCard, DeckSuggestion, DeckSuggestions, TopDeck } from '../types'
import { useT, type Translations } from '../lib/i18n'
import { haptic } from '../lib/telegram'

/**
 * Колоды, подобранные под коллекцию игрока. Разделены честно: «можно играть сейчас» —
 * это колоды, где открыты все восемь карт, остальное лежит в «почти собрана» с явным
 * списком недостающего. Обещать колоду, которую человек физически не может составить,
 * хуже, чем не показать её вовсе.
 */
export function DeckSuggestionsView({ playerTag, embedded = false }: {
  playerTag: string
  /** Внутри шторки заголовок уже есть — второй раз его печатать не надо. */
  embedded?: boolean
}) {
  const { t } = useT()
  const [data, setData] = useState<DeckSuggestions | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    let alive = true
    setState('loading')
    api.getPlayerDecks(playerTag)
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [playerTag])

  if (state === 'loading') {
    return <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
  }
  if (state === 'error' || !data) {
    return <p className="center muted" style={{ marginTop: 16 }}>{t.decks.error}</p>
  }

  const nothing = data.ready.length === 0 && data.almost.length === 0

  return (
    <div>
      {embedded ? (
        <p className="muted small" style={{ margin: '0 0 4px' }}>
          {t.decks.hint} · {data.baseSize} {t.decks.decksWord} · {data.baseUpdated}
        </p>
      ) : (
        <section className="card">
          <div className="card-title-row">
            <div className="card-title">{t.decks.title}</div>
            <span className="muted small">{data.baseSize} {t.decks.decksWord}</span>
          </div>
          <p className="muted small" style={{ margin: '0 0 4px' }}>{t.decks.hint}</p>
          <p className="muted small" style={{ margin: 0, opacity: 0.75 }}>
            {data.baseUpdated} · {data.baseSource}
          </p>
        </section>
      )}

      {nothing && (
        <p className="center muted small" style={{ marginTop: 16 }}>{t.decks.empty}</p>
      )}

      {data.ready.length > 0 && (
        <>
          <div className="decks-group-title">✅ {t.decks.ready}</div>
          {data.ready.map(d => <DeckRow key={d.id} deck={d} t={t} />)}
        </>
      )}

      {data.almost.length > 0 && (
        <>
          <div className="decks-group-title">🧩 {t.decks.almost}</div>
          {data.almost.map(d => <DeckRow key={d.id} deck={d} t={t} />)}
        </>
      )}

      {/* Колоды мирового топа — единственный источник, который не надо править руками:
          база меты устаревает каждый сезон молча, а живой топ обновляет себя сам. */}
      {data.top.length > 0 && (
        <>
          <div className="decks-group-title">🌍 {t.decks.topTitle}</div>
          <p className="muted small" style={{ margin: '0 0 8px' }}>{t.decks.topHint}</p>
          {data.top.map(d => <TopDeckRow key={d.deck.id} top={d} t={t} />)}
        </>
      )}
    </div>
  )
}

/**
 * Колода из мирового топа. Над обычной карточкой — строка с именем игрока и его местом:
 * «так играет игрок №1 мира» весомее любой усреднённой меты, и без этой подписи
 * колода ничем не отличалась бы от остальных.
 */
function TopDeckRow({ top, t }: { top: TopDeck; t: Translations }) {
  return (
    <div className="top-deck">
      <div className="top-deck-head">
        <span className="top-deck-rank">#{top.rank}</span>
        <span className="top-deck-name">{top.playerName}</span>
        <span className="muted small">{top.trophies} 🏆{top.clanName ? ` · ${top.clanName}` : ''}</span>
      </div>
      <DeckRow deck={top.deck} t={t} />
    </div>
  )
}

function DeckRow({ deck, t }: { deck: DeckSuggestion; t: Translations }) {
  const [open, setOpen] = useState(false)
  const complete = deck.missing.length === 0

  return (
    <section className="card deck-card">
      <div className="deck-head">
        <div className="deck-head-main">
          <div className="deck-name">{deck.name}</div>
          <div className="deck-chips">
            <span className="deck-chip">{deck.archetype}</span>
            <span className="deck-chip">💧 {deck.avgElixir}</span>
            <span className="deck-chip">🔄 {deck.cycleCost}</span>
          </div>
        </div>
        <div className="deck-readiness">
          <span className={`deck-readiness-value ${complete ? 'deck-ready-on' : ''}`}>
            {deck.readiness}%
          </span>
          <span className="deck-readiness-label">{t.decks.readiness}</span>
        </div>
      </div>

      <div className="deck-track">
        <div className={`deck-fill ${complete ? 'deck-fill-on' : ''}`} style={{ width: `${deck.readiness}%` }} />
      </div>

      <div className="deck-cards">
        {deck.cards.map(c => <DeckCardChip key={c.name} card={c} />)}
      </div>

      <p className={`deck-verdict ${complete ? '' : 'deck-verdict-warn'}`}>{deck.verdict}</p>

      {/* Только считаемые величины: средний эликсир и цикл — про саму колоду,
          уровни и максы — про то, как она выглядит именно в твоей коллекции. */}
      <div className="deck-stats">
        <div className="deck-stat">
          <span className="deck-stat-value">{deck.avgLevel > 0 ? deck.avgLevel : '—'}</span>
          <span className="deck-stat-label">{t.decks.avgLevel}</span>
        </div>
        <div className="deck-stat">
          <span className="deck-stat-value">{deck.maxedCount}/8</span>
          <span className="deck-stat-label">{t.decks.maxed}</span>
        </div>
        <div className="deck-stat">
          <span className="deck-stat-value">{deck.cycleCost}</span>
          <span className="deck-stat-label">{t.decks.cycle}</span>
        </div>
        <div className="deck-stat">
          <span className="deck-stat-value">{deck.levelsToMax > 0 ? deck.levelsToMax : '✓'}</span>
          <span className="deck-stat-label">{t.decks.levelsToMax}</span>
        </div>
      </div>

      <div className="deck-meta muted small">
        <span>{t.decks.rarityTitle}:</span>
        {deck.rarity.map(r => (
          <span key={r.rarity} className={`rarity-chip rarity-${r.rarity}`}>
            {r.count} {t.decks.rarity[r.rarity] ?? r.rarity}
          </span>
        ))}
        {deck.evoAvailable > 0 && <span>· ⚡ {deck.evoUnlocked}/{deck.evoAvailable}</span>}
      </div>

      {/* Совет, который нельзя собрать в один тап, — половина совета.
          Ссылка открывает колоду прямо в Clash Royale. Кнопки нет, если у какой-то
          карты не нашлось id: неполная ссылка открыла бы не ту колоду. */}
      {deck.copyLink && (
        <a
          className="btn btn-nudge deck-open-btn"
          href={deck.copyLink}
          target="_blank"
          rel="noreferrer"
          onClick={() => haptic('medium')}
        >
          {t.decks.openInGame}
        </a>
      )}

      <button className="btn-mini deck-note-btn" onClick={() => setOpen(o => !o)}>
        {open ? t.decks.hideNote : t.decks.showNote}
      </button>
      {open && <p className="muted small deck-note">{deck.note}</p>}
    </section>
  )
}

function DeckCardChip({ card }: { card: DeckCard }) {
  // Карта, которой у игрока нет, показывается приглушённой: видно, чего добирать,
  // и при этом сразу понятно, что играть колодой прямо сейчас не выйдет.
  return (
    <div className={`deck-card-chip ${card.owned ? '' : 'deck-card-missing'} ${card.evoUnlocked ? 'card-chip-evo' : ''}`}>
      <img src={card.iconUrl} alt={card.name} title={card.name} loading="lazy" />
      {card.evoUnlocked && <span className="card-evo-mark">⚡</span>}
      <div className={`card-chip-level ${card.owned && card.level >= card.maxLevel ? 'card-chip-maxed' : ''}`}>
        {card.owned ? card.level : '—'}
      </div>
    </div>
  )
}
