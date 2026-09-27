import { useState } from 'react'
import { api } from '../lib/api'
import type { AppConfig } from '../types'
import { haptic, hapticNotify, openInvoice, tg } from '../lib/telegram'
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
  const [state, setState] = useState<'idle' | 'paying' | 'waiting' | 'failed'>('idle')

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

  const buy = async () => {
    haptic('medium')
    setState('paying')
    try {
      const { link } = await api.createSponsorInvoice()
      const status = await openInvoice(link)
      if (status === 'cancelled') { setState('idle'); return }
      if (status !== 'paid') { setState('failed'); hapticNotify('error'); return }

      // Списано — но выдаёт бот, получив подтверждение отдельным сообщением. Ждём,
      // пока срок на сервере действительно сдвинется, а не рисуем звезду заранее:
      // иначе при сбое выдачи человек увидел бы спонсорство, которого нет.
      setState('waiting')
      const before = config.sponsorUntil
      for (let i = 0; i < 15; i++) {
        await new Promise(r => setTimeout(r, 1000))
        const fresh = await api.getAppConfig().catch(() => null)
        if (fresh?.isSponsor && fresh.sponsorUntil !== before) {
          hapticNotify('success')
          setState('idle')
          onBought()
          return
        }
      }
      // Не дождались за 15 секунд — не значит, что пропало: бот мог выдать позже.
      // Обновляем как есть и говорим честно, что делать, если звезды так и нет.
      setState('failed')
      onBought()
    } catch {
      hapticNotify('error')
      setState('failed')
    }
  }

  const renew = config.isSponsor
  const label = state === 'paying' ? t.sponsor.opening
    : state === 'waiting' ? t.sponsor.waiting
    : (renew ? t.sponsor.renew : t.sponsor.buy)
        .replace('{stars}', String(config.sponsorPriceStars))
        .replace('{days}', String(config.sponsorDays))

  return (
    <>
      <button className={className} disabled={state === 'paying' || state === 'waiting'} onClick={buy}>
        {label}
      </button>
      {state === 'failed' && <p className="muted small">{t.sponsor.failed}</p>}
    </>
  )
}
