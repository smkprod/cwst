import { useEffect, useState } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { DeckSuggestionsView } from './DeckSuggestionsView'

/**
 * Кнопка «мои колоды» и шторка с подбором. Отдельной вкладкой это занимало место
 * в таббаре ради экрана, который открывают изредка, — а как кнопка на своей же
 * странице подбор стоит ровно там, где о нём вспоминают: рядом с разбором колоды.
 */
export function DecksButton({ playerTag }: { playerTag: string }) {
  const { t } = useT()
  const [open, setOpen] = useState(false)

  return (
    <>
      <section className="card decks-cta">
        <div className="decks-cta-text">
          <span className="decks-cta-title">🃏 {t.decks.ctaTitle}</span>
          <span className="muted small">{t.decks.ctaHint}</span>
        </div>
        <button
          className="btn decks-cta-btn"
          onClick={() => { haptic('medium'); setOpen(true) }}
        >
          {t.decks.ctaBtn}
        </button>
      </section>

      {open && <DecksModal playerTag={playerTag} onClose={() => setOpen(false)} />}
    </>
  )
}

function DecksModal({ playerTag, onClose }: { playerTag: string; onClose: () => void }) {
  const { t } = useT()

  // Пока шторка открыта, страница под ней не должна прокручиваться вместе с ней
  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  const close = () => {
    haptic('light')
    onClose()
  }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />
        <div className="modal-head">
          <div className="modal-title-wrap">
            <h3 className="modal-name">{t.decks.title}</h3>
          </div>
          <button className="modal-close" onClick={close} aria-label={t.warlog.close}>✕</button>
        </div>

        <DeckSuggestionsView playerTag={playerTag} embedded />
      </div>
    </div>
  )
}
