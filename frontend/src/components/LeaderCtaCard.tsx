import { useT } from '../lib/i18n'
import { haptic, shareToTelegram } from '../lib/telegram'
import { IconTile } from './ui/Section'
import { InfoButton } from './ui/Info'

/** Призыв подключить бота к чату своего клана (/setup) — главный growth-крючок для гостя:
 *  превращает «любопытного с тегом» в приведённый целиком клан. */
export function LeaderCtaCard() {
  const { t } = useT()

  const tellClan = () => {
    haptic('medium')
    shareToTelegram(t.leaderCta.shareText)
  }

  return (
    <section className="card leader-cta-card">
      <div className="community-inner">
        <span className="community-icon cl-tile-wrap"><IconTile name="megaphone" tone="orange" size={44} /></span>
        <div className="community-text">
          <span className="community-label">{t.leaderCta.label}</span>
        </div>
        <InfoButton title={t.leaderCta.label}><p>{t.leaderCta.hint}</p></InfoButton>
      </div>
      <button className="btn community-btn" onClick={tellClan}>{t.leaderCta.btn}</button>
    </section>
  )
}
