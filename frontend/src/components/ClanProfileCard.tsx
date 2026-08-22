import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { ClanOverview, WarLogWeek } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, roleLabel, type Translations } from '../lib/i18n'
import { WarLogWeeks } from './WarLogCard'

const ROLE_ICON: Record<string, string> = { leader: '👑', coLeader: '⚜️', elder: '⭐' }

export function ClanProfileCard({ clan }: { clan: ClanOverview }) {
  const { t } = useT()
  const members = clan.members ?? []

  return (
    <>
      <section className="card" style={{ marginTop: 12 }}>
        <div className="profile-header">
          <div className="profile-avatar">🏰</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="profile-name">{clan.clanName ?? clan.clanTag}</div>
            <div className="profile-tag">{clan.clanTag}</div>
            {clan.countryName && (
              <div className="muted small" style={{ marginTop: 2 }}>🌍 {clan.countryName}</div>
            )}
          </div>
          {clan.connected && <span className="clan-connected">{t.search.clanConnected}</span>}
        </div>

        <div className="profile-stats">
          <div className="profile-stat">
            <div className="profile-stat-value">⚔️ {fmt(clan.warTrophies)}</div>
            <div className="profile-stat-label">{t.search.warTrophies}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value">🏆 {fmt(clan.clanScore)}</div>
            <div className="profile-stat-label">{t.search.clanScore}</div>
          </div>
          <div className="profile-stat">
            <div className="profile-stat-value">👥 {clan.memberCount ?? '—'}/50</div>
            <div className="profile-stat-label">{t.search.clanMembers}</div>
          </div>
        </div>

        <div className="clan-meta muted small">
          {clan.type && <span>{t.search.clanType[clan.type] ?? clan.type}</span>}
          {clan.requiredTrophies > 0 && <span>· {t.search.clanRequired}: 🏆 {fmt(clan.requiredTrophies)}</span>}
          {clan.donationsPerWeek > 0 && <span>· {t.search.clanDonations}: {fmt(clan.donationsPerWeek)}</span>}
        </div>

        {(clan.countryRank != null || clan.globalRank != null) && (
          <p className="muted small" style={{ margin: '8px 0 0' }}>
            {clan.countryRank != null && `${t.search.clanCountryRank}: #${clan.countryRank}`}
            {clan.countryRank != null && clan.globalRank != null && ' · '}
            {clan.globalRank != null && `${t.search.clanWorldRank}: #${clan.globalRank}`}
          </p>
        )}

        {clan.description && (
          <p className="clan-description">{clan.description}</p>
        )}
      </section>

      {members.length > 0 && <MembersCard clan={clan} t={t} />}

      <ClanWarsCard clanTag={clan.clanTag} t={t} />
    </>
  )
}

/** Состав клана. Свёрнут до десятки: полсотни строк сразу — это стена. */
function MembersCard({ clan, t }: { clan: ClanOverview; t: Translations }) {
  const [all, setAll] = useState(false)
  const members = clan.members ?? []
  const shown = all ? members : members.slice(0, 10)

  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="card-title-row">
        <div className="cards-section-title" style={{ margin: 0 }}>{t.search.clanRoster}</div>
        <span className="muted small">{members.length}</span>
      </div>

      <ul className="clan-member-list">
        {shown.map(m => {
          const role = roleLabel(m.role, t)
          return (
            <li key={m.playerTag} className="clan-member-row">
              {role && (
                <span className={`role-badge role-${m.role}`}>{ROLE_ICON[m.role]} {role}</span>
              )}
              <span className="clan-member-name">{m.name}</span>
              <span className="clan-member-trophies">🏆 {fmt(m.trophies)}</span>
            </li>
          )
        })}
      </ul>

      {members.length > shown.length && (
        <button className="btn-mini" style={{ width: '100%', marginTop: 10 }}
                onClick={() => { haptic('light'); setAll(true) }}>
          {t.search.clanShowAll} ({members.length})
        </button>
      )}
    </section>
  )
}

/** Прошлые войны клана — тот же официальный журнал, что и в гонке. */
function ClanWarsCard({ clanTag, t }: { clanTag: string; t: Translations }) {
  const [weeks, setWeeks] = useState<WarLogWeek[] | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    let alive = true
    setState('loading')
    api.getClanWarLog(clanTag)
      .then(log => { if (alive) { setWeeks(log.weeks); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [clanTag])

  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="cards-section-title">{t.warlog.pastWars}</div>
      {state === 'loading' && <p className="muted small">{t.warlog.loading}</p>}
      {state === 'error' && <p className="muted small">{t.warlog.error}</p>}
      {state === 'ready' && weeks && weeks.length === 0 && (
        <p className="muted small">{t.warlog.empty}</p>
      )}
      {state === 'ready' && weeks && weeks.length > 0 && <WarLogWeeks weeks={weeks} />}
    </section>
  )
}
