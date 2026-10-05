import { useT } from '../lib/i18n'
import { openExternalLink, haptic } from '../lib/telegram'
import { Icon } from './ui/Icon'
import { IconTile } from './ui/Section'

export function CommunityCard() {
  const { t } = useT()
  const open = () => {
    haptic('light')
    openExternalLink('https://t.me/RoyalFamilyNewsChannel')
  }
  return (
    <section className="card community-card">
      <div className="community-inner">
        <span className="community-icon cl-tile-wrap"><IconTile name="crown" tone="gold" size={44} /></span>
        <div className="community-text">
          <span className="community-label">{t.community.label}</span>
        </div>
      </div>
      <button className="btn community-btn" onClick={open}>{t.community.btn}<Icon name="external" size={16} /></button>
    </section>
  )
}
