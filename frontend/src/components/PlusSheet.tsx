import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { PlusStatus } from '../types'
import { haptic, hapticNotify, openInvoice, tg } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PLUS_CHANGED } from '../lib/plusSheet'

/**
 * «Clanify Плюс»: что даёт, сколько стоит, купить в два нажатия.
 *
 * Выгоды — первыми и конкретными: «напишу, когда начнёшь сливать серию» продаёт,
 * «расширенная аналитика» — нет. Цена стоит под ними, а не над: человек сначала
 * должен понять, за что платит.
 */
export function PlusSheet({ onClose }: { onClose: () => void }) {
  const { t } = useT()
  const p = t.plus
  const [status, setStatus] = useState<PlusStatus | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [pay, setPay] = useState<'idle' | 'opening' | 'waiting' | 'paid' | 'failed'>('idle')

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    api.getPlus()
      .then(s => { if (alive) { setStatus(s); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [])

  const close = () => { haptic('light'); onClose() }

  const buy = async (days: number) => {
    haptic('medium')
    setPay('opening')
    try {
      const { link } = await api.createPlusInvoice(days)
      const result = await openInvoice(link)
      if (result === 'cancelled') { setPay('idle'); return }
      if (result !== 'paid') { setPay('failed'); hapticNotify('error'); return }

      // Списано, но выдаёт бот, получив подтверждение отдельным сообщением. Ждём,
      // пока срок на сервере действительно сдвинется, а не рисуем «готово» заранее.
      setPay('waiting')
      const before = status?.until ?? null
      for (let i = 0; i < 15; i++) {
        await new Promise(r => setTimeout(r, 1000))
        const fresh = await api.getPlus().catch(() => null)
        if (fresh?.active && fresh.until !== before) {
          setStatus(fresh)
          setPay('paid')
          hapticNotify('success')
          window.dispatchEvent(new Event(PLUS_CHANGED))
          return
        }
      }
      setPay('failed')
      window.dispatchEvent(new Event(PLUS_CHANGED))
    } catch {
      hapticNotify('error')
      setPay('failed')
    }
  }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up plus-sheet" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />
        <div className="plus-hero">
          <button className="modal-close plus-close" onClick={close} aria-label={p.close}>✕</button>
          <div className="plus-title">{p.title}</div>
          <div className="plus-tagline">{p.tagline}</div>
        </div>

        <ul className="plus-features">
          <Feature title={p.f1Title} text={p.f1} />
          <Feature title={p.f2Title} text={p.f2} />
          <Feature title={p.f3Title} text={p.f3} />
          <Feature title={p.f4Title} text={p.f4} />
        </ul>

        {state === 'loading' && <div className="center" style={{ padding: 16 }}><div className="spinner" /></div>}
        {state === 'error' && <p className="center muted small">{t.battles.loadError}</p>}
        {state === 'ready' && status && (
          <Offer status={status} pay={pay} onBuy={buy} t={t} />
        )}

        <p className="muted small plus-fine">{p.once}<br />{p.fanNote}</p>
      </div>
    </div>
  )
}

function Feature({ title, text }: { title: string; text: string }) {
  return (
    <li className="plus-feature">
      <b>{title}</b>
      <span className="muted small">{text}</span>
    </li>
  )
}

function Offer({ status, pay, onBuy, t }: {
  status: PlusStatus
  pay: 'idle' | 'opening' | 'waiting' | 'paid' | 'failed'
  onBuy: (days: number) => void
  t: Translations
}) {
  const p = t.plus
  if (!status.paywall) return <p className="plus-status plus-status-ok">{p.free}</p>
  if (!status.linked) return <p className="plus-status">{p.notLinked}</p>

  const canPay = status.onSale && Boolean(tg?.openInvoice)
  const busy = pay === 'opening' || pay === 'waiting'
  const source = status.source === 'trial' ? p.sourceTrial
    : status.source === 'sponsor' ? p.sourceSponsor
    : status.source === 'gift' ? p.sourceGift
    : status.source === 'grant' ? p.sourceGrant
    : status.source === 'purchase' ? p.sourcePurchase
    : null

  return (
    <div className="plus-offer">
      {status.active && status.until && (
        <p className="plus-status plus-status-ok">
          ✅ {p.activeUntil.replace('{date}', new Date(status.until).toLocaleDateString())}
          {source && <span className="muted small"> · {source}</span>}
        </p>
      )}
      {!status.active && !status.trialUsed && status.trialDays > 0 && (
        <p className="muted small plus-trial-note">
          🎁 {p.trialNote.replace('{days}', String(status.trialDays)).replace('{n}', String(status.trialMinBattles))}
        </p>
      )}

      {!canPay ? (
        <p className="muted small">{p.unavailable}</p>
      ) : (
        <>
          {status.active && <p className="muted small" style={{ margin: '4px 0' }}>{p.extend}</p>}
          <div className="plus-buttons">
            <button className="btn plus-buy" disabled={busy} onClick={() => onBuy(30)}>
              {p.buy30.replace('{stars}', String(status.price30))}
              <span className="plus-best">{p.best}</span>
            </button>
            <button className="btn btn-ghost plus-buy" disabled={busy} onClick={() => onBuy(7)}>
              {p.buy7.replace('{stars}', String(status.price7))}
            </button>
          </div>
        </>
      )}

      {pay === 'opening' && <p className="muted small">{p.opening}</p>}
      {pay === 'waiting' && <p className="muted small">{p.waiting}</p>}
      {pay === 'paid' && <p className="plus-status plus-status-ok">{p.paid}</p>}
      {pay === 'failed' && <p className="muted small">{p.failed}</p>}
    </div>
  )
}
