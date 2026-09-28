import { useEffect } from 'react'
import type { AppConfig } from '../types'
import { haptic, tg } from '../lib/telegram'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { useT } from '../lib/i18n'

/**
 * Кнопка покупки спонсорства — одна на все места, где её показывают.
 *
 * Одна, потому что два независимых места с одной и той же логикой уже однажды
 * разъехались: оформление было вставлено только в одну из двух веток вкладки «Я»,
 * и тот, кто только что заплатил, его не видел. Здесь то же самое стоило бы денег.
 *
 * Продажа звёздами включается ценой в панели. Пока цены нет — или Telegram слишком
 * старый и не умеет открывать счёт — остаётся прежняя ссылка «написать владельцу».
 */
export function SponsorBuyButton({ config, onBought, className = 'btn hall-sponsor-btn' }: {
  config: AppConfig
  onBought: () => void
  className?: string
}) {
  const { t } = useT()
  const openPlus = usePlusSheet()

  // Покупка идёт в общем окне линейки; после неё оно шлёт событие — перечитываем своё
  useEffect(() => {
    window.addEventListener(PLUS_CHANGED, onBought)
    return () => window.removeEventListener(PLUS_CHANGED, onBought)
  }, [onBought])

  const canPay = config.sponsorPriceStars > 0 && Boolean(tg?.openInvoice)

  if (!canPay) {
    return config.sponsorContact ? (
      <a
        className={className}
        href={`https://t.me/${config.sponsorContact}`}
        target="_blank"
        rel="noreferrer"
        onClick={() => haptic('medium')}
      >
        {t.hall.becomeSponsor}
      </a>
    ) : null
  }

  // Одна линейка: спонсорство покупается в том же окне, что и Плюс, — рядом видно,
  // что Спонсор включает весь Плюс и подарки соклановцам.
  const label = (config.isSponsor ? t.sponsor.renew : t.sponsor.buy)
    .replace('{stars}', String(config.sponsorPriceStars))
    .replace('{days}', String(config.sponsorDays))

  return (
    <button className={className} onClick={() => openPlus('sponsor')}>
      {label}
    </button>
  )
}
