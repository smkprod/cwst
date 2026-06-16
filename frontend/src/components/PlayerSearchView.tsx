import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { PlayerProfile } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { PlayerProfileCard } from './PlayerProfileCard'

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
