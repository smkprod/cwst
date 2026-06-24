import { useT } from '../lib/i18n'
import { haptic, shareToTelegram } from '../lib/telegram'

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
        <span className="community-icon">📣</span>
        <div className="community-text">
          <span className="community-label">{t.leaderCta.label}</span>
          <span className="muted small">{t.leaderCta.hint}</span>
        </div>
      </div>
      <button className="btn community-btn" onClick={tellClan}>{t.leaderCta.btn}</button>
    </section>
  )
}
