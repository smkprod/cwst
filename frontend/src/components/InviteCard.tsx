import { useT } from '../lib/i18n'
import { haptic, shareToTelegram, botStartLink, tgUser } from '../lib/telegram'
import { useBotUsername } from '../lib/botUsername'
import { IconTile } from './ui/Section'

/** Кнопка «Пригласить друга» — личная реф-ссылка в бота (ref_<telegramId>). */
export function InviteCard() {
  const { t } = useT()
  // Нужны и username бота (для ссылки), и Telegram ID пользователя (реф-код).
  const botUsername = useBotUsername()
  if (!botUsername || !tgUser) return null

  const refLink = botStartLink(`ref_${tgUser.id}`)
  const invite = () => {
    haptic('medium')
    shareToTelegram(t.invite.shareText, refLink)
  }

  return (
    <section className="card community-card">
      <div className="community-inner">
        <span className="community-icon cl-tile-wrap"><IconTile name="gift" tone="violet" size={44} /></span>
        <div className="community-text">
          <span className="community-label">{t.invite.label}</span>
        </div>
      </div>
      <button className="btn community-btn" onClick={invite}>{t.invite.btn}</button>
    </section>
  )
}
