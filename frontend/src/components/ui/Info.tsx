import { useEffect, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useT } from '../../lib/i18n'
import { haptic } from '../../lib/telegram'
import { Icon } from './Icon'

/**
 * Мини-кнопка «i»: объяснение прячется за ней и открывается снизу листом.
 *
 * Раньше каждая карточка несла под заголовком серый абзац «как это считается».
 * Читают его один раз, а место он занимает всегда — экран превращался в инструкцию.
 * Теперь пояснение есть у всех одинаково, но показывается только тому, кто спросил.
 */
export function InfoButton({ title, children, size = 22, className }: {
  title?: ReactNode
  children: ReactNode
  size?: number
  className?: string
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" className={`ui-info ${className ?? ''}`} style={{ width: size, height: size }}
        aria-label="info" onClick={e => { e.stopPropagation(); haptic('light'); setOpen(true) }}>
        <Icon name="info" size={Math.round(size * 0.68)} />
      </button>
      {open && <InfoSheet title={title} onClose={() => setOpen(false)}>{children}</InfoSheet>}
    </>
  )
}

/** Лист с объяснением. Отдельно — чтобы открыть его и не из кнопки «i». */
export function InfoSheet({ title, children, onClose }: { title?: ReactNode; children: ReactNode; onClose: () => void }) {
  const { t } = useT()
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return createPortal(
    <div className="ui-sheet-backdrop" onClick={onClose}>
      <div className="ui-sheet" role="dialog" onClick={e => e.stopPropagation()}>
        <div className="ui-sheet-grip" />
        <div className="ui-sheet-head">
          <span className="ui-sheet-icon"><Icon name="info" size={18} /></span>
          {title && <span className="ui-sheet-title">{title}</span>}
          <button type="button" className="ui-sheet-close" onClick={onClose} aria-label="close"><Icon name="x" size={18} /></button>
        </div>
        <div className="ui-sheet-body">{children}</div>
        <button type="button" className="ui-btn ui-btn-primary ui-sheet-ok" onClick={onClose}>{t.ui.gotIt}</button>
      </div>
    </div>,
    document.body,
  )
}
