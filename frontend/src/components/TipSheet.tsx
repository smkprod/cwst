import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'
import { OPEN_TIP } from '../lib/tipSheet'
import { haptic } from '../lib/telegram'
import { ARENA_TIPS, TIP_IDS, isTipId, tipScene, tipText, type TipId } from '../lib/tips'
import { ArenaAnim } from './ArenaAnim'
import { Icon } from './ui/Icon'

/** Окно фишки: схема арены с анимацией, «когда» и шаги. Открывается событием из профиля и библиотеки. */
export function TipSheet() {
  const { t, lang } = useT()
  const s = t.dash
  const [id, setId] = useState<TipId | null>(null)

  useEffect(() => {
    const open = (e: Event) => {
      const next = (e as CustomEvent<string>).detail
      if (isTipId(next)) { haptic('light'); setId(next) }
    }
    window.addEventListener(OPEN_TIP, open)
    return () => window.removeEventListener(OPEN_TIP, open)
  }, [])

  if (!id) return null
  const text = tipText(lang, id)
  const scene = tipScene(lang, id)
  const close = () => { haptic('light'); setId(null) }
  const next = () => {
    haptic('light')
    const list = TIP_IDS.filter(x => ARENA_TIPS.includes(x))
    setId(list[(list.indexOf(id) + 1) % list.length])
  }

  return (
    <div className="modal-backdrop tip-backdrop" onClick={close}>
      <div className="tip-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <button className="fb-x" aria-label={s.close} onClick={close}><Icon name="x" size={16} /></button>
        <h2 className="tip-title">{text.title}</h2>
        {scene && <div className="tip-arena"><ArenaAnim scene={scene} /></div>}
        <p className="tip-when"><b>{s.when}:</b> {text.when}</p>
        <p className="tip-how">{s.how}</p>
        <ol className="tip-steps">
          {text.steps.map((step, i) => <li key={i}><span>{i + 1}</span>{step}</li>)}
        </ol>
        {scene && <button className="btn btn-ghost tip-next" onClick={next}>{s.next} <Icon name="chevronRight" size={16} /></button>}
      </div>
    </div>
  )
}

/** Библиотека фишек: плитки с мини-схемой. */
export function TipsLibrary() {
  const { t, lang } = useT()
  const s = t.dash
  return (
    <div className="tips-lib fade-in">
      <h2 className="tips-lib-title">{s.tipsTitle}</h2>
      <p className="muted small">{s.tipsLead}</p>
      <div className="tips-grid">
        {ARENA_TIPS.map(id => {
          const scene = tipScene(lang, id)
          return (
            <button key={id} className="tips-tile" onClick={() => openTipById(id)}>
              {scene && <div className="tips-tile-arena"><ArenaAnim scene={scene} paused /></div>}
              <b>{tipText(lang, id).title}</b>
            </button>
          )
        })}
      </div>
    </div>
  )
}

function openTipById(id: TipId) {
  window.dispatchEvent(new CustomEvent(OPEN_TIP, { detail: id }))
}
