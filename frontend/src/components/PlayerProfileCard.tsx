import { useState, useEffect } from 'react'
import { api } from '../lib/api'
import type { PlayerAnalysis, PlayerCard, PlayerHistory, PlayerProfile } from '../types'
import { fmt } from '../lib/format'
import { useT, type Translations } from '../lib/i18n'
import { Icon, type IconName } from './ui/Icon'
import { SectionHead } from './ui/Section'

export function PlayerProfileCard({ profile, embedded = false }: {
  profile: PlayerProfile
  /**
   * На вкладке «Я» история войн и недельная статистика уже нарисованы графиками выше,
   * поэтому здесь их не повторяем — иначе один и тот же факт стоит на экране дважды.
   */
  embedded?: boolean
}) {
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
          <div className="profile-avatar pl-avatar"><Icon name="user" size={26} /></div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="profile-name">{profile.name}</div>
            <div className="profile-tag">{profile.playerTag}</div>
            {profile.clanName && (
              <div className="muted small pl-inline" style={{ marginTop: 2 }}><Icon name="castle" size={13} /> {profile.clanName}</div>
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
            <div className="profile-stat-value pl-stat-val"><Icon name="crown" size={15} /> {profile.expLevel}</div>
            <div className="profile-stat-label">{t.search.level}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value pl-stat-val"><Icon name="trophy" size={15} /> {fmt(profile.trophies)}</div>
            <div className="profile-stat-label">{t.search.trophies}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value pl-stat-val"><Icon name="swords" size={15} /> {fmt(profile.clanWarTrophies)}</div>
            <div className="profile-stat-label">{t.search.warTrophies}</div>
          </div>
          {profile.arenaName && (
            <div className="profile-stat" style={{ gridColumn: '1 / -1' }}>
              <div className="profile-stat-value" style={{ fontSize: 13 }}>{profile.arenaName}</div>
              <div className="profile-stat-label">{t.search.arena}</div>
            </div>
          )}
        </div>

        {!embedded && (
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
        )}
      </section>

      {tab === 'profile' && (
        <>
          {profile.analysis && <AnalysisCard a={profile.analysis} t={t} />}

          <section className="card" style={{ marginTop: 10 }}>
            <SectionHead icon="chart" tone="blue" title={t.search.careerStats} />
            <div className="profile-stats" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
              <div className="profile-stat">
                <div className="profile-stat-value pl-stat-val"><Icon name="swords" size={15} /> {fmt(profile.warDayWins)}</div>
                <div className="profile-stat-label">{t.search.warDayWins}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value pl-stat-val"><Icon name="crown" size={15} /> {fmt(profile.threeCrownWins)}</div>
                <div className="profile-stat-label">{t.search.threeCrowns}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value pl-stat-val"><Icon name="medal" size={15} /> {fmt(profile.bestTrophies)}</div>
                <div className="profile-stat-label">{t.search.bestTrophies}</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value pl-stat-val"><Icon name="bolt" size={15} /> {fmt(profile.battleCount)}</div>
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
                  <div className="profile-stat-value pl-stat-val">
                    <Icon name={profile.currentWinLoseStreak > 0 ? 'flame' : 'snowflake'} size={15} /> {Math.abs(profile.currentWinLoseStreak)}
                  </div>
                  <div className="profile-stat-label">
                    {profile.currentWinLoseStreak > 0 ? t.search.streakWin : t.search.streakLose}
                  </div>
                </div>
              )}
              {profile.currentPathOfLegend && profile.currentPathOfLegend.trophies > 0 && (
                <div className="profile-stat">
                  <div className="profile-stat-value pl-stat-val"><Icon name="medal" size={15} /> {fmt(profile.currentPathOfLegend.trophies)}</div>
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
              <SectionHead icon="cards" tone="violet" title={t.search.currentDeck} aside={<>
                ⌀ {(profile.currentDeck.reduce((s, c) => s + c.level, 0) / profile.currentDeck.length).toFixed(1)}
                {' / '}{profile.maxCardLevel}
              </>} />
              <div className="cards-grid">
                {profile.currentDeck.map(card => <CardChip key={card.name} card={card} />)}
              </div>
            </section>
          )}

          {!embedded && profile.weeksPlayed > 0 && (
            <section className="card" style={{ marginTop: 10 }}>
              <SectionHead icon="swords" tone="orange" title={t.search.warStats} />
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

      {!embedded && tab === 'wars' && (
        <section className="card" style={{ marginTop: 10 }}>
          <SectionHead icon="history" tone="blue" title={t.playerModal.pastWars} />

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
                    {w.isColosseum ? <Icon name="columns" size={14} /> : `W${w.sectionIndex + 1}`}
                  </span>
                  <div className="history-week-info">
                    <span className="history-week-clan">{w.clanName}</span>
                    <span className="muted small">
                      {t.playerModal.season} {w.seasonId} · <Icon name="swords" size={11} /> {w.decksUsed} · <Icon name="bolt" size={11} /> {w.avgFamePerAttack > 0 ? Math.round(w.avgFamePerAttack) : '—'}
                    </span>
                  </div>
                  <span className="history-week-fame pl-inline">{fmt(w.fame)} <Icon name="medal" size={14} /></span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </>
  )
}

/* ---------- Разбор игрока: в клан какой лиги идти ---------- */

const LEAGUE_CLASS: Record<string, string> = {
  bronze: 'league-bronze', silver: 'league-silver', gold: 'league-gold', legendary: 'league-legend',
}
const LEAGUE_ICON: Record<string, IconName> = {
  bronze: 'medal', silver: 'medal', gold: 'medal', legendary: 'crown',
}

/**
 * Главный ответ карточки — лига клана, в который человеку идти прямо сейчас.
 * Раньше здесь первым делом стояло «0 из 4 полных колод», и для новичка это читалось
 * как отказ. Полные колоды остались, но ниже: это цель, а не приговор.
 */
function AnalysisCard({ a, t }: { a: PlayerAnalysis; t: Translations }) {
  const cls = LEAGUE_CLASS[a.league] ?? ''
  const leagueName = t.search.leagues[a.league] ?? a.league

  // Что делать дальше — одна конкретная задача, а не список претензий
  const nextGoal = a.nextLeague === null
    ? t.search.leagueTop
    : a.nextLeagueMaxedCards != null
      ? `${t.search.nextLeaguePrefix} ${t.search.leagues[a.nextLeague]} — ${t.search.needMaxedCards(a.nextLeagueMaxedCards, a.maxCardLevel)}`
      : `${t.search.nextLeaguePrefix} ${t.search.leagues[a.nextLeague]} — ${t.search.needWarLevel(a.nextLeagueWarLevel ?? 0)}`

  return (
    <section className={`card analysis-card ${cls}`} style={{ marginTop: 10 }}>
      <SectionHead icon="target" tone="gold" title={t.search.analysisTitle}
        aside={<span className={`tier-badge pl-inline ${cls}`}><Icon name={LEAGUE_ICON[a.league] ?? 'medal'} size={13} /> {leagueName}</span>} />

      <div className="league-hero">
        <span className={`league-hero-icon pl-league-icon pl-league-${a.league}`}><Icon name={LEAGUE_ICON[a.league] ?? 'medal'} size={26} /></span>
        <div className="league-hero-text">
          <span className="league-hero-name">{leagueName}</span>
          <span className="muted small">{t.search.leagueHint}</span>
        </div>
      </div>

      <p className="analysis-verdict">{t.search.leagueVerdict[a.league]}</p>
      <p className="analysis-next pl-inline"><Icon name="target" size={15} /> {nextGoal}</p>

      <div className="analysis-metrics">
        <div className="analysis-metric">
          <span className="analysis-metric-value">{a.warLevel}</span>
          <span className="analysis-metric-label">{t.search.warLevel}</span>
        </div>
        <div className="analysis-metric">
          <span className="analysis-metric-value">{a.fullDecks}/{a.decksNeeded}</span>
          <span className="analysis-metric-label">{t.search.fullDecks}</span>
        </div>
        <div className="analysis-metric">
          <span className="analysis-metric-value">{a.maxedTotal}</span>
          <span className="analysis-metric-label">{t.search.onMaxLevel} {a.maxCardLevel}</span>
        </div>
      </div>

      {/* Четыре ячейки = четыре боя военного дня: видно, куда расти */}
      <div className="deck-slots">
        {Array.from({ length: a.decksNeeded }, (_, i) => (
          <div key={i} className={`deck-slot ${i < a.fullDecks ? 'deck-slot-on' : ''}`}>
            {i < a.fullDecks ? <Icon name="cards" size={16} /> : '·'}
          </div>
        ))}
      </div>

      <ul className="analysis-notes">
        {a.evoAvailable > 0 && (
          <li className="muted small">• {t.search.noteEvo(a.evoUnlocked, a.evoAvailable)}</li>
        )}
        {a.deckSize > 0 && (
          <li className="muted small">• {t.search.noteDeck(a.avgDeckLevel, a.maxedInDeck, a.deckSize)}</li>
        )}
        {a.winRate !== null && (
          <li className="muted small">
            • {t.search.noteWinRate(a.winRate)}{a.winRate >= 55 ? ` — ${t.search.aboveAverage}` : ''}
          </li>
        )}
        {a.warDayWins > 0 && <li className="muted small">• {t.search.noteWarWins(a.warDayWins)}</li>}
        {a.weeksPlayed > 0 && a.avgFamePerAttack > 0 && (
          <li className="muted small">
            • {t.search.noteInOurClans(a.weeksPlayed, Math.round(a.avgFamePerAttack))}
          </li>
        )}
      </ul>
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
      <SectionHead icon="cards" tone="blue" title={<>{t.search.collection} ({cards.length})</>}
        aside={t.search.byLevel} />

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
          {cards.map(card => <CardChip key={card.name} card={card} />)}
        </div>
      )}
    </section>
  )
}

/* ---------- Карточка карты ---------- */

function CardChip({ card }: { card: PlayerCard }) {
  // У эволюционной карты своя иконка — показываем её, чтобы эво было видно сразу,
  // как в игре. Значок ⚡ остаётся на случай, если иконки эво в API не оказалось.
  const unlockedEvo = card.evolutionLevel > 0
  const icon = unlockedEvo && card.evoIconUrl ? card.evoIconUrl : card.iconUrl

  return (
    <div className={`card-chip ${unlockedEvo ? 'card-chip-evo' : ''}`}>
      <img src={icon} alt={card.name} loading="lazy" title={card.name} />
      {unlockedEvo && <span className="card-evo-mark"><Icon name="bolt" size={11} /></span>}
      <div className={`card-chip-level ${card.level >= card.maxLevel ? 'card-chip-maxed' : ''}`}>
        {card.level}
      </div>
    </div>
  )
}
