import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { GlobalTop } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { usePlayerSheet } from '../lib/playerSheet'
import { PlaceBadge } from '../lib/sponsorMarks'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

/**
 * Живой топ-3 игроков сервиса для стартовых экранов (гость / без клана).
 * Психология: движение и соревнование цепляют сильнее статичного текста,
 * а счётчик «N игроков уже соревнуются» — социальное доказательство.
 * При ошибке API тихо не рендерится (стартовый экран не ломаем).
 */
export function TopPlayersTeaser() {
  const openSheet = usePlayerSheet()
  const [top, setTop] = useState<GlobalTop | null>(null)
  const { t } = useT()

  useEffect(() => {
    api.getGlobalTop().then(setTop).catch(() => { /* тизер опционален */ })
  }, [])

  if (!top || top.players.length === 0) return null

  return (
    <section className="card teaser-card">
      <SectionHead icon="trophy" tone="gold" title={t.guest.topTitle} />
      <ul className="teaser-list">
        {top.players.slice(0, 3).map((p, i) => (
          <li key={p.playerTag} className="teaser-row row-tap" onClick={() => openSheet(p.playerTag)}>
            <span className="teaser-medal"><PlaceBadge place={i + 1} /></span>
            <span className="teaser-name">
              {p.name}
              <span className="muted small teaser-clan">{p.clanName}</span>
            </span>
            <span className="teaser-fame pl-inline">{fmt(p.totalFame)} <Icon name="medal" size={13} /></span>
          </li>
        ))}
      </ul>
      {top.playersTracked > 0 && (
        <p className="muted small teaser-count">
          {fmt(top.playersTracked)} {t.guest.topCountSuffix}
        </p>
      )}
    </section>
  )
}
