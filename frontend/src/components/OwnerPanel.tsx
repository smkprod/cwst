import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { OwnerClan, OwnerStats } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; clans: OwnerClan[]; stats: OwnerStats }

export function OwnerPanel() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [busy, setBusy] = useState<number | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<number | null>(null)
  const { t } = useT()

  const load = useCallback(() => {
    Promise.all([api.ownerGetClans(), api.ownerGetStats()])
      .then(([clans, stats]) => setState({ kind: 'ready', clans, stats }))
      .catch(() => setState({ kind: 'error' }))
  }, [])

  useEffect(load, [load])

  const setPlan = async (clanId: number, tier: 'pro' | 'free', days?: number) => {
    haptic('medium')
    setBusy(clanId)
    try {
      await api.ownerSetPlan(clanId, tier, days)
      hapticNotify('success')
      load()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(null)
    }
  }

  const deleteClan = async (clanId: number) => {
    haptic('medium')
    if (confirmDelete !== clanId) {
      setConfirmDelete(clanId)
      return
    }
    setBusy(clanId)
    try {
      await api.ownerDeleteClan(clanId)
      hapticNotify('success')
      load()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(null)
      setConfirmDelete(null)
    }
  }

  if (state.kind === 'loading') return <div className="center"><div className="spinner" /></div>
  if (state.kind === 'error') return <p className="center muted">{t.owner.error}</p>

  const { stats } = state
  const conversionPct = stats.totalLinkedUsers > 0
    ? Math.round(stats.usersWithClan * 100 / stats.totalLinkedUsers)
    : 0

  return (
    <div>
      <h2 className="section-title">{t.owner.title}</h2>

      {/* Воронка */}
      <div className="card owner-stats-card">
        <p className="owner-stats-title">📊 Воронка</p>
        <div className="owner-stats-grid">
          <div className="owner-stat">
            <span className="owner-stat-value">{stats.totalLinkedUsers}</span>
            <span className="owner-stat-label">Отправили тег</span>
          </div>
          <div className="owner-stat">
            <span className="owner-stat-value">{stats.usersWithClan}</span>
            <span className="owner-stat-label">С кланом</span>
          </div>
          <div className="owner-stat">
            <span className="owner-stat-value">{stats.usersWithoutClan}</span>
            <span className="owner-stat-label">Без клана</span>
          </div>
          <div className="owner-stat">
            <span className="owner-stat-value">{conversionPct}%</span>
            <span className="owner-stat-label">Конверсия</span>
          </div>
        </div>
        <p className="owner-stats-sub muted small">
          {stats.totalClans} кланов · {stats.proClans} PRO
        </p>
      </div>

      <p className="muted small owner-hint">
        {t.owner.clans} {state.clans.length} · {t.owner.pro} {state.clans.filter(c => c.plan === 'pro').length}
      </p>

      <ul className="owner-list">
        {state.clans.map(c => (
          <li key={c.id} className="card owner-card">
            <div className="owner-card-top">
              <div className="owner-card-info">
                <span className="owner-clan-name">{c.name}</span>
                <span className="muted small">{c.clanTag} · {t.owner.linked} {c.linkedPlayers}</span>
              </div>
              <span className={`plan-badge ${c.plan === 'pro' ? 'plan-pro' : 'plan-free'}`}>
                {c.plan === 'pro' ? 'PRO' : 'FREE'}
              </span>
            </div>

            {c.plan === 'pro' && c.planExpiresAtUtc && (
              <p className="muted small owner-expiry">
                {t.owner.expiry} {new Date(c.planExpiresAtUtc).toLocaleDateString(t.dateLocale)}
              </p>
            )}

            <div className="owner-actions">
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro', 30)}>{t.owner.pro30}</button>
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro', 90)}>{t.owner.pro90}</button>
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro')}>{t.owner.proInf}</button>
              <button className="btn-mini btn-mini-danger" disabled={busy === c.id} onClick={() => setPlan(c.id, 'free')}>{t.owner.free}</button>
              <button
                className="btn-mini btn-mini-danger owner-delete"
                disabled={busy === c.id}
                onClick={() => deleteClan(c.id)}
                onBlur={() => setConfirmDelete(null)}
              >
                {confirmDelete === c.id ? t.owner.confirmDelete : t.owner.delete}
              </button>
            </div>
            {confirmDelete === c.id && (
              <p className="muted small owner-delete-hint">{t.owner.deleteHint}</p>
            )}
          </li>
        ))}
      </ul>

      {state.clans.length === 0 && (
        <p className="center muted">{t.owner.noClans}</p>
      )}
    </div>
  )
}
