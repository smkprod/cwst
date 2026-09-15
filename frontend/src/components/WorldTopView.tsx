import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TopMeta, TopPlayerRow } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { DeckStrip } from './DeckStrip'
import { TopPlayerModal } from './TopPlayerModal'

const PAGE = 50

type Section = 'meta' | 'list'

/**
 * Мировой топ: тысяча лучших игроков планеты, их колоды и мета.
 *
 * Сам рейтинг Clash Royale отдаёт живьём, но только «сейчас» и только первую тысячу.
 * Поэтому интересное здесь — не сам список (его видно и в игре), а то, чего в игре нет:
 * порог входа в тысячу и как менялась популярность карт. И то и другое существует
 * только потому, что мы храним вчерашний снимок. Пока снимков нет, честно говорим
 * «копим», а не рисуем пустой экран с нулями.
 */
export function WorldTopView() {
  const { t } = useT()
  const [meta, setMeta] = useState<TopMeta | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'empty' | 'error'>('loading')
  const [section, setSection] = useState<Section>('meta')
  const [openTag, setOpenTag] = useState<string | null>(null)

  useEffect(() => {
    let alive = true
    api.getTopMeta()
      .then(m => {
        if (!alive) return
        setMeta(m)
        setState(m ? 'ready' : 'empty')
      })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [])

  if (state === 'loading') {
    return <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
  }

  if (state === 'error') {
    return <p className="center muted" style={{ marginTop: 16 }}>{t.worldTop.error}</p>
  }

  if (state === 'empty' || !meta) {
    return (
      <section className="card fade-in">
        <div className="card-title">{t.worldTop.title}</div>
        <p className="wtop-empty">{t.worldTop.collecting}</p>
        <p className="muted small" style={{ margin: 0 }}>{t.worldTop.collectingHint}</p>
      </section>
    )
  }

  return (
    <div className="fade-in">
      <section className="card">
        <div className="card-title-row">
          <div className="card-title">{t.worldTop.title}</div>
          <span className="muted small">{t.worldTop.snapshot} {meta.dayUtc}</span>
        </div>
        <p className="muted small" style={{ margin: '0 0 10px' }}>{t.worldTop.hint}</p>

        <div className="wtop-tiles">
          <Tile value={fmt(meta.cutoffTrophies)} label={t.worldTop.cutoff} hint={t.worldTop.cutoffHint} accent />
          <Tile value={fmt(meta.topTrophies)} label={t.worldTop.best} />
          <Tile value={meta.avgLevel > 0 ? String(meta.avgLevel) : '—'} label={t.worldTop.avgLevel} />
        </div>
      </section>

      <div className="wtop-tabs">
        <button
          className={`wtop-tab ${section === 'meta' ? 'wtop-tab-on' : ''}`}
          onClick={() => { haptic('light'); setSection('meta') }}
        >
          🃏 {t.worldTop.tabMeta}
        </button>
        <button
          className={`wtop-tab ${section === 'list' ? 'wtop-tab-on' : ''}`}
          onClick={() => { haptic('light'); setSection('list') }}
        >
          🏆 {t.worldTop.tabList}
        </button>
      </div>

      {section === 'meta' ? <MetaCards meta={meta} t={t} /> : <TopList t={t} onOpen={setOpenTag} />}

      {openTag && <TopPlayerModal tag={openTag} onClose={() => setOpenTag(null)} />}
    </div>
  )
}

function Tile({ value, label, hint, accent }: {
  value: string
  label: string
  hint?: string
  accent?: boolean
}) {
  return (
    <div className={`wtop-tile ${accent ? 'wtop-tile-accent' : ''}`}>
      <span className="wtop-tile-value">{value}</span>
      <span className="wtop-tile-label">{label}</span>
      {hint && <span className="wtop-tile-hint">{hint}</span>}
    </div>
  )
}

/** Витрина популярности карт с недельной динамикой. */
function MetaCards({ meta, t }: { meta: TopMeta; t: Translations }) {
  return (
    <section className="card" style={{ marginTop: 10 }}>
      <div className="card-title">{t.worldTop.cardsTitle}</div>
      <p className="muted small" style={{ margin: '0 0 2px' }}>{t.worldTop.cardsHint}</p>
      <p className="muted small" style={{ margin: '0 0 10px', opacity: 0.75 }}>
        {meta.comparedToDayUtc
          ? `${t.worldTop.comparedTo} ${meta.comparedToDayUtc}`
          : t.worldTop.noCompare}
        {' · '}
        {fmt(meta.playersWithDeck)} {t.worldTop.decksKnown}
      </p>

      {meta.cards.length === 0 ? (
        <p className="muted small" style={{ margin: 0 }}>{t.worldTop.noCards}</p>
      ) : (
        <div className="wtop-cards">
          {meta.cards.map(c => (
            <div key={c.cardId} className="wtop-card">
              {c.iconUrl
                ? <img src={c.iconUrl} alt={c.name} loading="lazy" />
                : <div className="wtop-card-blank" />}
              <span className="wtop-card-pct">{c.percent}%</span>
              <span className="wtop-card-name">{c.name}</span>
              {c.deltaPercent !== null && c.deltaPercent !== 0 && (
                <span className={`wtop-delta ${c.deltaPercent > 0 ? 'wtop-up' : 'wtop-down'}`}>
                  {c.deltaPercent > 0 ? '▲' : '▼'} {Math.abs(c.deltaPercent)}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

/** Список топа порциями: тысяча строк с восемью иконками разом телефон не обрадует. */
function TopList({ t, onOpen }: { t: Translations; onOpen: (tag: string) => void }) {
  const [rows, setRows] = useState<TopPlayerRow[]>([])
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [more, setMore] = useState(false)
  const [done, setDone] = useState(false)

  useEffect(() => {
    let alive = true
    api.getTopPlayers(0, PAGE)
      .then(r => { if (alive) { setRows(r); setDone(r.length < PAGE); setState('ready') } })
      .catch(() => { if (alive) setState('error') })
    return () => { alive = false }
  }, [])

  const loadMore = async () => {
    haptic('light')
    setMore(true)
    try {
      const next = await api.getTopPlayers(rows.length, PAGE)
      setRows(prev => [...prev, ...next])
      if (next.length < PAGE) setDone(true)
    } catch {
      setDone(true)                        // дальше не получилось — кнопку убираем
    } finally {
      setMore(false)
    }
  }

  if (state === 'loading') {
    return <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
  }
  if (state === 'error') {
    return <p className="center muted" style={{ marginTop: 16 }}>{t.worldTop.error}</p>
  }

  return (
    <div style={{ marginTop: 10 }}>
      {rows.map(r => (
        <button key={r.playerTag} className="card wtop-row" onClick={() => { haptic('light'); onOpen(r.playerTag) }}>
          <div className="wtop-row-head">
            <span className={`wtop-rank ${r.rank <= 3 ? 'wtop-rank-medal' : ''}`}>
              {r.rank <= 3 ? ['🥇', '🥈', '🥉'][r.rank - 1] : r.rank}
            </span>
            <span className="wtop-row-main">
              <span className="wtop-row-name">{r.name}</span>
              <span className="muted small wtop-row-clan">
                {r.clanName || '—'}{r.expLevel > 0 ? ` · ${t.worldTop.levelShort} ${r.expLevel}` : ''}
              </span>
            </span>
            <span className="wtop-row-trophies">{fmt(r.trophies)} 🏆</span>
          </div>
          {r.deck.length > 0
            ? <DeckStrip deck={r.deck} />
            : <span className="muted small wtop-row-nodeck">{t.worldTop.deckUnknown}</span>}
        </button>
      ))}

      {!done && (
        <button className="btn btn-secondary wtop-more" onClick={loadMore} disabled={more}>
          {more ? '…' : `${t.worldTop.loadMore} (${rows.length}/1000)`}
        </button>
      )}
    </div>
  )
}
