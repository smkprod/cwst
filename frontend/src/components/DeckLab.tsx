import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { BuilderCard, DeckLab as Lab } from '../types'
import { useT } from '../lib/i18n'
import { haptic } from '../lib/telegram'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'
import { InfoButton } from './ui/Info'
import { CardArt, CardPicker, DeckCard } from './DeckBuilderView'

/**
 * Конструктор колоды: восемь слотов, а под ними - что получилось. Шкалы, чего не
 * хватает, связи между картами по колодам топа, что добавить и похожие колоды из меты.
 * Каждое изменение колоды - новый разбор с сервера: правила и статистика живут там.
 */
export function DeckLab({ ids, onChange, cards, loadCards }: {
  ids: number[]
  onChange: (ids: number[]) => void
  cards: BuilderCard[] | null
  loadCards: () => void
}) {
  const { t, lang } = useT()
  const b = t.builder
  const [lab, setLab] = useState<Lab | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'nometa' | 'error'>('loading')
  const [picker, setPicker] = useState(false)

  useEffect(() => {
    let alive = true
    setState(s => (s === 'ready' ? 'ready' : 'loading'))
    // Небольшая пауза: тапы по нескольким картам подряд - один запрос, а не пять
    const timer = setTimeout(() => {
      api.builderAnalyze(ids, lang)
        .then(r => { if (alive) { setLab(r); setState('ready') } })
        .catch(e => { if (alive) setState(e instanceof ApiError && e.code === 'no_meta' ? 'nometa' : 'error') })
    }, 250)
    return () => { alive = false; clearTimeout(timer) }
  }, [ids, lang])

  const add = (id: number) => {
    if (ids.includes(id) || ids.length >= 8) return
    haptic('light')
    onChange([...ids, id])
  }
  const remove = (id: number) => { haptic('light'); onChange(ids.filter(x => x !== id)) }
  const openPicker = () => { haptic('light'); loadCards(); setPicker(true) }

  // Карты колоды - из разбора (с уровнями игрока), пока он грузится - из справочника
  const byId = new Map<number, BuilderCard>()
  for (const c of cards ?? []) byId.set(c.cardId, c)
  for (const c of lab?.cards ?? []) byId.set(c.cardId, c)
  const name = (id: number) => byId.get(id)?.name ?? `#${id}`

  const checkLabel: Record<string, string> = {
    win: b.checkWin, antiair: b.checkAntiair, splash: b.checkSplash,
    smallspell: b.checkSmallspell, bigspell: b.checkBigspell, elixir: b.checkElixir,
  }
  const reasonLabel: Record<string, string> = {
    best: b.reasonBest, win: b.reasonWin, antiair: b.reasonAntiair, splash: b.reasonSplash,
    smallspell: b.reasonSmallspell, bigspell: b.reasonBigspell,
  }

  return (
    <>
      <section className="card">
        <div className="lab-slots">
          {Array.from({ length: 8 }, (_, i) => {
            const id = ids[i]
            const card = id !== undefined ? byId.get(id) : undefined
            return id !== undefined ? (
              <button key={i} className="bld-card lab-slot" onClick={() => remove(id)} aria-label={name(id)}>
                {card ? <CardArt card={card} /> : <span className="bld-card-name">{name(id)}</span>}
                {lab?.linked && card?.owned && <span className={`bld-level ${card.level >= card.maxLevel ? 'bld-level-max' : ''}`}>{card.level}</span>}
                <span className="lab-slot-x"><Icon name="x" size={10} /></span>
              </button>
            ) : (
              <button key={i} className="lab-slot lab-slot-empty" onClick={openPicker} aria-label="+">
                <Icon name="plus" size={22} />
              </button>
            )
          })}
        </div>

        <div className="lab-summary">
          <span><Icon name="droplet" size={13} /> {lab ? lab.avgElixir : '-'}</span>
          <span className="muted">{b.cycle.replace('{n}', String(lab?.cycleElixir ?? 0))}</span>
          {ids.length === 8 && <span className="lab-full"><Icon name="check" size={13} /> {b.full}</span>}
          {ids.length > 0 && <button className="btn-mini lab-clear" onClick={() => { haptic('light'); onChange([]) }}>{b.clearDeck}</button>}
        </div>

        {ids.length === 0 && <p className="muted small" style={{ margin: '8px 0 0' }}>{b.labEmpty}</p>}

        {lab && ids.length > 0 && (
          <div className="lab-bars">
            {([['attack', b.barAttack, lab.attack], ['defense', b.barDefense, lab.defense],
              ['synergy', b.barSynergy, lab.synergy], ['balance', b.barBalance, lab.balance]] as const).map(([k, label, v]) => (
              <div key={k} className="lab-bar">
                <div className="lab-bar-head"><span>{label}</span><b>{v}</b></div>
                <div className="lab-bar-track">
                  <i className={v >= 70 ? 'lab-good' : v >= 40 ? 'lab-mid' : 'lab-bad'} style={{ width: `${Math.max(4, v)}%` }} />
                </div>
              </div>
            ))}
          </div>
        )}

        {lab && ids.length > 0 && (
          <div className="lab-checks">
            {lab.checks.map(c => (
              <span key={c.key} className={`lab-check ${c.ok ? 'lab-check-ok' : 'lab-check-no'}`}>
                <Icon name={c.ok ? 'check' : 'alert'} size={12} />{checkLabel[c.key]}
              </span>
            ))}
          </div>
        )}

        {lab?.copyLink && (
          <a className="btn btn-nudge bld-copy" href={lab.copyLink} target="_blank" rel="noreferrer" onClick={() => haptic('medium')}>
            <Icon name="external" size={15} /> {b.copy}
          </a>
        )}
      </section>

      {state === 'loading' && !lab && <div className="center" style={{ marginTop: 16 }}><div className="spinner" /></div>}
      {state === 'nometa' && <p className="center muted">{b.noMeta}</p>}
      {state === 'error' && <p className="center muted">{b.loadError}</p>}

      {lab && lab.links.length > 0 && (
        <section className="card">
          <SectionHead icon="link" tone="blue" title={b.linksTitle} info={<p>{b.linksInfo}</p>} />
          <div className="lab-links">
            {lab.links.map(l => (
              <div key={`${l.a}-${l.b}`} className="lab-link">
                <div className="lab-link-cards">
                  {[l.a, l.b].map(id => (
                    <span key={id} className="lab-mini">{byId.get(id) ? <CardArt card={byId.get(id)!} /> : null}</span>
                  ))}
                </div>
                <div className="lab-link-text">
                  <b>{name(l.a)} + {name(l.b)}</b>
                  <span className="muted small">{b.linkLine.replace('{co}', String(l.coPlayPercent)).replace('{win}', String(l.winPercent))}</span>
                  {l.note && <span className="small">{l.note}</span>}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {lab && lab.suggestions.length > 0 && (
        <section className="card">
          <SectionHead icon="plus" tone="green" title={b.addTitle} />
          {lab.suggestions.map(g => (
            <div key={g.reason} className="lab-group">
              <div className={`lab-group-title ${g.reason === 'best' ? '' : 'lab-group-need'}`}>
                {g.reason !== 'best' && <Icon name="alert" size={13} />}{reasonLabel[g.reason] ?? g.reason}
              </div>
              <div className="lab-suggest">
                {g.cards.map(s => (
                  <button key={s.card.cardId} className={`lab-suggest-card ${s.weak ? 'lab-weak' : ''}`}
                    onClick={() => add(s.card.cardId)} disabled={ids.length >= 8}>
                    <span className="bld-card"><CardArt card={s.card} />
                      {lab.linked && s.card.owned && <span className="bld-level">{s.card.level}</span>}
                    </span>
                    {g.reason !== 'best' || ids.length > 0 ? <span className="lab-syn">{s.synergy}%</span> : null}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </section>
      )}

      {lab && lab.roles.length > 0 && (
        <section className="card">
          <SectionHead icon="list" tone="violet" title={b.rolesTitle} />
          <div className="lab-roles">
            {lab.roles.map(r => (
              <div key={r.role} className="lab-role">
                <span className="lab-role-name">{b.roleNames[r.role] ?? r.role} <span className="muted">{r.cards.length}</span></span>
                <div className="lab-role-cards">
                  {r.cards.map(id => <span key={id} className="lab-mini">{byId.get(id) ? <CardArt card={byId.get(id)!} /> : null}</span>)}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {lab && lab.similar.length > 0 && (
        <>
          <div className="lab-similar-head">
            <b>{b.similarTitle}</b>
            <InfoButton title={b.similarTitle}><p>{b.info}</p></InfoButton>
          </div>
          {lab.similar.map(d => (
            <DeckCard key={d.key} deck={d} linked={lab.linked} t={t}
              onToLab={deck => { haptic('medium'); onChange(deck.cards.map(c => c.cardId)); window.scrollTo({ top: 0, behavior: 'smooth' }) }} />
          ))}
        </>
      )}

      {picker && (
        <CardPicker cards={cards} t={t} title={`${b.tabLab} · ${ids.length}/8`} hint={b.pickAdd}
          marks={Object.fromEntries(ids.map(id => [id, 'in' as const]))}
          onToggle={id => (ids.includes(id) ? remove(id) : add(id))}
          onClose={() => setPicker(false)} />
      )}
    </>
  )
}
