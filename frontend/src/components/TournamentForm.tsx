import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Tournament } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

interface Props {
  mode: 'create' | 'edit'
  initial?: Tournament
  onSaved: (t: Tournament) => void
  onCancel: () => void
}

export function TournamentForm({ mode, initial, onSaved, onCancel }: Props) {
  const { t } = useT()
  const [name, setName] = useState(initial?.name ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [prizeInfo, setPrizeInfo] = useState(initial?.prizeInfo ?? '')
  const [clanInviteLink, setClanInviteLink] = useState(initial?.clanInviteLink ?? '')
  const [bestOf, setBestOf] = useState(initial?.bestOf ?? 2)
  const [minParticipants, setMinParticipants] = useState(initial?.minParticipants ?? 2)
  const [maxParticipants, setMaxParticipants] = useState(initial?.maxParticipants ?? 16)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const errorText = (code: string) => {
    switch (code) {
      case 'bad_name': return t.tournament.badName
      case 'bad_link': return t.tournament.badLink
      case 'bad_format': return t.tournament.badFormat
      case 'too_many_active': return t.tournament.tooManyActive
      case 'player_not_linked': return t.tournament.notLinked
      default: return t.tournament.error
    }
  }

  const submit = async () => {
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      const req = {
        name,
        description: description.trim() || undefined,
        prizeInfo: prizeInfo.trim() || undefined,
        clanInviteLink,
        bestOf,
        minParticipants,
        maxParticipants,
      }
      const result = mode === 'create'
        ? await api.createTournament(req)
        : await api.updateTournament(initial!.id, req)
      hapticNotify('success')
      onSaved(result)
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError ? errorText(e.code) : t.tournament.error)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card tournament-form">
      <div className="card-title">{mode === 'create' ? t.tournament.formTitleCreate : t.tournament.formTitleEdit}</div>

      <div className="form-field">
        <label className="muted small">{t.tournament.nameLabel}</label>
        <input
          className="search-input"
          value={name}
          onChange={e => setName(e.target.value)}
          placeholder={t.tournament.namePlaceholder}
          maxLength={80}
        />
      </div>

      <div className="form-field">
        <label className="muted small">{t.tournament.descLabel}</label>
        <textarea
          className="recruit-note"
          value={description}
          onChange={e => setDescription(e.target.value)}
          placeholder={t.tournament.descPlaceholder}
          maxLength={2000}
          rows={3}
        />
      </div>

      <div className="form-field">
        <label className="muted small">{t.tournament.prizeLabel}</label>
        <input
          className="search-input"
          value={prizeInfo}
          onChange={e => setPrizeInfo(e.target.value)}
          placeholder={t.tournament.prizePlaceholder}
          maxLength={500}
        />
      </div>

      <div className="form-field">
        <label className="muted small">{t.tournament.linkLabel}</label>
        <input
          className="search-input"
          value={clanInviteLink}
          onChange={e => setClanInviteLink(e.target.value)}
          placeholder={t.tournament.linkPlaceholder}
          maxLength={300}
        />
        <p className="muted small form-hint">{t.tournament.linkHint}</p>
      </div>

      <div className="form-row">
        <div className="form-field">
          <label className="muted small">{t.tournament.bestOfFieldLabel}</label>
          <select className="rating-select tournament-select" value={bestOf} onChange={e => setBestOf(Number(e.target.value))}>
            <option value={1}>{t.tournament.format1}</option>
            <option value={2}>{t.tournament.format2}</option>
            <option value={3}>{t.tournament.format3}</option>
          </select>
        </div>
        <div className="form-field">
          <label className="muted small">{t.tournament.minParticipantsLabel}</label>
          <input
            className="search-input"
            type="number"
            min={2}
            max={64}
            value={minParticipants}
            onChange={e => setMinParticipants(Number(e.target.value))}
          />
        </div>
        <div className="form-field">
          <label className="muted small">{t.tournament.maxParticipantsLabel}</label>
          <input
            className="search-input"
            type="number"
            min={2}
            max={64}
            value={maxParticipants}
            onChange={e => setMaxParticipants(Number(e.target.value))}
          />
        </div>
      </div>
      <p className="muted small form-hint">{t.tournament.minParticipantsHint}</p>

      {error && <p className="form-error small">{error}</p>}

      <div className="recruit-actions">
        <button className="btn" disabled={busy || !name.trim() || !clanInviteLink.trim()} onClick={submit}>
          {busy ? t.tournament.saving : mode === 'create' ? t.tournament.create : t.tournament.update}
        </button>
        <button className="btn-mini" disabled={busy} onClick={onCancel}>{t.tournament.cancelForm}</button>
      </div>
    </div>
  )
}
