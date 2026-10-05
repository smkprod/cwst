import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { WhatsNew } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { Icon, type IconName } from './ui/Icon'
import { SectionHead } from './ui/Section'

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

  const rows: { icon: IconName; html: string }[] = []
  if (data.fameDelta > 0) rows.push({ icon: 'medal', html: `${t.whatsNew.gained} <b>+${fmt(data.fameDelta)}</b> ${t.whatsNew.medals}` })
  if (data.rankDelta > 0) rows.push({ icon: 'trendUp', html: `${t.whatsNew.climbed} <b>${data.rankDelta}</b> ${t.whatsNew.places} — ${t.whatsNew.nowRank} #${data.rank}` })
  if (data.rankDelta < 0 && data.passedByName) rows.push({ icon: 'trendDown', html: `${data.passedByName} ${t.whatsNew.passedYou} — ${t.whatsNew.nowRank} #${data.rank}` })
  if (data.respectsSince > 0) rows.push({ icon: 'thumbsUp', html: `${t.whatsNew.gotRespects} <b>+${data.respectsSince}</b>` })

  if (rows.length === 0) return null

  return (
    <div className="card whats-new-card fade-in">
      <SectionHead icon="sparkles" tone="violet" title={t.whatsNew.title} aside={
        <button
          className="notif-close"
          onClick={() => { haptic('light'); setClosed(true) }}
          aria-label="close"
        ><Icon name="x" size={16} /></button>
      } />

      <ul className="whats-new-list">
        {rows.map((r, i) => (
          <li key={i} className="mx-wn-row">
            <span className="mx-wn-ic"><Icon name={r.icon} size={15} /></span>
            <span dangerouslySetInnerHTML={{ __html: r.html }} />
          </li>
        ))}
      </ul>

      {data.decksLeftToday > 0 && (
        <p className="whats-new-cta small">
          <Icon name="swords" size={14} /> {t.whatsNew.decksLeft} <b>{data.decksLeftToday}/4</b> — {t.whatsNew.goPlay}
        </p>
      )}
    </div>
  )
}
