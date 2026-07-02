import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { ClanRanking } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

/**
 * Место клана в стране и мире по КВ-трофеям — официальные rankings из CR API.
 * Ранги есть только у топ-1000; если клана там нет, показываем топ страны и трофеи.
 */
export function ClanWorldRankCard() {
  const { t } = useT()
  const [rank, setRank] = useState<ClanRanking | null>(null)

  useEffect(() => {
    api.getClanRanking().then(setRank).catch(() => setRank(null))
  }, [])

  if (!rank) return null

  const delta = (r: number | null, p: number | null) => {
    if (!r || !p || p <= 0 || p === r) return null
    return p > r
      ? <span className="wr-delta wr-up">▲{p - r}</span>
      : <span className="wr-delta wr-down">▼{r - p}</span>
  }

  return (
    <section className="card" style={{ marginBottom: 10 }}>
      <div className="card-title" style={{ marginBottom: 10 }}>{t.worldRank.title}</div>

      <div className="wr-chips">
        {rank.countryRank != null && rank.countryName && (
          <div className="wr-chip">
            <span className="wr-chip-value">#{fmt(rank.countryRank)} {delta(rank.countryRank, rank.countryPreviousRank)}</span>
            <span className="wr-chip-label">{rank.countryName}</span>
          </div>
        )}
        {rank.globalRank != null && (
          <div className="wr-chip">
            <span className="wr-chip-value">#{fmt(rank.globalRank)} {delta(rank.globalRank, rank.globalPreviousRank)}</span>
            <span className="wr-chip-label">{t.worldRank.world}</span>
          </div>
        )}
        <div className="wr-chip">
          <span className="wr-chip-value">⚔️ {fmt(rank.warTrophies)}</span>
          <span className="wr-chip-label">{t.worldRank.trophies}</span>
        </div>
      </div>

      {rank.countryRank == null && rank.globalRank == null && (
        <p className="muted small" style={{ margin: '0 0 8px' }}>{t.worldRank.notRanked}</p>
      )}

      {rank.countryTop.length > 0 && rank.countryName && (
        <>
          <p className="muted small" style={{ margin: '4px 0 6px' }}>
            {t.worldRank.topTitle} {rank.countryName}:
          </p>
          <ul className="wr-top">
            {rank.countryTop.map(c => (
              <li key={c.rank} className={`wr-row ${c.isOurClan ? 'wr-row-ours' : ''}`}>
                <span className="wr-row-rank">#{c.rank}</span>
                <span className="wr-row-name">{c.name}</span>
                <span className="wr-row-score">{fmt(c.warTrophies)} ⚔️</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
