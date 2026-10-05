import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { Challenge } from '../types'
import { haptic, initData, startParam } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { openChallenge, promoDone } from '../lib/promo'
import { Icon } from './ui/Icon'

const seenKey = (id: string) => `cwst_ch_promo_${id}`

/**
 * Окно-анонс челленджа: один раз на каждый челлендж, при открытии приложения,
 * пока он скоро или идёт, а человек ещё не участвует. Кнопка ведёт прямо на
 * страницу челленджа.
 */
export function ChallengePromo() {
  const { t } = useT()
  const s = t.ch
  const [data, setData] = useState<Challenge | null>(null)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    // Вне Telegram, или пришёл по ссылке прямо на челлендж — анонс не нужен
    if (!initData || startParam === 'challenge') { promoDone(); return }
    let alive = true
    const id = window.setTimeout(() => {
      api.getChallenge()
        .then(c => {
          if (!alive) return
          let seen = false
          try { seen = localStorage.getItem(seenKey(c.event.id)) === '1' } catch { /* без памяти — покажем */ }
          const open = c.event.status !== 'ended' && !c.joined && c.linked && !seen
          if (!open) { promoDone(); return }
          try { localStorage.setItem(seenKey(c.event.id), '1') } catch { /* пусть так */ }
          setData(c)
          haptic('medium')
        })
        .catch(() => promoDone())
    }, 900)
    return () => { alive = false; window.clearTimeout(id) }
  }, [])

  useEffect(() => {
    if (!data) return
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [data])

  if (!data) return null
  const ev = data.event
  const prize = ev.prize ?? s.defaultPrize
  const start = new Date(ev.startUtc).getTime()
  const end = new Date(ev.endUtc).getTime()
  const live = now >= start
  const left = Math.max(0, Math.floor(((live ? end : start) - now) / 1000))
  const d = Math.floor(left / 86400), h = Math.floor((left % 86400) / 3600), m = Math.floor((left % 3600) / 60), sec = left % 60
  const timer = `${d > 0 ? `${d}${s.daysShort} ` : ''}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`

  const close = () => { haptic('light'); setData(null); promoDone() }
  const go = () => { haptic('medium'); setData(null); promoDone(); openChallenge() }

  return (
    <div className="modal-backdrop chp-backdrop" onClick={close}>
      <div className="chp fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="chp-glow" />
        <button className="chp-x" aria-label="close" onClick={close}><Icon name="x" size={16} /></button>
        <div className="chp-ticket mx-chp-ticket"><Icon name="ticket" size={44} /></div>
        <div className="chp-prize mx-chp-prize"><Icon name="trophy" size={15} /> {prize}</div>
        <h2 className="chp-title">{s.promoTitle.replace('{prize}', prize)}</h2>
        <p className="chp-text">{s.promoText.replace('{prize}', prize)}</p>
        <div className="chp-timer">
          <span>{live ? s.promoLive : s.promoStarts}</span>
          <b>{timer}</b>
        </div>
        <button className="ch-join-btn chp-go" onClick={go}>
          <span className="ch-join-shine" />
          {s.promoGo}
        </button>
        <button className="chp-later" onClick={close}>{s.promoLater}</button>
      </div>
    </div>
  )
}
