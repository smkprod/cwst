import { useState } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

/** Ключ версии: если меню перестроят снова, достаточно поднять номер. */
const SEEN_KEY = 'menuNoticeSeen:v1'

function alreadySeen(): boolean {
  try { return localStorage.getItem(SEEN_KEY) === '1' } catch { return false }
}

/**
 * Однократное объяснение, куда что переехало.
 *
 * Перестановка меню — ровно то, что сбивает с толку человека, у которого всё
 * работало вчера. Многошаговый тур на такое отвечать не стоит: его проматывают,
 * не читая. Здесь одна карточка на первом заходе, с явной кнопкой «понятно»,
 * и больше она не появляется.
 */
export function MenuChangedNotice() {
  const { t } = useT()
  const [hidden, setHidden] = useState(alreadySeen)

  if (hidden) return null

  const dismiss = () => {
    haptic('light')
    try { localStorage.setItem(SEEN_KEY, '1') } catch { /* приватный режим */ }
    setHidden(true)
  }

  return (
    <section className="card menu-notice fade-in">
      <div className="connect-title">✨ {t.menuNotice.title}</div>
      <ul className="menu-notice-list">
        <li>{t.menuNotice.clan}</li>
        <li>{t.menuNotice.me}</li>
        <li>{t.menuNotice.more}</li>
      </ul>
      <button className="btn btn-nudge" style={{ width: '100%', marginTop: 10 }} onClick={dismiss}>
        {t.menuNotice.gotIt}
      </button>
    </section>
  )
}
