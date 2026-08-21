import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { DeckCard, DeckSuggestion, DeckSuggestions } from '../types'
import { useT, type Translations } from '../lib/i18n'

/**
 * Колоды, подобранные под коллекцию игрока. Разделены честно: «можно играть сейчас» —
 * это колоды, где открыты все восемь карт, остальное лежит в «почти собрана» с явным
 * списком недостающего. Обещать колоду, которую человек физически не может составить,
 * хуже, чем не показать её вовсе.
 */
export function DeckSuggestionsView({ playerTag }: { playerTag: string }) {
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
    </div>
  )
}

const DIFF_CLASS: Record<string, string> = {
  easy: 'diff-easy', medium: 'diff-mid', hard: 'diff-hard',
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
            <span className={`deck-chip ${DIFF_CLASS[deck.difficulty] ?? ''}`}>
              {t.decks.difficulty[deck.difficulty] ?? deck.difficulty}
            </span>
            <span className="deck-chip">💧 {deck.avgElixir}</span>
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

      <div className="deck-meta muted small">
        <span>{t.decks.avgLevel}: {deck.avgLevel > 0 ? deck.avgLevel : '—'} / {deck.cards[0]?.maxLevel ?? '—'}</span>
        {deck.maxedCount > 0 && <span>· {t.decks.maxed}: {deck.maxedCount}/8</span>}
        {deck.evoAvailable > 0 && <span>· ⚡ {deck.evoUnlocked}/{deck.evoAvailable}</span>}
      </div>

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
