import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { WhatsNew } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

/**
 * «Что нового» — персональная дельта с прошлого визита, первым экраном при входе.
 * Показываем только когда есть о чём сказать: пустая карточка хуже, чем её отсутствие.
 * ВАЖНО: запрос обновляет отметку визита на сервере, поэтому дёргаем строго один раз.
 */
export function WhatsNewCard() {
  const { t } = useT()
  const [data, setData] = useState<WhatsNew | null>(null)
  const [closed, setClosed] = useState(false)

  useEffect(() => {
    api.getWhatsNew().then(setData).catch(() => setData(null))
  }, [])

  if (!data || closed || data.isFirstVisit) return null

  const rows: string[] = []
  if (data.fameDelta > 0) rows.push(`🏅 ${t.whatsNew.gained} <b>+${fmt(data.fameDelta)}</b> ${t.whatsNew.medals}`)
  if (data.rankDelta > 0) rows.push(`📈 ${t.whatsNew.climbed} <b>${data.rankDelta}</b> ${t.whatsNew.places} — ${t.whatsNew.nowRank} #${data.rank}`)
  if (data.rankDelta < 0 && data.passedByName) rows.push(`📉 ${data.passedByName} ${t.whatsNew.passedYou} — ${t.whatsNew.nowRank} #${data.rank}`)
  if (data.respectsSince > 0) rows.push(`👏 ${t.whatsNew.gotRespects} <b>+${data.respectsSince}</b>`)

  if (rows.length === 0) return null

  return (
    <div className="card whats-new-card fade-in">
      <div className="card-title-row">
        <div className="card-title">{t.whatsNew.title}</div>
        <button
          className="notif-close"
          onClick={() => { haptic('light'); setClosed(true) }}
          aria-label="✕"
        >✕</button>
      </div>

      <ul className="whats-new-list">
        {rows.map((html, i) => (
          <li key={i} dangerouslySetInnerHTML={{ __html: html }} />
        ))}
      </ul>

      {data.decksLeftToday > 0 && (
        <p className="whats-new-cta small">
          ⚔️ {t.whatsNew.decksLeft} <b>{data.decksLeftToday}/4</b> — {t.whatsNew.goPlay}
        </p>
      )}
    </div>
  )
}
