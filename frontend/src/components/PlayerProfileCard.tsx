import { useState, useEffect } from 'react'
import { api } from '../lib/api'
import type { PlayerAnalysis, PlayerCard, PlayerHistory, PlayerProfile } from '../types'
import { fmt } from '../lib/format'
import { useT, type Translations } from '../lib/i18n'

export function PlayerProfileCard({ profile }: { profile: PlayerProfile }) {
  const { t } = useT()
  const [tab, setTab] = useState<'profile' | 'wars'>('profile')
  const [warHistory, setWarHistory] = useState<PlayerHistory | null>(null)
  const [warsState, setWarsState] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')

  useEffect(() => {
    if (tab !== 'wars' || warsState !== 'idle') return
    setWarsState('loading')
    api.getPlayerHistory(profile.playerTag)
      .then(h => { setWarHistory(h); setWarsState('ready') })
      .catch(() => setWarsState('error'))
  }, [tab, warsState, profile.playerTag])

  useEffect(() => {
    setTab('profile')
    setWarHistory(null)
    setWarsState('idle')
  }, [profile.playerTag])

  return (
    <>
      <section className="card" style={{ marginTop: 12 }}>
        <div className="profile-header">
          <div className="profile-avatar">👤</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="profile-name">{profile.name}</div>
            <div className="profile-tag">{profile.playerTag}</div>
            {profile.clanName && (
              <div className="muted small" style={{ marginTop: 2 }}>🏰 {profile.clanName}</div>
            )}
          </div>
          <a
            href={profile.royaleApiUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="muted small"
            style={{ textDecoration: 'none', flexShrink: 0 }}
          >
            {t.search.royaleApi}
          </a>
        </div>

        <div className="profile-stats">
          <div className="profile-stat">
            <div className="profile-stat-value">👑 {profile.expLevel}</div>
            <div className="profile-stat-label">{t.search.level}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value">🏆 {fmt(profile.trophies)}</div>
            <div className="profile-stat-label">{t.search.trophies}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value">⚔️ {fmt(profile.clanWarTrophies)}</div>
            <div className="profile-stat-label">{t.search.warTrophies}</div>
          </div>
          {profile.arenaName && (
            <div className="profile-stat" style={{ gridColumn: '1 / -1' }}>
              <div className="profile-stat-value" style={{ fontSize: 13 }}>{profile.arenaName}</div>
              <div className="profile-stat-label">{t.search.arena}</div>
            </div>
          )}
        </div>

        <div className="profile-tabs">
          <button
            className={`profile-tab ${tab === 'profile' ? 'profile-tab-active' : ''}`}
            onClick={() => setTab('profile')}
          >
            {t.search.tabProfile}
          </button>
          <button
            className={`profile-tab ${tab === 'wars' ? 'profile-tab-active' : ''}`}
            onClick={() => setTab('wars')}
          >
            {t.search.tabWars}
          </button>
        </div>
      </section>

      {tab === 'profile' && (
        <>
          {profile.analysis && <AnalysisCard a={profile.analysis} t={t} />}

          <section className="card" style={{ marginTop: 10 }}>
            <div className="cards-section-title">{t.search.careerStats}</div>
            <div className="profile-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
              <div className="profile-stat">
                <div className="profile-stat-value">⚔️ {fmt(profile.warDayWins)}</div>
                <div className="profile-stat-label">{t.search.warDayWins}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value">👑 {fmt(profile.threeCrownWins)}</div>
                <div className="profile-stat-label">{t.search.threeCrowns}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value">🥇 {fmt(profile.bestTrophies)}</div>
                <div className="profile-stat-label">{t.search.bestTrophies}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value">⚡ {fmt(profile.battleCount)}</div>
                <div className="profile-stat-label">{t.search.battles}</div>
              </div>
              {profile.wins + profile.losses > 0 && (
                <div className="profile-stat">
                  <div className="profile-stat-value">
                    {Math.round(profile.wins * 100 / (profile.wins + profile.losses))}%
                  </div>
                  <div className="profile-stat-label">{t.search.winRate}</div>
                </div>
              )}
              {profile.wins > 0 && (
                <div className="profile-stat">
                  <div className="profile-stat-value" style={{ fontSize: 13 }}>
                    {fmt(profile.wins)} / {fmt(profile.losses)}
                  </div>
                  <div className="profile-stat-label">{t.search.winsLosses}</div>
                </div>
              )}
              {profile.currentWinLoseStreak !== 0 && (
                <div className="profile-stat">
                  <div className="profile-stat-value">
                    {profile.currentWinLoseStreak > 0 ? `🔥 ${profile.currentWinLoseStreak}` : `❄️ ${Math.abs(profile.currentWinLoseStreak)}`}
                  </div>
                  <div className="profile-stat-label">
                    {profile.currentWinLoseStreak > 0 ? t.search.streakWin : t.search.streakLose}
                  </div>
                </div>
              )}
              {profile.currentPathOfLegend && profile.currentPathOfLegend.trophies > 0 && (
                <div className="profile-stat">
                  <div className="profile-stat-value">🏅 {fmt(profile.currentPathOfLegend.trophies)}</div>
                  <div className="profile-stat-label">
                    {t.search.polRank}
                    {profile.currentPathOfLegend.rank > 0 ? ` · #${profile.currentPathOfLegend.rank} ${t.search.polRankSuffix}` : ''}
                  </div>
                </div>
              )}
            </div>
          </section>

          {profile.currentDeck.length > 0 && (
            <section className="card" style={{ marginTop: 10 }}>
              <div className="card-title-row">
                <div className="cards-section-title" style={{ margin: 0 }}>{t.search.currentDeck}</div>
                <span className="muted small">
                  ⌀ {(profile.currentDeck.reduce((s, c) => s + c.level, 0) / profile.currentDeck.length).toFixed(1)}
                  {' / '}{profile.maxCardLevel}
                </span>
              </div>
              <div className="cards-grid">
                {profile.currentDeck.map(card => (
                  <div key={card.name} className="card-chip">
                    <img src={card.iconUrl} alt={card.name} loading="lazy" title={card.name} />
                    <div className={`card-chip-level ${card.level >= card.maxLevel ? 'card-chip-maxed' : ''}`}>
                      {card.level} {t.search.lvl}
                    </div>
                  </div>
                ))}
              </div>
            </section>
          )}

          {profile.weeksPlayed > 0 && (
            <section className="card" style={{ marginTop: 10 }}>
              <div className="cards-section-title">{t.search.warStats}</div>
              <div className="profile-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div className="profile-stat">
                  <div className="profile-stat-value">{profile.weeksPlayed}</div>
                  <div className="profile-stat-label">{t.search.weeks}</div>
                </div>
                <div className="profile-stat">
                  <div className="profile-stat-value">{fmt(profile.totalFame)}</div>
                  <div className="profile-stat-label">{t.search.totalMedals}</div>
                </div>
                <div className="profile-stat">
                  <div className="profile-stat-value">{profile.avgFamePerAttack > 0 ? profile.avgFamePerAttack.toFixed(0) : '—'}</div>
                  <div className="profile-stat-label">{t.search.perAttack}</div>
                </div>
              </div>
            </section>
          )}

          {profile.cards.length > 0 && (
            <CollectionCard cards={profile.cards} maxLevel={profile.maxCardLevel} t={t} />
          )}
        </>
      )}

      {tab === 'wars' && (
        <section className="card" style={{ marginTop: 10 }}>
          <div className="cards-section-title">{t.playerModal.pastWars}</div>

          {warsState === 'loading' && (
            <p className="muted small">{t.playerModal.loadingHistory}</p>
          )}
          {warsState === 'error' && (
            <p className="muted small">{t.playerModal.historyError}</p>
          )}
          {warsState === 'ready' && warHistory && warHistory.weeks.length === 0 && (
            <p className="muted small">{t.playerModal.noHistory}</p>
          )}
          {warsState === 'ready' && warHistory && warHistory.weeks.length > 0 && (
            <ul className="history-week-list">
              {warHistory.weeks.map(w => (
                <li key={`${w.seasonId}-${w.sectionIndex}-${w.clanTag}`} className="history-week-row">
                  <span className="history-week-badge">
                    {w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}
                  </span>
                  <div className="history-week-info">
                    <span className="history-week-clan">{w.clanName}</span>
                    <span className="muted small">
                      {t.playerModal.season} {w.seasonId} · ⚔️ {w.decksUsed} · ⚡ {w.avgFamePerAttack > 0 ? Math.round(w.avgFamePerAttack) : '—'}
                    </span>
                  </div>
                  <span className="history-week-fame">{fmt(w.fame)} 🏅</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </>
  )
}

/* ---------- Разбор игрока (Pro) ---------- */

const TIER_CLASS: Record<string, string> = {
  top: 'tier-top', strong: 'tier-strong', mid: 'tier-mid', developing: 'tier-dev',
}

function AnalysisCard({ a, t }: { a: PlayerAnalysis; t: Translations }) {
  // Заполнение шкалы — насколько колода близка к потолку уровней
  const pct = Math.min(100, Math.round((a.avgDeckLevel / a.maxCardLevel) * 100))

  return (
    <section className={`card analysis-card ${TIER_CLASS[a.tier] ?? ''}`} style={{ marginTop: 10 }}>
      <div className="card-title-row">
        <div className="cards-section-title" style={{ margin: 0 }}>{t.search.analysisTitle}</div>
        <span className={`tier-badge ${TIER_CLASS[a.tier] ?? ''}`}>
          {t.search.analysisTiers[a.tier] ?? a.tier}
        </span>
      </div>

      <div className="analysis-main">
        <span className="analysis-level">{a.avgDeckLevel}</span>
        <span className="muted small">/ {a.maxCardLevel}</span>
        <span className="analysis-level-label">{t.search.avgDeckLevel}</span>
      </div>

      <div className="analysis-track">
        <div className="analysis-fill" style={{ width: `${Math.max(4, pct)}%` }} />
      </div>

      <p className="analysis-verdict">{a.verdict}</p>

      <p className="analysis-fit">
        <span className="muted">{t.search.fitsClan}</span> <b>{a.recommendedClanLevel}</b>
      </p>

      {a.notes.length > 0 && (
        <ul className="analysis-notes">
          {a.notes.map((n, i) => <li key={i} className="muted small">• {n}</li>)}
        </ul>
      )}
    </section>
  )
}

/* ---------- Коллекция: распределение по уровням ---------- */

function CollectionCard({ cards, maxLevel, t }: {
  cards: PlayerCard[]; maxLevel: number; t: Translations
}) {
  const [open, setOpen] = useState(false)

  // Считаем, сколько карт на каждом уровне — как на RoyaleAPI: видно силу коллекции,
  // а не просто её размер. Показываем сверху вниз, начиная с максимума.
  const byLevel = new Map<number, number>()
  for (const c of cards) byLevel.set(c.level, (byLevel.get(c.level) ?? 0) + 1)
  const levels = [...byLevel.entries()].sort((a, b) => b[0] - a[0]).slice(0, 6)

  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="card-title-row">
        <div className="cards-section-title" style={{ margin: 0 }}>
          {t.search.collection} ({cards.length})
        </div>
        <span className="muted small">{t.search.byLevel}</span>
      </div>

      <div className="lvl-rows">
        {levels.map(([lvl, count]) => (
          <div key={lvl} className="lvl-row">
            <span className={`lvl-badge ${lvl >= maxLevel ? 'lvl-badge-max' : ''}`}>{lvl}</span>
            <div className="lvl-track">
              <div className="lvl-fill" style={{ width: `${Math.round(count * 100 / cards.length)}%` }} />
            </div>
            <span className="lvl-count muted small">{count}</span>
          </div>
        ))}
      </div>

      <button className="btn-mini" style={{ width: '100%', marginTop: 10 }} onClick={() => setOpen(o => !o)}>
        {open ? t.search.hideCards : t.search.showAllCards}
      </button>

      {open && (
        <div className="cards-grid" style={{ marginTop: 10 }}>
          {cards.map(card => (
            <div key={card.name} className="card-chip">
              <img src={card.iconUrl} alt={card.name} loading="lazy" title={card.name} />
              <div className={`card-chip-level ${card.level >= card.maxLevel ? 'card-chip-maxed' : ''}`}>
                {card.level}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
