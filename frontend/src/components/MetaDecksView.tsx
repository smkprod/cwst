import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { MetaCard, MetaDeckRow, MetaDecks } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import type { Translations } from '../lib/i18n'

/**
 * Лучшие колоды топа за неделю — по боям, а не по профилям.
 *
 * Профиль говорит, чем игрок играет сейчас; бои говорят, чем выигрывают. Под
 * каждой колодой — чего она боится: карты соперника, против которых её процент
 * побед заметно ниже обычного. Этого в игре нет, и ради этого сюда и заходят.
 */
export function MetaDecksView({ t }: { t: Translations }) {
  const [data, setData] = useState<MetaDecks | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'empty' | 'error'>('loading')

  useEffect(() => {
    let alive = true
    api.getMetaDecks()
      .then(d => {
        if (!alive) return
        setData(d)
        setState(d && d.decks.length > 0 ? 'ready' : 'empty')
      })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [])

  if (state === 'loading') return <div className="center" style={{ marginTop: 16 }}><div className="spinner" /></div>
  if (state === 'error') return <p className="center muted" style={{ marginTop: 16 }}>{t.worldTop.decksError}</p>

  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="card-title">{t.worldTop.decksTitle}</div>
      <p className="muted small" style={{ margin: '0 0 2px' }}>{t.worldTop.decksHint}</p>

      {state === 'empty' || !data ? (
        <p className="muted small" style={{ margin: '8px 0 0' }}>{t.worldTop.decksCollecting}</p>
      ) : (
        <>
          <p className="muted small" style={{ margin: '0 0 10px', opacity: 0.75 }}>
            {t.worldTop.decksWindow
              .replace('{from}', data.fromDayUtc)
              .replace('{to}', data.toDayUtc)
              .replace('{n}', fmt(data.battles))}
          </p>
          {data.decks.map((d, i) => <DeckRow key={i} deck={d} place={i + 1} t={t} />)}
        </>
      )}
    </section>
  )
}

function DeckRow({ deck, place, t }: { deck: MetaDeckRow; place: number; t: Translations }) {
  const good = deck.winPercent >= 55
  const bad = deck.winPercent < 50

  return (
    <div className="mdeck">
      <div className="mdeck-head">
        <span className="mdeck-place">{place}</span>
        <span className={`mdeck-win ${good ? 'wtop-up' : bad ? 'wtop-down' : ''}`}>
          {deck.winPercent}% <span className="mdeck-unit">{t.worldTop.deckWin}</span>
        </span>
        <span className="muted small mdeck-wdl">
          {fmt(deck.games)} {t.worldTop.deckGames} · {deck.wins}/{deck.draws}/{deck.losses}
        </span>
      </div>

      <div className="wtop-deck">
        {deck.cards.map((c, i) => <CardIcon key={`${c.cardId}-${i}`} card={c} />)}
      </div>

      <div className="mdeck-stats">
        <span><b>{deck.usagePercent}%</b> {t.worldTop.deckUsage}</span>
        <span><b>💧{deck.avgElixir}</b> {t.worldTop.deckElixir}</span>
        <span><b>🔄{deck.cycleElixir}</b> {t.worldTop.deckCycle}</span>
      </div>

      <div className="mdeck-counters">
        <span className="muted small">{t.worldTop.deckCounters}</span>
        {deck.counters.length === 0
          ? <span className="muted small">{t.worldTop.deckNoCounters}</span>
          : deck.counters.map(c => (
              <span key={`${c.card.cardId}-${c.card.evo}`} className="mdeck-counter" title={c.card.name}>
                <CardIcon card={c.card} />
                <span className="wtop-down mdeck-counter-pct">{c.deltaPercent}</span>
              </span>
            ))}
      </div>

      {deck.copyLink && (
        <a
          className="btn-mini mdeck-open"
          href={deck.copyLink}
          target="_blank"
          rel="noreferrer"
          onClick={() => haptic('medium')}
        >
          {t.worldTop.deckOpen}
        </a>
      )}
    </div>
  )
}

function CardIcon({ card }: { card: MetaCard }) {
  return (
    <span className={`wtop-deck-card ${card.evo ? 'mdeck-evo' : ''}`} title={card.name}>
      {card.iconUrl
        ? <img src={card.iconUrl} alt={card.name} loading="lazy" />
        : <span className="wtop-card-blank" />}
    </span>
  )
}
