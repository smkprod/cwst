import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { MetaDecks, TopCard, TopProfileDeck } from '../types'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { CardIcon, DeckRow } from './MetaDecksView'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

/**
 * Колоды с одной картой: тап по карте в «Мете».
 *
 * Два списка. По боям — чем с этой картой выигрывают (есть, когда накопилась
 * мета). По профилям — чем с ней играют прямо сейчас; он есть всегда, поэтому
 * окно не бывает пустым даже в первый день.
 */
export function CardDecksModal({ card, onClose }: { card: TopCard; onClose: () => void }) {
  const { t } = useT()
  const [data, setData] = useState<MetaDecks | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    api.getCardDecks(card.cardId)
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [card.cardId])

  const battleDecks = data?.decks ?? []
  const profileDecks = data?.profileDecks ?? []

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()}>
        <div className="modal-grip" />

        <div className="modal-head">
          <div className="modal-title-wrap cdeck-title">
            {card.iconUrl && <img src={card.iconUrl} alt="" className="cdeck-title-icon" />}
            <span className="modal-name">{t.worldTop.cardDecksTitle} «{card.name}»</span>
          </div>
          <button className="modal-close" onClick={onClose} aria-label={t.warlog.close}><Icon name="x" size={18} /></button>
        </div>

        {state === 'loading' && <div className="center" style={{ padding: 24 }}><div className="spinner" /></div>}
        {state === 'error' && <p className="center muted">{t.worldTop.decksError}</p>}

        {state === 'ready' && battleDecks.length === 0 && profileDecks.length === 0 && (
          <p className="center muted">{t.worldTop.cardDecksNone}</p>
        )}

        {state === 'ready' && battleDecks.length > 0 && (
          <>
            <SectionHead className="bt-group-head" icon="flame" tone="orange" title={t.worldTop.cardDecksBattles} />
            {battleDecks.map((d, i) => <DeckRow key={i} deck={d} place={i + 1} t={t} />)}
          </>
        )}

        {state === 'ready' && profileDecks.length > 0 && (
          <>
            <SectionHead className="bt-group-head" icon="users" tone="blue" title={t.worldTop.cardDecksProfiles} />
            {profileDecks.map((d, i) => <ProfileDeck key={i} deck={d} t={t} />)}
          </>
        )}
      </div>
    </div>
  )
}

function ProfileDeck({ deck, t }: { deck: TopProfileDeck; t: Translations }) {
  const elixir = deck.cards.length
    ? Math.round(deck.cards.reduce((s, c) => s + c.elixir, 0) / deck.cards.length * 10) / 10
    : 0

  return (
    <div className="mdeck">
      <div className="mdeck-head">
        <span className="mdeck-win">{deck.players} <span className="mdeck-unit">{t.worldTop.cardDecksPlayers}</span></span>
        <span className="muted small mdeck-wdl">{t.worldTop.cardDecksBest} #{deck.bestRank} · <Icon name="droplet" size={12} className="bt-ico-elixir" /> {elixir}</span>
      </div>
      <div className="wtop-deck">
        {deck.cards.map((c, i) => <CardIcon key={`${c.cardId}-${i}`} card={c} />)}
      </div>
      {deck.copyLink && (
        <a className="btn-mini mdeck-open" href={deck.copyLink} target="_blank" rel="noreferrer"
           onClick={() => haptic('medium')}>
          {t.worldTop.deckOpen}
        </a>
      )}
    </div>
  )
}
