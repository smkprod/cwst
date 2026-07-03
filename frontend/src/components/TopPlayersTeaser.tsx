import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { GlobalTop } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

/**
 * Живой топ-3 игроков сервиса для стартовых экранов (гость / без клана).
 * Психология: движение и соревнование цепляют сильнее статичного текста,
 * а счётчик «N игроков уже соревнуются» — социальное доказательство.
 * При ошибке API тихо не рендерится (стартовый экран не ломаем).
 */
export function TopPlayersTeaser() {
  const [top, setTop] = useState<GlobalTop | null>(null)
  const { t } = useT()

  useEffect(() => {
    api.getGlobalTop().then(setTop).catch(() => { /* тизер опционален */ })
  }, [])

  if (!top || top.players.length === 0) return null
  const medals = ['🥇', '🥈', '🥉']

  return (
    <section className="card teaser-card">
      <div className="card-title">{t.guest.topTitle}</div>
      <ul className="teaser-list">
        {top.players.slice(0, 3).map((p, i) => (
          <li key={p.playerTag} className="teaser-row">
            <span className="teaser-medal">{medals[i]}</span>
            <span className="teaser-name">
              {p.name}
              <span className="muted small teaser-clan">{p.clanName}</span>
            </span>
            <span className="teaser-fame">{fmt(p.totalFame)} 🏅</span>
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
