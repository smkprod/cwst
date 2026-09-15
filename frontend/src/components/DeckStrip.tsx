import type { TopDeckCard } from '../types'

/**
 * Колода восемью иконками в строку — читается одним взглядом, без подписей.
 *
 * Иконки грузятся лениво: на экране топа их одновременно под сотню, и тянуть все
 * сразу значит показать список только после того, как докачается последняя.
 */
export function DeckStrip({ deck }: { deck: TopDeckCard[] }) {
  if (deck.length === 0) return null

  return (
    <div className="wtop-deck">
      {deck.map((c, i) => (
        <span key={`${c.cardId}-${i}`} className="wtop-deck-card" title={c.name}>
          {c.iconUrl
            ? <img src={c.iconUrl} alt={c.name} loading="lazy" />
            : <span className="wtop-card-blank" />}
        </span>
      ))}
    </div>
  )
}
