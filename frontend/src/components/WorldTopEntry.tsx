import { useState } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { WorldTopView } from './WorldTopView'
import { Icon } from './ui/Icon'
import { IconTile } from './ui/Section'

/**
 * Вход в мировой топ из раздела «Рейтинг».
 *
 * Свёрнут по умолчанию не ради компактности: раскрытый экран сразу тянет снимок,
 * справочник карт и полсотни строк с иконками. Класть это в загрузку раздела,
 * который открывают ради своего клана, — платить трафиком за то, что попросили
 * не все.
 */
export function WorldTopEntry() {
  const { t } = useT()
  const [open, setOpen] = useState(false)

  if (open) {
    return (
      <div className="fade-in">
        <button className="btn-mini more-back" onClick={() => { haptic('light'); setOpen(false) }}>
          <Icon name="chevronLeft" size={15} /> {t.more.back}
        </button>
        <WorldTopView />
      </div>
    )
  }

  return (
    <section className="card" style={{ marginTop: 10 }}>
      <button className="more-row wtop-entry" onClick={() => { haptic('light'); setOpen(true) }}>
        <span className="more-row-icon pl-row-tile"><IconTile name="globe" tone="blue" size={36} /></span>
        <span className="more-row-text">
          <span className="more-row-title">{t.more.worldTop}</span>
          <span className="muted small">{t.more.worldTopHint}</span>
        </span>
        <span className="more-row-arrow"><Icon name="chevronRight" size={18} /></span>
      </button>
    </section>
  )
}
