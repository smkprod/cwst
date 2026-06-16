import { useState, useEffect } from 'react'
import { api, ApiError } from '../lib/api'
import type { PlayerHistory, PlayerProfile } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

type SearchState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: PlayerProfile }

export function PlayerSearchView() {
  const [tag, setTag] = useState('')
  const [state, setState] = useState<SearchState>({ kind: 'idle' })
  const { t } = useT()

  const search = async () => {
    const trimmed = tag.trim().replace(/^#+/, '').toUpperCase()
    if (!trimmed) return
    haptic('light')
    setState({ kind: 'loading' })
    try {
      const data = await api.getPlayerProfile('#' + trimmed)
      setState({ kind: 'ready', data })
    } catch (e) {
      setState({
        kind: 'error',
        message: e instanceof ApiError ? e.message : t.search.notFound,
      })
    }
  }

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') search()
  }

  return (
    <div>
      <section className="card">
        <div className="card-title-row">
          <div className="card-title">{t.search.title}</div>
        </div>
        <p className="muted small" style={{ margin: '0 0 12px' }}>{t.search.hint}</p>
        <div className="search-row">
          <input
            className="search-input"
            type="text"
            placeholder={t.search.placeholder}
            value={tag}
            onChange={e => setTag(e.target.value)}
            onKeyDown={onKey}
            autoCapitalize="characters"
            spellCheck={false}
          />
          <button className="btn search-btn" onClick={search} disabled={state.kind === 'loading'}>
            {state.kind === 'loading' ? t.search.finding : t.search.find}
          </button>
        </div>
      </section>

      {state.kind === 'loading' && (
        <div className="center" style={{ marginTop: 24 }}>
          <div className="spinner" />
        </div>
      )}

      {state.kind === 'error' && (
        <p className="center muted" style={{ marginTop: 16 }}>{state.message}</p>
      )}

      {state.kind === 'ready' && <PlayerProfileCard profile={state.data} />}
    </div>
  )
}

function PlayerProfileCard({ profile }: { profile: PlayerProfile }) {
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

  // Reset when profile changes
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

        {/* Вкладки */}
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

      {/* Профиль: war stats + cards */}
      {tab === 'profile' && (
        <>
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
            <section className="card" style={{ marginTop: 10 }}>
              <div className="cards-section-title">{t.search.cards} ({profile.cards.length})</div>
              <div className="cards-grid">
                {profile.cards.map(card => (
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
        </>
      )}

      {/* Вкладка: история КВ */}
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
