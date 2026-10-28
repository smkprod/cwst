import { useEffect, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { JarOrder, PlusStatus } from '../types'
import { useShowcase } from '../lib/showcase'
import { copyText, haptic, hapticNotify, openExternalLink, openInvoice, shareToTelegram, tg } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PLUS_CHANGED } from '../lib/plusSheet'
import { markOfferSeen } from '../lib/funnel'
import { Icon, type IconName } from './ui/Icon'
import { InfoButton } from './ui/Info'
import { IconTile, SectionHead } from './ui/Section'

export type PlusFocus = 'plus' | 'sponsor' | 'gift'

type Pay = 'idle' | 'opening' | 'waiting' | 'paid' | 'gifted' | 'failed'

/**
 * Одна линейка: «Плюс — чтобы не сливать» и «★ Спонсор — чтобы не сливать и
 * чтобы это видели». Под обеими — «Подарить» и «Попросить в подарок»: у подростка
 * часто нет своей карты, и платит старший брат, лидер или родители.
 *
 * Выгода — первой и конкретной, цена — под ней: сначала понять, за что платишь.
 */
export function PlusSheet({ focus, onClose }: { focus: PlusFocus; onClose: () => void }) {
  const { t } = useT()
  const p = t.plus
  const openShowcase = useShowcase()
  const [status, setStatus] = useState<PlusStatus | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [pay, setPay] = useState<Pay>('idle')
  const [giftOpen, setGiftOpen] = useState(focus === 'gift')
  // Оплата гривнами через банку: заказ с кодом для комментария к переводу
  const [jar, setJar] = useState<JarOrder | null>(null)
  const [jarBusy, setJarBusy] = useState(false)
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

  // Оба предложения на экране - шаг «увидел» воронки покупки по обоим
  useEffect(() => {
    if (state !== 'ready' || !status?.paywall || !status.linked) return
    markOfferSeen('plus', 'sheet')
    // Без цены карточки спонсорства нет - и видеть было нечего
    if (status.sponsorPrice > 0) markOfferSeen('sponsor', 'sheet')
  }, [state, status])

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

  const orderJar = async (product: 'plus' | 'pro', days: number) => {
    haptic('medium')
    setJarBusy(true)
    try {
      setJar(await api.jarOrder(product, days))
    } catch {
      hapticNotify('error')
      setPay('failed')
    } finally {
      setJarBusy(false)
    }
  }

  const jarPaid = async () => {
    await load()
    window.dispatchEvent(new Event(PLUS_CHANGED))
  }

  // «Попросить в подарок»: счёт на себя, который пересылают тому, кто заплатит
  const ask = async () => {
    haptic('medium')
    try {
      const { link } = await api.createPlusInvoice(30, undefined, true)
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
          <button className="modal-close plus-close" onClick={close} aria-label={p.close}><Icon name="x" size={18} /></button>
          <div className="plus-title bt-inline"><IconTile name="gem" tone="violet" size={36} /> {p.title}</div>
          <div className="plus-tagline">{p.parents}</div>
          {/* Сначала увидеть, потом платить: витрина с живыми примерами и примеркой фонов */}
          <button className="plus-showcase-link" onClick={() => { close(); openShowcase(focus === 'sponsor' ? 'sponsor' : 'plus') }}>
            <Icon name="sparkles" size={14} /> {t.showcase.openShowcase} <Icon name="chevronRight" size={14} />
          </button>
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
                {jar ? (
                  <JarPanel key={jar.code} order={jar} onBack={() => setJar(null)} onPaid={jarPaid} t={t} />
                ) : (
                  <>
                    <PlusCard status={status} pay={pay} onBuy={buyPlus} onJar={d => orderJar('plus', d)} jarBusy={jarBusy} t={t} />
                    <div ref={sponsorRef}>
                      <SponsorCard status={status} pay={pay} onBuy={buySponsor} onJar={() => orderJar('pro', status.sponsorDays)} jarBusy={jarBusy} t={t} />
                    </div>
                  </>
                )}

                <div className="plus-gift-row">
                  <button className="btn btn-ghost" onClick={() => { haptic('light'); setGiftOpen(o => !o) }}><Icon name="gift" size={16} /> {p.giftBtn}</button>
                  <button className="btn btn-ghost" onClick={ask}><Icon name="send" size={16} /> {p.askBtn}</button>
                </div>
                {giftOpen && <GiftPanel status={status} onPaidGift={giftPaid} onFreeGift={load} t={t} />}
              </>
            )}

            <PayState pay={pay} t={t} />
          </>
        )}

        <div className="muted small plus-fine bt-fine">
          {p.once}
          <InfoButton size={18} title={p.title}>
            <p>{p.once}</p>
            <p>{p.refund48}</p>
            <p>{p.fanNote}</p>
          </InfoButton>
        </div>
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
    const source = status.source === 'gift' ? p.sourceGift : status.source === 'grant' ? p.sourceGrant
      : status.source === 'referral' ? p.sourceReferral : null
    return (
      <p className="plus-status plus-status-ok">
        <Icon name="checkCircle" size={15} /> {p.activeUntil.replace('{date}', new Date(status.until).toLocaleDateString())}
        {source && <span className="muted small"> · {source}</span>}
      </p>
    )
  }
  return null
}

function PlusCard({ status, pay, onBuy, onJar, jarBusy, t }: {
  status: PlusStatus; pay: Pay; onBuy: (days: number) => void; onJar: (days: number) => void; jarBusy: boolean; t: Translations
}) {
  const p = t.plus
  const canPay = status.onSale && Boolean(tg?.openInvoice)
  const busy = pay === 'opening' || pay === 'waiting'
  return (
    <section className="plus-card">
      <SectionHead icon="gem" tone="violet" title={p.plusCardTitle} aside={p.plusCardSub} />
      <ul className="plus-features">
        <Feature icon="snowflake" title={p.f1Title} text={p.f1} />
        <Feature icon="target" title={t.trk.title} text={t.trk.plusRow} />
        <Feature icon="sliders" title={p.f2Title} text={p.f2} />
        <Feature icon="pause" title={p.f3Title} text={p.f3} />
        <Feature icon="chart" title={p.f4Title} text={p.f4} />
        <Feature icon="star" title={p.f5Title} text={p.f5} />
        <Feature icon="target" title={p.f6Title} text={p.f6} />
      </ul>
      {!canPay && !status.jar ? <p className="muted small">{p.unavailable}</p> : !canPay ? null : (
        <div className="plus-buttons">
          {status.promoUntil && (
            <span className="plus-promo">
              {t.ch.promoBadge.replace('{date}', new Date(status.promoUntil).toLocaleString(t.dateLocale, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }))}
            </span>
          )}
          <button className="btn plus-buy" disabled={busy} onClick={() => onBuy(30)}>
            {status.promo30 ? <s className="plus-old">{status.price30}★</s> : null}
            {p.buy30.replace('{stars}', String(status.promo30 ?? status.price30))}
            <span className="plus-best">{p.best}</span>
          </button>
          <button className="btn btn-ghost plus-buy" disabled={busy} onClick={() => onBuy(7)}>
            {status.promo7 ? <s className="plus-old">{status.price7}★</s> : null}
            {p.buy7.replace('{stars}', String(status.promo7 ?? status.price7))}
          </button>
        </div>
      )}
      {status.jar && status.onSale && (
        <div className="jar-offer">
          <span className="jar-offer-title"><Icon name="wallet" size={14} /> {p.jarTitle}</span>
          <div className="jar-offer-row">
            <button className="btn btn-ghost jar-btn" disabled={jarBusy} onClick={() => onJar(30)}>{p.jarPlus30.replace('{uah}', String(status.jar.plus30))}</button>
            <button className="btn btn-ghost jar-btn" disabled={jarBusy} onClick={() => onJar(7)}>{p.jarPlus7.replace('{uah}', String(status.jar.plus7))}</button>
          </div>
        </div>
      )}
      {!status.active && !status.isSponsor && <p className="muted small plus-trial-note bt-inline"><Icon name="gift" size={14} /> {p.trialNote}</p>}
    </section>
  )
}

function SponsorCard({ status, pay, onBuy, onJar, jarBusy, t }: {
  status: PlusStatus; pay: Pay; onBuy: () => void; onJar: () => void; jarBusy: boolean; t: Translations
}) {
  const p = t.plus
  if (status.sponsorPrice <= 0) return null
  const canPay = Boolean(tg?.openInvoice)
  const busy = pay === 'opening' || pay === 'waiting'
  return (
    <section className="plus-card plus-card-sponsor">
      <SectionHead icon="crown" tone="gold" title={p.sponsorCardTitle.replace(/^(★|👑)\s*/u, '')} aside={p.sponsorCardSub}
        info={<p>{p.sponsorForLeader}</p>} />
      <ul className="plus-perks">
        {p.sponsorPerks.split('|').map(x => <li key={x}>{x}</li>)}
      </ul>
      {canPay && (
        <button className="btn plus-buy plus-buy-sponsor" disabled={busy} onClick={onBuy}>
          {(status.isSponsor ? t.sponsor.renew : p.buySponsor)
            .replace('{days}', String(status.sponsorDays)).replace('{stars}', String(status.sponsorPrice))}
        </button>
      )}
      {status.jar && (
        <button className="btn btn-ghost jar-btn jar-btn-pro" disabled={jarBusy} onClick={onJar}>
          <Icon name="wallet" size={15} /> {p.jarPro.replace('{uah}', String(status.jar.pro))}
        </button>
      )}
    </section>
  )
}

/**
 * Оплата через банку Монобанка: сумма, код для комментария и кнопка в банку. Доступ
 * выдаёт воркер по выписке банки - здесь только ждём, пока заказ станет оплаченным.
 */
function JarPanel({ order, onBack, onPaid, t }: {
  order: JarOrder; onBack: () => void; onPaid: () => void; t: Translations
}) {
  const p = t.plus
  const [copied, setCopied] = useState(false)
  const [check, setCheck] = useState<'idle' | 'waiting' | 'notyet' | 'paid'>(order.paid ? 'paid' : 'idle')
  const alive = useRef(true)
  const polling = useRef(false)
  const ref = useRef<HTMLElement>(null)

  useEffect(() => {
    ref.current?.scrollIntoView({ block: 'start' })
    return () => { alive.current = false }
  }, [])

  const copy = async () => {
    if (await copyText(order.code)) { setCopied(true); haptic('light') }
  }

  // Ждём перевод: бот читает выписку раз в минуту - смотрим каждые 6 секунд до четырёх минут
  const poll = async () => {
    if (polling.current) return
    polling.current = true
    setCheck('waiting')
    for (let i = 0; i < 40 && alive.current; i++) {
      const fresh = await api.jarGetOrder(order.code).catch(() => null)
      if (fresh?.paid) {
        if (!alive.current) return
        setCheck('paid')
        hapticNotify('success')
        onPaid()
        return
      }
      await new Promise(r => setTimeout(r, 6000))
    }
    polling.current = false
    if (alive.current) setCheck('notyet')
  }

  const open = () => {
    haptic('medium')
    openExternalLink(order.link)
    void poll()
  }

  return (
    <section ref={ref} className="plus-card jar-panel">
      <SectionHead icon="wallet" tone="green" title={order.product === 'pro' ? p.sponsorCardTitle.replace(/^(★|👑)\s*/u, '') : p.plusCardTitle}
        aside={`${order.amountUah} ₴`} />
      {check === 'paid' ? (
        <p className="plus-status plus-status-ok">{p.jarPaid}</p>
      ) : (
        <>
          <p className="small jar-step">{p.jarStep.replace('{uah}', String(order.amountUah))}</p>
          <button className="jar-code" onClick={copy} aria-label={p.jarCopy}>
            <b>{order.code}</b>
            <span className="small">{copied ? p.jarCopied : p.jarCopy}</span>
          </button>
          <div className="plus-buttons">
            <button className="btn plus-buy jar-open" onClick={open}>{p.jarOpen} · {order.amountUah} ₴</button>
            <button className="btn btn-ghost plus-buy" disabled={check === 'waiting'} onClick={() => { haptic('light'); void poll() }}>{p.jarCheck}</button>
          </div>
          {check === 'waiting' && <p className="muted small jar-note">{p.jarWaiting}</p>}
          {check === 'notyet' && <p className="muted small jar-note">{p.jarNotYet}</p>}
          <p className="muted small jar-note">{p.jarAuto}</p>
        </>
      )}
      <button className="btn btn-ghost jar-back" onClick={() => { haptic('light'); onBack() }}>
        <Icon name="chevronLeft" size={15} /> {p.jarBack}
      </button>
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
      <SectionHead icon="gift" tone="green" title={p.giftTitle} />
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

function Feature({ icon, title, text }: { icon: IconName; title: string; text: string }) {
  return (
    <li className="plus-feature bt-feature">
      <IconTile name={icon} tone="violet" size={28} />
      <span className="bt-feature-text">
        <b>{title}</b>
        <span className="muted small">{text}</span>
      </span>
    </li>
  )
}
