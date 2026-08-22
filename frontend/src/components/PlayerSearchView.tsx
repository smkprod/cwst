import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ClanOverview, PlayerProfile, SearchHistoryItem } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { PlayerProfileCard } from './PlayerProfileCard'
import { ClanProfileCard } from './ClanProfileCard'
import { readHistory, pushHistory, clearHistory } from '../lib/searchHistory'

type Kind = 'player' | 'clan'

type SearchState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'player'; data: PlayerProfile }
  | { kind: 'clan'; data: ClanOverview }

/** Теги CR пишут по-разному: с решёткой, в нижнем регистре, с лишними пробелами. */
function normalizeTag(raw: string) {
  return raw.trim().replace(/^#+/, '').toUpperCase()
}

export function PlayerSearchView() {
  const { t } = useT()
  const [kind, setKind] = useState<Kind>('player')
  const [tag, setTag] = useState('')
  const [state, setState] = useState<SearchState>({ kind: 'idle' })
  const [history, setHistory] = useState<SearchHistoryItem[]>([])

  useEffect(() => { setHistory(readHistory()) }, [])

  const run = async (searchKind: Kind, rawTag: string) => {
    const clean = normalizeTag(rawTag)
    if (!clean) return

    haptic('light')
    setKind(searchKind)
    setTag(clean)
    setState({ kind: 'loading' })

    try {
      if (searchKind === 'player') {
        const data = await api.getPlayerProfile('#' + clean)
        setState({ kind: 'player', data })
        setHistory(pushHistory({ kind: 'player', tag: clean, name: data.name }))
      } else {
        const data = await api.getClanOverview('#' + clean)
        setState({ kind: 'clan', data })
        setHistory(pushHistory({ kind: 'clan', tag: clean, name: data.clanName ?? clean }))
      }
    } catch (e) {
      setState({
        kind: 'error',
        message: e instanceof ApiError && e.message !== e.code
          ? e.message
          : searchKind === 'player' ? t.search.notFound : t.search.clanNotFound,
      })
    }
  }

  const switchKind = (next: Kind) => {
    if (next === kind) return
    haptic('light')
    setKind(next)
    // Результат относится к прежнему виду поиска — оставлять его под чужой
    // вкладкой значит показывать игрока в разделе кланов.
    setState({ kind: 'idle' })
  }

  return (
    <div>
      <section className="card">
        <div className="card-title-row">
          <div className="card-title">{t.search.title}</div>
        </div>

        <div className="search-kinds">
          <button
            className={`search-kind ${kind === 'player' ? 'search-kind-on' : ''}`}
            onClick={() => switchKind('player')}
          >
            👤 {t.search.kindPlayer}
          </button>
          <button
            className={`search-kind ${kind === 'clan' ? 'search-kind-on' : ''}`}
            onClick={() => switchKind('clan')}
          >
            🏰 {t.search.kindClan}
          </button>
        </div>

        <p className="muted small" style={{ margin: '0 0 12px' }}>
          {kind === 'player' ? t.search.hint : t.search.clanHint}
        </p>

        <div className="search-row">
          <input
            className="search-input"
            type="text"
            placeholder={t.search.placeholder}
            value={tag}
            onChange={e => setTag(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') run(kind, tag) }}
            autoCapitalize="characters"
            spellCheck={false}
          />
          <button className="btn search-btn" onClick={() => run(kind, tag)} disabled={state.kind === 'loading'}>
            {state.kind === 'loading' ? t.search.finding : t.search.find}
          </button>
        </div>
      </section>

      {/* История нужна ровно тогда, когда результата на экране нет */}
      {state.kind === 'idle' && history.length > 0 && (
        <HistoryCard
          items={history}
          onOpen={item => run(item.kind, item.tag)}
          onClear={() => { haptic('light'); setHistory(clearHistory()) }}
        />
      )}

      {state.kind === 'loading' && (
        <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
      )}
      {state.kind === 'error' && (
        <p className="center muted" style={{ marginTop: 16 }}>{state.message}</p>
      )}
      {state.kind === 'player' && <PlayerProfileCard profile={state.data} />}
      {state.kind === 'clan' && <ClanProfileCard clan={state.data} />}
    </div>
  )
}

function HistoryCard({ items, onOpen, onClear }: {
  items: SearchHistoryItem[]
  onOpen: (item: SearchHistoryItem) => void
  onClear: () => void
}) {
  const { t } = useT()
  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="card-title-row">
        <div className="cards-section-title" style={{ margin: 0 }}>{t.search.historyTitle}</div>
        <button className="btn-mini" onClick={onClear}>{t.search.historyClear}</button>
      </div>

      <ul className="search-history">
        {items.map(item => (
          <li key={`${item.kind}:${item.tag}`}>
            <button className="search-history-row" onClick={() => onOpen(item)}>
              <span className="search-history-icon">{item.kind === 'clan' ? '🏰' : '👤'}</span>
              <span className="search-history-name">{item.name}</span>
              <span className="search-history-tag muted small">#{item.tag}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
