import type { ClanOverview } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { useOpenClan } from '../lib/clanModal'

/**
 * Топ кланов страны из официального рейтинга CR. Показываем игроку без подключённого
 * клана: он видит и планку в своём регионе, и куда реально можно перейти.
 */
export function RegionTopCard({ overview }: { overview: ClanOverview }) {
  const openClan = useOpenClan()
  const { t } = useT()
  if (overview.countryTop.length === 0) return null

  return (
    <section className="card">
      <div className="card-title-row">
        <div className="card-title">🌍 {t.clanless.regionTitle}</div>
        {overview.countryName && <span className="muted small">{overview.countryName}</span>}
      </div>

      <ul className="region-top-list">
        {overview.countryTop.map(c => (
          <li key={`${c.rank}-${c.tag}`}>
            <button
              className={`region-top-row clan-row-tap ${c.isOurClan ? 'region-top-mine' : ''}`}
              onClick={() => openClan(c.tag, c.name)}
            >
              <span className="region-top-rank">{c.rank}</span>
              <div className="region-top-info">
                <span className="region-top-name">{c.name}</span>
                <span className="muted small">👥 {c.members}/50</span>
              </div>
              <span className="region-top-trophies">⚔️ {fmt(c.warTrophies)}</span>
            </button>
          </li>
        ))}
      </ul>

      {overview.countryRank != null && (
        <p className="muted small" style={{ margin: '10px 0 0' }}>
          {t.clanless.yourClanRank}: #{overview.countryRank}
          {overview.globalRank != null && ` · ${t.clanless.worldRank}: #${overview.globalRank}`}
        </p>
      )}
    </section>
  )
}
