import { useT } from '../lib/i18n'
import { openExternalLink, haptic } from '../lib/telegram'

export function CommunityCard() {
  const { t } = useT()
  const open = () => {
    haptic('light')
    openExternalLink('https://t.me/RoyalFamilyNewsChannel')
  }
  return (
    <section className="card community-card">
      <div className="community-inner">
        <span className="community-icon">👑</span>
        <div className="community-text">
          <span className="community-label">{t.community.label}</span>
        </div>
      </div>
      <button className="btn community-btn" onClick={open}>{t.community.btn}</button>
    </section>
  )
}
