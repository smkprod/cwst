import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { PlusStatus } from '../types'
import { haptic, hapticNotify, openInvoice, shareToTelegram, tg } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PLUS_CHANGED } from '../lib/plusSheet'

export type PlusFocus = 'plus' | 'sponsor' | 'gift'

type Pay = 'idle' | 'opening' | 'waiting' | 'paid' | 'gifted' | 'failed'

/**
 * Одна линейка: «🧊 Плюс — чтобы не сливать» и «★ Спонсор — чтобы не сливать и
 * чтобы это видели». Под обеими — «Подарить» и «Попросить в подарок»: у подростка
 * часто нет своей карты, и платит старший брат, лидер или родители.
 *
 * Выгода — первой и конкретной, цена — под ней: сначала понять, за что платишь.
 */
export function PlusSheet({ focus, onClose }: { focus: PlusFocus; onClose: () => void }) {
  const { t } = useT()
  const p = t.plus
  const [status, setStatus] = useState<PlusStatus | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [pay, setPay] = useState<Pay>('idle')
  const [giftOpen, setGiftOpen] = useState(focus === 'gift')
  const sponsorRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  const load = () => api.getPlus()
    .then(s => { setStatus(s); setState('ready') })
    .catch(() => setState('error'))

  useEffect(() => { load() }, [])

  // Открыли с «Стать спонсором» — сразу к карточке спонсора
  useEffect(() => {
    if (state === 'ready' && focus === 'sponsor') sponsorRef.current?.scrollIntoView({ block: 'center' })
  }, [state, focus])

  const close = () => { haptic('light'); onClose() }

  /**
   * Оплата и ожидание выдачи. Выдаёт бот, получив подтверждение отдельным
   * сообщением, поэтому ждём, пока срок на сервере действительно сдвинется.
   */
  const payAndWait = async (getLink: () => Promise<string>, changed: (fresh: PlusStatus) => boolean, gift = false) => {
    haptic('medium')
    setPay('opening')
    try {
      const link = await getLink()
      const result = await openInvoice(link)
      if (result === 'cancelled') { setPay('idle'); return }
      if (result !== 'paid') { setPay('failed'); hapticNotify('error'); return }
      if (gift) { setPay('gifted'); hapticNotify('success'); return }

      setPay('waiting')
      for (let i = 0; i < 15; i++) {
        await new Promise(r => setTimeout(r, 1000))
        const fresh = await api.getPlus().catch(() => null)
        if (fresh && changed(fresh)) {
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

  const buyPlus = (days: number) => payAndWait(
    async () => (await api.createPlusInvoice(days)).link,
    fresh => fresh.active && fresh.until !== status?.until)

  const buySponsor = () => payAndWait(
    async () => (await api.createSponsorInvoice()).link,
    fresh => fresh.isSponsor && fresh.sponsorUntil !== status?.sponsorUntil)

  const giftPaid = (tag: string, days: number) => payAndWait(
    async () => (await api.createPlusInvoice(days, tag)).link,
    () => false, true)

  // «Попросить в подарок»: счёт на себя, который пересылают тому, кто заплатит
  const ask = async () => {
    haptic('medium')
    try {
      const { link } = await api.createPlusInvoice(30)
      shareToTelegram(p.askText, link)
    } catch {
      hapticNotify('error')
    }
  }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up plus-sheet" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />
        <div className="plus-hero">
          <button className="modal-close plus-close" onClick={close} aria-label={p.close}>✕</button>
          <div className="plus-title">{p.title}</div>
          <div className="plus-tagline">{p.parents}</div>
        </div>

        {state === 'loading' && <div className="center" style={{ padding: 16 }}><div className="spinner" /></div>}
        {state === 'error' && <p className="center muted small">{t.battles.loadError}</p>}

        {state === 'ready' && status && (
          <>
            <StatusLine status={status} t={t} />

            {!status.paywall ? (
              <p className="plus-status plus-status-ok">{p.free}</p>
            ) : !status.linked ? (
              <p className="plus-status">{p.notLinked}</p>
            ) : (
              <>
                <PlusCard status={status} pay={pay} onBuy={buyPlus} t={t} />
                <div ref={sponsorRef}>
                  <SponsorCard status={status} pay={pay} onBuy={buySponsor} t={t} />
                </div>

                <div className="plus-gift-row">
                  <button className="btn btn-ghost" onClick={() => { haptic('light'); setGiftOpen(o => !o) }}>{p.giftBtn}</button>
                  <button className="btn btn-ghost" onClick={ask}>{p.askBtn}</button>
                </div>
                {giftOpen && <GiftPanel status={status} onPaidGift={giftPaid} onFreeGift={load} t={t} />}
              </>
            )}

            <PayState pay={pay} t={t} />
          </>
        )}

        <p className="muted small plus-fine">{p.once}<br />{p.refund48}<br />{p.fanNote}</p>
      </div>
    </div>
  )
}

function StatusLine({ status, t }: { status: PlusStatus; t: Translations }) {
  const p = t.plus
  if (status.isSponsor && status.sponsorUntil) {
    return <p className="plus-status plus-status-ok">{p.sponsorActive.replace('{date}', new Date(status.sponsorUntil).toLocaleDateString())}</p>
  }
  if (status.active && status.until) {
    const source = status.source === 'gift' ? p.sourceGift : status.source === 'grant' ? p.sourceGrant : null
    return (
      <p className="plus-status plus-status-ok">
        ✅ {p.activeUntil.replace('{date}', new Date(status.until).toLocaleDateString())}
        {source && <span className="muted small"> · {source}</span>}
      </p>
    )
  }
  return null
}

function PlusCard({ status, pay, onBuy, t }: {
  status: PlusStatus; pay: Pay; onBuy: (days: number) => void; t: Translations
}) {
  const p = t.plus
  const canPay = status.onSale && Boolean(tg?.openInvoice)
  const busy = pay === 'opening' || pay === 'waiting'
  return (
    <section className="plus-card">
      <div className="plus-card-head">
        <span className="plus-card-title">{p.plusCardTitle}</span>
        <span className="muted small">{p.plusCardSub}</span>
      </div>
      <ul className="plus-features">
        <Feature title={p.f1Title} text={p.f1} />
        <Feature title={p.f2Title} text={p.f2} />
        <Feature title={p.f3Title} text={p.f3} />
        <Feature title={p.f4Title} text={p.f4} />
      </ul>
      {!canPay ? <p className="muted small">{p.unavailable}</p> : (
        <div className="plus-buttons">
          <button className="btn plus-buy" disabled={busy} onClick={() => onBuy(30)}>
            {p.buy30.replace('{stars}', String(status.price30))}
            <span className="plus-best">{p.best}</span>
          </button>
          <button className="btn btn-ghost plus-buy" disabled={busy} onClick={() => onBuy(7)}>
            {p.buy7.replace('{stars}', String(status.price7))}
          </button>
        </div>
      )}
      {!status.active && !status.isSponsor && <p className="muted small plus-trial-note">🎁 {p.trialNote}</p>}
    </section>
  )
}

function SponsorCard({ status, pay, onBuy, t }: {
  status: PlusStatus; pay: Pay; onBuy: () => void; t: Translations
}) {
  const p = t.plus
  if (status.sponsorPrice <= 0) return null
  const canPay = Boolean(tg?.openInvoice)
  const busy = pay === 'opening' || pay === 'waiting'
  return (
    <section className="plus-card plus-card-sponsor">
      <div className="plus-card-head">
        <span className="plus-card-title">{p.sponsorCardTitle}</span>
        <span className="muted small">{p.sponsorCardSub}</span>
      </div>
      <ul className="plus-perks">
        {p.sponsorPerks.split('|').map(x => <li key={x}>{x}</li>)}
      </ul>
      <p className="muted small" style={{ margin: '0 0 8px' }}>{p.sponsorForLeader}</p>
      {canPay && (
        <button className="btn plus-buy plus-buy-sponsor" disabled={busy} onClick={onBuy}>
          {(status.isSponsor ? t.sponsor.renew : p.buySponsor)
            .replace('{days}', String(status.sponsorDays)).replace('{stars}', String(status.sponsorPrice))}
        </button>
      )}
    </section>
  )
}

/**
 * Подарок по тегу. Платный — любому привязанному игроку; у спонсора ещё две
 * бесплатные недели в месяц. Получатель узнаёт о подарке от бота сразу.
 */
function GiftPanel({ status, onPaidGift, onFreeGift, t }: {
  status: PlusStatus
  onPaidGift: (tag: string, days: number) => void
  onFreeGift: () => void
  t: Translations
}) {
  const p = t.plus
  const [tag, setTag] = useState('')
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const clean = tag.trim()

  const errorText = (e: unknown) => {
    const code = e instanceof ApiError ? e.code : ''
    return code === 'recipient_not_linked' ? p.giftNotLinked
      : code === 'player_not_found' ? p.giftNotFound
      : code === 'self_gift' ? p.giftSelf
      : code === 'no_gifts_left' ? p.giftNoLeft
      : t.battles.loadError
  }

  const giveFree = async () => {
    if (!clean) return
    haptic('medium')
    setBusy(true)
    setNote(null)
    try {
      const r = await api.giftFreePlus(clean)
      hapticNotify('success')
      setNote(p.giftDone.replace('{name}', r.recipient).replace('{date}', new Date(r.until).toLocaleDateString()))
      setTag('')
      onFreeGift()
    } catch (e) {
      hapticNotify('error')
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="plus-card plus-gift">
      <div className="plus-card-title">{p.giftTitle}</div>
      <input
        className="search-input"
        placeholder={p.giftTagPlaceholder}
        value={tag}
        onChange={e => setTag(e.target.value)}
        autoCapitalize="characters"
        spellCheck={false}
      />
      {status.isSponsor && (
        <>
          <p className="muted small" style={{ margin: '8px 0 4px' }}>{p.giftFreeLeft.replace('{n}', String(status.freeGiftsLeft))}</p>
          <button className="btn plus-buy" disabled={busy || !clean || status.freeGiftsLeft <= 0} onClick={giveFree}>
            {p.giftFreeBtn}
          </button>
        </>
      )}
      {status.onSale && Boolean(tg?.openInvoice) && (
        <div className="plus-buttons" style={{ marginTop: 8 }}>
          <button className="btn btn-ghost plus-buy" disabled={!clean} onClick={() => onPaidGift(clean, 30)}>
            {p.giftPaid30.replace('{stars}', String(status.price30))}
          </button>
          <button className="btn btn-ghost plus-buy" disabled={!clean} onClick={() => onPaidGift(clean, 7)}>
            {p.giftPaid7.replace('{stars}', String(status.price7))}
          </button>
        </div>
      )}
      {note && <p className="small" style={{ margin: '8px 0 0' }}>{note}</p>}
    </section>
  )
}

function PayState({ pay, t }: { pay: Pay; t: Translations }) {
  const p = t.plus
  if (pay === 'opening') return <p className="muted small">{p.opening}</p>
  if (pay === 'waiting') return <p className="muted small">{p.waiting}</p>
  if (pay === 'paid') return <p className="plus-status plus-status-ok">{p.paid}</p>
  if (pay === 'gifted') return <p className="plus-status plus-status-ok">{p.giftSent}</p>
  if (pay === 'failed') return <p className="muted small">{p.failed}</p>
  return null
}

function Feature({ title, text }: { title: string; text: string }) {
  return (
    <li className="plus-feature">
      <b>{title}</b>
      <span className="muted small">{text}</span>
    </li>
  )
}
