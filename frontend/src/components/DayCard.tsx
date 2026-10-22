import { useState } from 'react'
import { useT } from '../lib/i18n'
import { API_BASE } from '../lib/apiBase'
import { haptic, shareToTelegram } from '../lib/telegram'
import { Icon } from './ui/Icon'

/**
 * «Мой день»: итог игрового дня картинкой - счёт, кубки, серия и цепочка боёв.
 * Та же картинка уходит в утренней сводке в личку; здесь её можно посмотреть
 * за сегодня и поделиться в чат клана. Боёв за день нет - карточки нет.
 */
export function DayCard({ playerTag }: { playerTag: string }) {
  const { t } = useT()
  const s = t.day
  const [back, setBack] = useState<0 | 1>(1)
  const [failed, setFailed] = useState<Record<number, boolean>>({})
  const [loaded, setLoaded] = useState<Record<number, boolean>>({})

  // Смещение пояса как у сервера: минуты к UTC, Киев летом - 180
  const offset = -new Date().getTimezoneOffset()
  const path = `/api/img/day/${encodeURIComponent(playerTag.replace('#', ''))}.jpg?o=${offset}&d=${back}`
  const src = `${API_BASE}${path}`
  // Оба дня без боёв - прячем карточку целиком
  if (failed[0] && failed[1]) return null

  const share = () => {
    haptic('light')
    const abs = src.startsWith('http') ? src : `${window.location.origin}${src}`
    shareToTelegram(s.shareText, abs)
  }

  return (
    <div className="card day-card fade-in">
      <div className="day-head">
        <b><Icon name="activity" size={16} /> {s.title}</b>
        <div className="day-switch">
          {([1, 0] as const).map(b => (
            <button key={b} className={`day-opt ${back === b ? 'day-opt-on' : ''}`}
              onClick={() => { haptic('light'); setBack(b) }}>{b === 1 ? s.yesterday : s.today}</button>
          ))}
        </div>
      </div>
      {failed[back] ? (
        <p className="muted small day-empty">{back === 1 ? s.emptyYesterday : s.emptyToday}</p>
      ) : (
        <>
          <img key={src} className={`day-img ${loaded[back] ? 'day-img-on' : ''}`} src={src} alt={s.title}
            onLoad={() => setLoaded(l => ({ ...l, [back]: true }))}
            onError={() => setFailed(f => ({ ...f, [back]: true }))} />
          {loaded[back] && (
            <button className="btn-mini day-share" onClick={share}><Icon name="share" size={14} /> {s.share}</button>
          )}
        </>
      )}
    </div>
  )
}
