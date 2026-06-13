import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { PlayerHistory } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'

type SearchState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: PlayerHistory }

export function PlayerSearchView() {
  const [tag, setTag] = useState('')
  const [state, setState] = useState<SearchState>({ kind: 'idle' })

  const search = async () => {
    const trimmed = tag.trim().replace(/^#+/, '').toUpperCase()
    if (!trimmed) return
    haptic('light')
    setState({ kind: 'loading' })
    try {
      const data = await api.getPlayerHistory('#' + trimmed)
      setState({ kind: 'ready', data })
    } catch (e) {
      setState({
        kind: 'error',
        message: e instanceof ApiError ? e.message : 'Игрок не найден или нет истории',
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
          <div className="card-title">🔍 Поиск игрока</div>
        </div>
        <p className="muted small" style={{ margin: '0 0 12px' }}>
          Введи тег игрока (например, #ABC123) чтобы посмотреть историю войн.
        </p>
        <div className="search-row">
          <input
            className="search-input"
            type="text"
            placeholder="#ABC123"
            value={tag}
            onChange={e => setTag(e.target.value)}
            onKeyDown={onKey}
            autoCapitalize="characters"
            spellCheck={false}
          />
          <button className="btn search-btn" onClick={search} disabled={state.kind === 'loading'}>
            {state.kind === 'loading' ? '…' : 'Найти'}
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

      {state.kind === 'ready' && (
        <section className="card" style={{ marginTop: 12 }}>
          <div className="card-title-row">
            <div className="card-title">👤 {state.data.playerTag}</div>
            <a
              href={state.data.royaleApiUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="muted small"
              style={{ textDecoration: 'none' }}
            >
              RoyaleAPI ↗
            </a>
          </div>

          {state.data.weeks.length === 0 ? (
            <p className="muted small">Истории войн не найдено.</p>
          ) : (
            <ul className="warlog-list" style={{ marginTop: 8 }}>
              {state.data.weeks.map(w => (
                <li
                  key={`${w.seasonId}-${w.sectionIndex}`}
                  className="search-week-row"
                >
                  <span className="history-week-badge">
                    {w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}
                  </span>
                  <span className="warlog-standing-name">
                    {w.clanName}
                    <span className="muted small" style={{ display: 'block' }}>сезон {w.seasonId}</span>
                  </span>
                  <span className="muted small">{w.decksUsed > 0 ? `${w.decksUsed}🃏` : ''}</span>
                  <span className="race-fame">{fmt(w.fame)} 🏅</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  )
}
