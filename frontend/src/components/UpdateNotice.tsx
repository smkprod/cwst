import { useState } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

/**
 * Ключ версии объявления. Вышло новое — поднимаем номер и меняем строки в i18n
 * (updateNotice.*), карточка снова покажется всем ровно один раз.
 */
const SEEN_KEY = 'updateNotice:v1'

function alreadySeen(): boolean {
  try { return localStorage.getItem(SEEN_KEY) === '1' } catch { return false }
}

/**
 * «Что нового» — одна карточка на первом заходе после обновления.
 *
 * Рассылка в чат про такое не годится: её читают те, кто и так в курсе, а
 * остальные видят ещё одно сообщение от бота и приглушают его. Здесь объявление
 * встречает человека там, где изменения и произошли, и исчезает после «понятно».
 */
export function UpdateNotice() {
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
      <div className="connect-title">🎉 {t.updateNotice.title}</div>
      <ul className="menu-notice-list">
        <li>{t.updateNotice.game}</li>
        <li>{t.updateNotice.invite}</li>
        <li>{t.updateNotice.tag}</li>
      </ul>
      <button className="btn-invite" style={{ marginTop: 10 }} onClick={dismiss}>
        {t.updateNotice.gotIt}
      </button>
    </section>
  )
}
