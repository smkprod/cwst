import { useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../lib/api'
import { questDone } from '../lib/quests'
import type { BuilderCard, BuilderDeck, BuilderDecks, BuilderQuery, BuilderWar } from '../types'
import { useT, type Translations } from '../lib/i18n'
import { haptic } from '../lib/telegram'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'
import { DeckLab } from './DeckLab'

type Tab = 'lab' | 'meta' | 'war'

const LAB_KEY = 'cwst_lab_deck'
const TAB_KEY = 'cwst_bld_tab'
type Mark = 'in' | 'out'

const ELIXIR_STEPS: (number | null)[] = [null, 3.5, 4, 4.5]

/**
 * Билдер колод: мета-колоды из боёв топа за неделю, которые игрок соберёт из своих
 * карт, и четыре колоды для КВ без общих карт. Колоды только настоящие - сервер берёт
 * их из сыгранных в топе, со статистикой; «выбор аналитика» лишь отмечает лучшие.
 */
export function DeckBuilderView() {
  const { t, lang } = useT()
  const b = t.builder
  const [tab, setTabState] = useState<Tab>(() => {
    try { return (localStorage.getItem(TAB_KEY) as Tab | null) ?? 'lab' } catch { return 'lab' }
  })
  const setTab = (k: Tab) => {
    setTabState(k)
    try { localStorage.setItem(TAB_KEY, k) } catch { /* без памяти - не страшно */ }
  }
  // Колода конструктора переживает закрытие приложения: собирать её начинают и бросают
  const [labIds, setLabIdsState] = useState<number[]>(() => {
    try { return JSON.parse(localStorage.getItem(LAB_KEY) ?? '[]') as number[] } catch { return [] }
  })
  const setLabIds = (ids: number[]) => {
    setLabIdsState(ids)
    try { localStorage.setItem(LAB_KEY, JSON.stringify(ids)) } catch { /* без памяти - не страшно */ }
  }
  const toLab = (deck: BuilderDeck) => {
    haptic('medium')
    setLabIds(deck.cards.map(c => c.cardId))
    setTab('lab')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }
  const [onlyMine, setOnlyMine] = useState(true)
  const [sort, setSort] = useState<BuilderQuery['sort']>('fit')
  const [maxElixir, setMaxElixir] = useState<number | null>(null)
  const [marks, setMarks] = useState<Record<number, Mark>>({})
  const [picker, setPicker] = useState(false)
  const [cards, setCards] = useState<BuilderCard[] | null>(null)

  const [decks, setDecks] = useState<BuilderDecks | null>(null)
  const [war, setWar] = useState<BuilderWar | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'nometa' | 'error'>('loading')

  const query = useMemo<BuilderQuery>(() => ({
    include: Object.entries(marks).filter(([, m]) => m === 'in').map(([id]) => Number(id)),
    exclude: Object.entries(marks).filter(([, m]) => m === 'out').map(([id]) => Number(id)),
    maxElixir, owned: onlyMine, sort,
  }), [marks, maxElixir, onlyMine, sort])

  useEffect(() => {
    if (tab === 'lab') return
    let alive = true
    setState('loading')
    const load = tab === 'meta'
      ? api.builderDecks(query, lang).then(d => { if (alive) setDecks(d) })
      : api.builderWar(query, lang).then(w => { if (alive) setWar(w) })
    load
      .then(() => {
        if (!alive) return
        setState('ready')
        // Колоды из своих карт подобраны - задание дня «подбери колоду» сделано
        questDone('decks')
      })
      .catch(e => { if (alive) setState(e instanceof ApiError && e.code === 'no_meta' ? 'nometa' : 'error') })
    return () => { alive = false }
  }, [tab, query, lang])

  const loadCards = () => { if (!cards) api.builderCards().then(setCards).catch(() => setCards([])) }
  const openPicker = () => {
    haptic('light')
    setPicker(true)
    loadCards()
  }

  const linked = tab === 'meta' ? decks?.linked : war?.linked
  const marked = Object.keys(marks).length

  return (
    <div className="fade-in bld">
      <section className="card">
        <SectionHead icon="cards" tone="violet" title={b.title} info={<p>{tab === 'war' ? b.warInfo : tab === 'lab' ? b.labInfo : b.info}</p>} />
        <div className="bld-tabs">
          {(['lab', 'meta', 'war'] as Tab[]).map(k => (
            <button key={k} className={`bld-tab ${tab === k ? 'bld-tab-on' : ''}`}
              onClick={() => { haptic('light'); setTab(k) }}>
              <Icon name={k === 'lab' ? 'edit' : k === 'meta' ? 'trendUp' : 'swords'} size={15} />
              {k === 'lab' ? b.tabLab : k === 'meta' ? b.tabMeta : b.tabWar}
            </button>
          ))}
        </div>

        {tab !== 'lab' && <>

        <div className="bld-filters">
          <button className={`bld-chip ${onlyMine ? 'bld-chip-on' : ''}`} onClick={() => { haptic('light'); setOnlyMine(v => !v) }}>
            <Icon name={onlyMine ? 'checkCircle' : 'cards'} size={14} />{b.onlyMine}
          </button>
          <button className={`bld-chip ${marked ? 'bld-chip-on' : ''}`} onClick={openPicker}>
            <Icon name="filter" size={14} />{b.cards}{marked ? ` · ${marked}` : ''}
          </button>
          {ELIXIR_STEPS.map(e => (
            <button key={String(e)} className={`bld-chip ${maxElixir === e ? 'bld-chip-on' : ''}`}
              onClick={() => { haptic('light'); setMaxElixir(e) }}>
              {e === null ? b.elixirAny : <><Icon name="droplet" size={13} />{b.elixirMax.replace('{n}', String(e))}</>}
            </button>
          ))}
        </div>

        {tab === 'meta' && (
          <div className="bld-sort">
            {([['fit', b.sortFit], ['win', b.sortWin], ['popular', b.sortPopular]] as const).map(([k, label]) => (
              <button key={k} className={`bld-sort-btn ${sort === k ? 'bld-sort-on' : ''}`}
                onClick={() => { haptic('light'); setSort(k) }}>{label}</button>
            ))}
          </div>
        )}

        {marked > 0 && cards && (
          <div className="bld-marked">
            {Object.entries(marks).map(([id, m]) => {
              const c = cards.find(x => x.cardId === Number(id))
              return c && (
                <button key={id} className={`bld-mark bld-mark-${m}`} onClick={() => setMarks(prev => {
                  const next = { ...prev }; delete next[Number(id)]; return next
                })}>
                  <img src={c.iconUrl} alt="" loading="lazy" />
                  <span>{m === 'in' ? b.with : b.without}</span>
                </button>
              )
            })}
            <button className="btn-mini" onClick={() => setMarks({})}>{b.clear}</button>
          </div>
        )}
        {linked === false && <p className="muted small" style={{ margin: '10px 0 0' }}>{b.notLinked}</p>}
        </>}
      </section>

      {tab === 'lab' && <DeckLab ids={labIds} onChange={setLabIds} cards={cards} loadCards={loadCards} />}

      {tab !== 'lab' && state === 'loading' && <div className="center" style={{ marginTop: 16 }}><div className="spinner" /></div>}
      {tab !== 'lab' && state === 'nometa' && <p className="center muted">{b.noMeta}</p>}
      {tab !== 'lab' && state === 'error' && <p className="center muted">{b.loadError}</p>}

      {state === 'ready' && tab === 'meta' && decks && <MetaList data={decks} t={t} onToLab={toLab} />}
      {state === 'ready' && tab === 'war' && war && <WarList data={war} t={t} onToLab={toLab} />}

      {picker && (
        <CardPicker cards={cards} marks={marks} t={t}
          onToggle={id => setMarks(prev => {
            // По кругу: с картой → без карты → не важно
            const next = { ...prev }
            if (prev[id] === undefined) next[id] = 'in'
            else if (prev[id] === 'in') next[id] = 'out'
            else delete next[id]
            return next
          })}
          onClose={() => setPicker(false)} />
      )}
    </div>
  )
}

function MetaList({ data, t, onToLab }: { data: BuilderDecks; t: Translations; onToLab: (d: BuilderDeck) => void }) {
  const b = t.builder
  return (
    <>
      {data.analyst && (
        <section className="card bld-analyst">
          <SectionHead icon="sparkles" tone="gold" title={b.analystTitle} info={<p>{b.analystInfo}</p>} />
          {data.analyst.summary && <p className="bld-analyst-text">{data.analyst.summary}</p>}
        </section>
      )}
      {data.decks.length === 0
        ? <p className="center muted">{b.empty}</p>
        : data.decks.map(d => <DeckCard key={d.key} deck={d} linked={data.linked} t={t} onToLab={onToLab} />)}
      <p className="muted small center">
        {b.period.replace('{from}', data.fromDayUtc).replace('{to}', data.toDayUtc)}
      </p>
    </>
  )
}

function WarList({ data, t, onToLab }: { data: BuilderWar; t: Translations; onToLab: (d: BuilderDeck) => void }) {
  const b = t.builder
  if (data.decks.length === 0) return <p className="center muted">{b.empty}</p>
  return (
    <>
      <section className="card bld-war-head">
        <b>{b.warTitle}</b>
        <span className="muted small">{b.warAvg.replace('{n}', String(data.avgWinPercent))} · {data.uniqueCards}/32</span>
        {!data.complete && <p className="small bld-warn">{b.warIncomplete.replace('{n}', String(data.decks.length))}</p>}
      </section>
      {data.decks.map((d, i) => (
        <DeckCard key={d.key} deck={d} linked={data.linked} t={t} title={b.deckN.replace('{n}', String(i + 1))} onToLab={onToLab} />
      ))}
    </>
  )
}

export function DeckCard({ deck, linked, t, title, onToLab }: {
  deck: BuilderDeck; linked: boolean; t: Translations; title?: string; onToLab?: (d: BuilderDeck) => void
}) {
  const b = t.builder
  return (
    <section className={`card bld-deck ${deck.pick ? 'bld-deck-pick' : ''}`}>
      <div className="bld-deck-head">
        {title && <b className="bld-deck-title">{title}</b>}
        {deck.pick && <span className="bld-pick"><Icon name="sparkles" size={12} />{deck.pick.tag || b.analystTitle}</span>}
        <span className="bld-win">{b.win.replace('{n}', String(deck.winPercent))}</span>
        <span className="muted small">{b.games.replace('{n}', String(deck.games))}</span>
      </div>
      <div className="bld-cards">
        {deck.cards.map(c => (
          <span key={`${c.cardId}-${c.evo}`} className={`bld-card ${linked && !c.owned ? 'bld-card-missing' : ''} ${c.evo ? 'bld-card-evo' : ''}`}>
            <CardArt card={c} />
            {linked && c.owned && (
              <span className={`bld-level ${c.level >= c.maxLevel ? 'bld-level-max' : ''}`}>{c.level}</span>
            )}
          </span>
        ))}
      </div>
      {deck.pick?.note && <p className="bld-note">{deck.pick.note}</p>}
      <div className="bld-meta muted small">
        <span><Icon name="droplet" size={12} /> {b.elixir.replace('{n}', String(deck.avgElixir))}</span>
        {linked && <span>{b.owned.replace('{n}', String(deck.owned))}</span>}
        {linked && deck.avgLevel > 0 && <span>{b.avgLevel.replace('{n}', String(deck.avgLevel))}</span>}
      </div>
      <div className="bld-deck-actions">
        {deck.copyLink && (
          <a className="btn btn-nudge bld-copy" href={deck.copyLink} target="_blank" rel="noreferrer" onClick={() => haptic('medium')}>
            <Icon name="external" size={15} /> {b.copy}
          </a>
        )}
        {onToLab && (
          <button className="btn btn-ghost bld-to-lab" onClick={() => onToLab(deck)}>
            <Icon name="edit" size={15} /> {b.toLab}
          </button>
        )}
      </div>
    </section>
  )
}

/**
 * Арт карты с запасом: у части эволюций ссылка на арт в справочнике игры битая. Тогда
 * показываем обычный арт, а если не открылся и он - плашку с названием, а не значок
 * сломанной картинки.
 */
export function CardArt({ card }: { card: BuilderCard }) {
  const sources = [card.iconUrl, card.baseIconUrl].filter((u, i, all) => u && all.indexOf(u) === i)
  const [step, setStep] = useState(0)
  if (step >= sources.length) {
    return <span className="bld-card-name">{card.name}<small>{card.elixir}</small></span>
  }
  return <img src={sources[step]} alt={card.name} loading="lazy" onError={() => setStep(s => s + 1)} />
}

export function CardPicker({ cards, marks, t, onToggle, onClose, title, hint }: {
  cards: BuilderCard[] | null
  title?: string
  hint?: string
  marks: Record<number, Mark>
  t: Translations
  onToggle: (id: number) => void
  onClose: () => void
}) {
  const b = t.builder
  const [q, setQ] = useState('')
  const shown = (cards ?? []).filter(c => !q || c.name.toLowerCase().includes(q.trim().toLowerCase()))
  return (
    <div className="bld-picker-backdrop" onClick={onClose}>
      <div className="bld-picker" onClick={e => e.stopPropagation()}>
        <div className="bld-picker-head">
          <b>{title ?? b.pickerTitle}</b>
          <button className="btn-mini" onClick={onClose}>{b.done}</button>
        </div>
        <p className="muted small" style={{ margin: '0 0 8px' }}>{hint ?? b.pickerHint}</p>
        <input className="search-input bld-picker-search" placeholder={b.search} value={q} onChange={e => setQ(e.target.value)} />
        {!cards ? <div className="center"><div className="spinner" /></div> : (
          <div className="bld-picker-grid">
            {shown.map(c => {
              const m = marks[c.cardId]
              return (
                <button key={c.cardId} className={`bld-pick-card ${m ? `bld-pick-${m}` : ''}`}
                  onClick={() => { haptic('light'); onToggle(c.cardId) }}>
                  <CardArt card={c} />
                  {m && <span className="bld-pick-badge"><Icon name={m === 'in' ? 'check' : 'x'} size={12} /></span>}
                </button>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}
