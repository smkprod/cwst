import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ClanStatus } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'

interface Props {
  onSuccess: (playerTag: string, clanTag: string, data: ClanStatus) => void
}

type S = { kind: 'idle' } | { kind: 'loading' } | { kind: 'error'; message: string }

export function GuestEntry({ onSuccess }: Props) {
  const [tag, setTag] = useState('')
  const [s, setS] = useState<S>({ kind: 'idle' })
  const { t } = useT()

  const enter = async () => {
    const trimmed = tag.trim().replace(/^#+/, '').toUpperCase()
    if (!trimmed || s.kind === 'loading') return
    haptic('light')
    setS({ kind: 'loading' })

    try {
      const profile = await api.getPlayerProfile('#' + trimmed)
      if (!profile.clanTag) {
        setS({ kind: 'error', message: t.guest.notInClan })
        return
      }
      const clanStatus = await api.getClanStatus(profile.clanTag)
      const playerTag = '#' + trimmed
      localStorage.setItem('guestPlayerTag', playerTag)
      localStorage.setItem('guestClanTag', profile.clanTag)
      onSuccess(playerTag, profile.clanTag, clanStatus)
    } catch (e) {
      if (e instanceof ApiError && e.code === 'player_not_found') {
        setS({ kind: 'error', message: t.guest.notFound })
      } else if (e instanceof ApiError && e.code === 'clan_not_found') {
        setS({ kind: 'error', message: t.guest.clanNotRegistered })
      } else {
        setS({ kind: 'error', message: t.networkError })
      }
    }
  }

  return (
    <div className="center">
      <div className="lang-switcher-bar">
        <LangSwitcher />
      </div>
      <p style={{ fontSize: 40, margin: 0 }}>⚔️</p>
      <h2 style={{ margin: '12px 0 4px' }}>{t.guest.title}</h2>
      <p className="muted small" style={{ maxWidth: 280, textAlign: 'center', marginBottom: 20 }}>
        {t.guest.hint}
      </p>
      <div className="search-row" style={{ maxWidth: 320 }}>
        <input
          className="search-input"
          type="text"
          placeholder={t.guest.placeholder}
          value={tag}
          onChange={e => setTag(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && enter()}
          autoCapitalize="characters"
          spellCheck={false}
        />
        <button className="btn search-btn" onClick={enter} disabled={s.kind === 'loading'}>
          {s.kind === 'loading' ? '…' : t.guest.enter}
        </button>
      </div>
      {s.kind === 'error' && (
        <p className="small" style={{ color: '#ff7a76', marginTop: 12, maxWidth: 280, textAlign: 'center' }}>
          {s.message}
        </p>
      )}
      <p className="muted small" style={{ marginTop: 24, maxWidth: 280, textAlign: 'center' }}>
        {t.guest.linkHint}
      </p>
    </div>
  )
}
