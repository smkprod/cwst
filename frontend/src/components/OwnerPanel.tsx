import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { OwnerClan } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; clans: OwnerClan[] }

/** Панель владельца сервиса: выдача тарифов кланам. Видна только аккаунту из Owner:TelegramUserId. */
export function OwnerPanel() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [busy, setBusy] = useState<number | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<number | null>(null)

  const load = useCallback(() => {
    api.ownerGetClans()
      .then(clans => setState({ kind: 'ready', clans }))
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

  // Удаление в два тапа: первый — «точно?», второй — удаляем
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
  if (state.kind === 'error') return <p className="center muted">Нет доступа или ошибка сети.</p>

  return (
    <div>
      <h2 className="section-title">⚙️ Панель владельца</h2>
      <p className="muted small owner-hint">
        Кланов: {state.clans.length} · Pro: {state.clans.filter(c => c.plan === 'pro').length}
      </p>

      <ul className="owner-list">
        {state.clans.map(c => (
          <li key={c.id} className="card owner-card">
            <div className="owner-card-top">
              <div className="owner-card-info">
                <span className="owner-clan-name">{c.name}</span>
                <span className="muted small">{c.clanTag} · TG-привязок: {c.linkedPlayers}</span>
              </div>
              <span className={`plan-badge ${c.plan === 'pro' ? 'plan-pro' : 'plan-free'}`}>
                {c.plan === 'pro' ? 'PRO' : 'FREE'}
              </span>
            </div>

            {c.plan === 'pro' && c.planExpiresAtUtc && (
              <p className="muted small owner-expiry">
                до {new Date(c.planExpiresAtUtc).toLocaleDateString('ru-RU')}
              </p>
            )}

            <div className="owner-actions">
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro', 30)}>Pro 30д</button>
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro', 90)}>Pro 90д</button>
              <button className="btn-mini" disabled={busy === c.id} onClick={() => setPlan(c.id, 'pro')}>Pro ∞</button>
              <button className="btn-mini btn-mini-danger" disabled={busy === c.id} onClick={() => setPlan(c.id, 'free')}>Free</button>
              <button
                className="btn-mini btn-mini-danger owner-delete"
                disabled={busy === c.id}
                onClick={() => deleteClan(c.id)}
                onBlur={() => setConfirmDelete(null)}
              >
                {confirmDelete === c.id ? '❗ Точно удалить?' : '🗑 Удалить'}
              </button>
            </div>
            {confirmDelete === c.id && (
              <p className="muted small owner-delete-hint">
                Удалит клан, все TG-привязки игроков и историю войн. Нажми ещё раз для подтверждения.
              </p>
            )}
          </li>
        ))}
      </ul>

      {state.clans.length === 0 && (
        <p className="center muted">Пока ни одного клана — кланы появляются после /setup в группе.</p>
      )}
    </div>
  )
}
