import { useT } from '../lib/i18n'
import { getBotUsername, haptic, shareToTelegram } from '../lib/telegram'
import { IconTile } from './ui/Section'
import { InfoButton } from './ui/Info'

/** Призыв подключить бота к чату своего клана (/setup) — главный growth-крючок для гостя:
 *  превращает «любопытного с тегом» в приведённый целиком клан. */
export function LeaderCtaCard() {
  const { t } = useT()

  const tellClan = () => {
    haptic('medium')
    // Ссылка «добавить бота в группу»: лидер выбирает чат клана, и бот уже там
    const bot = getBotUsername()
    shareToTelegram(t.leaderCta.shareText, bot ? `https://t.me/${bot}?startgroup=hello` : undefined)
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
