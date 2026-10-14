import { useState } from 'react'
import { useT } from '../lib/i18n'
import { haptic, openTelegramLink } from '../lib/telegram'
import { useChannelUsername } from '../lib/appConfig'
import { Icon } from './ui/Icon'
import { IconTile } from './ui/Section'

/**
 * «Подпишись на канал Clanify» - мета недели, связи карт, новости. Канал берётся из
 * настроек бота: не подключён - карточки нет. С dismissKey её можно скрыть (на главных
 * экранах), без него - постоянная строка в «Ещё».
 */
export function ChannelCard({ dismissKey }: { dismissKey?: string }) {
  const { t } = useT()
  const channel = useChannelUsername()
  const storageKey = dismissKey ? `channelCardHidden:${dismissKey}` : null
  const [hidden, setHidden] = useState(() => {
    if (!storageKey) return false
    try { return localStorage.getItem(storageKey) === '1' } catch { return false }
  })
  if (!channel || hidden) return null

  const open = () => { haptic('light'); openTelegramLink(`https://t.me/${channel}`) }
  const hide = () => {
    haptic('light')
    try { if (storageKey) localStorage.setItem(storageKey, '1') } catch { /* приватный режим */ }
    setHidden(true)
  }

  return (
    <section className="card community-card">
      <div className="community-inner">
        <span className="community-icon cl-tile-wrap"><IconTile name="megaphone" tone="blue" size={44} /></span>
        <div className="community-text">
          <span className="community-label">{t.channelPromo.label}</span>
        </div>
        {storageKey && (
          <button className="btn-mini" onClick={hide} aria-label="×"><Icon name="x" size={14} /></button>
        )}
      </div>
      <button className="btn community-btn" onClick={open}>{t.channelPromo.btn}<Icon name="external" size={16} /></button>
    </section>
  )
}
