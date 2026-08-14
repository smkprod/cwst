import { useState, useEffect } from 'react'
import { api } from '../lib/api'
import type { RecruitmentStatus } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

export function RecruitToggle() {
  const { t } = useT()
  const [status, setStatus] = useState<RecruitmentStatus | null>(null)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState(false)

  useEffect(() => {
    api.getRecruitmentStatus().then(s => {
      setStatus(s)
      setNote(s.note ?? '')
    }).catch(() => {})
  }, [])

  const activate = async () => {
    if (!editing) { setEditing(true); return }
    haptic('medium')
    setBusy(true)
    try {
      await api.applyRecruitment(note)
      setStatus({ isActive: true, note: note || null })
      setEditing(false)
      hapticNotify('success')
    } catch { hapticNotify('error') }
    finally { setBusy(false) }
  }

  const deactivate = async () => {
    haptic('medium')
    setBusy(true)
    try {
      await api.withdrawRecruitment()
      setStatus({ isActive: false, note: status?.note ?? null })
      setEditing(false)
      hapticNotify('success')
    } catch { hapticNotify('error') }
    finally { setBusy(false) }
  }

  if (status === null) return null

  return (
    <section className="card recruit-card">
      <div className="card-title">{t.recruit.myTitle}</div>
      <p className="muted small" style={{ margin: '4px 0 10px' }}>{t.recruit.myHint}</p>

      {status.isActive && !editing && (
        <p className="recruit-active-status">{t.recruit.activeStatus}</p>
      )}
      {!status.isActive && !editing && (
        <p className="muted small">{t.recruit.inactiveStatus}</p>
      )}

      {editing && (
        <div className="recruit-note-wrap">
          <label className="muted small">{t.recruit.noteLabel}</label>
          <textarea
            className="recruit-note"
            placeholder={t.recruit.notePlaceholder}
            value={note}
            onChange={e => setNote(e.target.value)}
            maxLength={500}
            rows={3}
          />
        </div>
      )}

      <div className="recruit-actions">
        {!status.isActive ? (
          <button className="btn" disabled={busy} onClick={activate}>
            {busy ? t.recruit.saving : editing ? t.recruit.activate : t.recruit.activate}
          </button>
        ) : (
          <>
            {!editing && (
              <button className="btn" disabled={busy} onClick={() => setEditing(true)}>
                {t.recruit.activate}
              </button>
            )}
            {editing && (
              <button className="btn" disabled={busy} onClick={activate}>
                {busy ? t.recruit.saving : t.recruit.activate}
              </button>
            )}
            <button className="btn-mini btn-mini-danger" disabled={busy} onClick={deactivate}>
              {t.recruit.deactivate}
            </button>
          </>
        )}
      </div>
    </section>
  )
}
