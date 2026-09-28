import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { MatchCard, MatchReport, MatchSide } from '../types'
import { haptic, openExternalLink } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { usePlusSheet } from '../lib/plusSheet'
import { usePlayerSheet } from '../lib/playerSheet'
import { signed } from './MatchHistoryView'

/** Полное HP башен по уровню не знаем — полоска показывает остаток от самой крепкой из видимых. */
const KING_HP_GUESS = 4824
const PRINCESS_HP_GUESS = 3052

/**
 * Отчёт об одном бое: обе колоды с уровнями, башни, эликсир, соперник, вывод и
 * «против кого и чем играть». Тексты вывода и строк Плюса приходят с сервера —
 * те же, что в карточке бота.
 */
export function MatchReportSheet({ id, onClose }: { id: number; onClose: () => void }) {
  const { t, lang } = useT()
  const s = t.trk
  const openPlus = usePlusSheet()
  const openPlayer = usePlayerSheet()
  const [data, setData] = useState<MatchReport | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'locked' | 'error'>('loading')

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    api.getMatch(id, lang)
      .then(d => { if (alive) { setData(d); setState('ready') } })
      .catch(e => { if (alive) setState(e instanceof ApiError && e.status === 403 ? 'locked' : 'error') })
    return () => { alive = false }
  }, [id, lang])

  const close = () => { haptic('light'); onClose() }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet psheet-modal fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />
        {state === 'loading' && <div className="center" style={{ padding: 24 }}><div className="spinner" /></div>}
        {state === 'error' && <p className="center muted">{s.loadError}</p>}
        {state === 'locked' && (
          <div className="center" style={{ padding: '12px 0' }}>
            <p className="muted small">{s.locked403}</p>
            <button className="btn" onClick={() => openPlus()}>{t.battles.unlockBtn}</button>
          </div>
        )}
        {state === 'ready' && data && (
          <Report r={data} t={t} onPlus={() => openPlus()} onOpponent={tag => openPlayer(tag)} />
        )}
      </div>
    </div>
  )
}

function Report({ r, t, onPlus, onOpponent }: {
  r: MatchReport
  t: Translations
  onPlus: () => void
  onOpponent: (tag: string) => void
}) {
  const s = t.trk
  const time = new Date(r.timeUtc).toLocaleString(t.dateLocale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
  const resultText = r.result > 0 ? s.win : r.result < 0 ? s.loss : s.draw

  return (
    <div className="trk-report">
      <div className={`trk-report-head ${r.result > 0 ? 'trk-head-w' : r.result < 0 ? 'trk-head-l' : ''}`}>
        <div className="trk-report-score">{r.crownsFor}–{r.crownsAgainst}</div>
        <div className="trk-report-title">
          <b>{resultText}</b>
          {r.trophyChange !== null && <span> · {signed(r.trophyChange)}🏆</span>}
          <div className="muted small">{s.modes[r.mode] ?? r.mode} · {time}</div>
        </div>
      </div>

      <button
        className="trk-opp"
        disabled={!r.opp.tag}
        onClick={() => r.opp.tag && onOpponent(r.opp.tag)}
      >
        <span className="trk-opp-name">
          <b>{r.opp.name ?? '—'}</b>
          {r.opp.clan && <span className="muted small"> · {r.opp.clan}</span>}
        </span>
        <span className="muted small">
          {r.opp.trophies !== null && <>{r.opp.trophies}🏆</>}
          {r.opp.diff !== null && r.opp.diff !== 0 && <> ({signed(r.opp.diff)})</>}
          {r.opp.globalRank !== null && <> · {s.rank.replace('{n}', String(r.opp.globalRank))}</>}
        </span>
        {r.opp.tag && <span className="ba-locked-arrow">›</span>}
      </button>

      {r.verdict && <p className="trk-verdict">{r.verdict}</p>}

      <div className="trk-side-title">
        <span>{s.them}{r.arch && <b> · {r.arch.label}</b>}</span>
        {r.oppAvgElixir !== null && <span className="muted small">{s.avgElixir.replace('{n}', r.oppAvgElixir.toFixed(1))}</span>}
      </div>
      <Deck side={r.them} t={t} />
      <div className="trk-side-title"><span>{s.you}</span></div>
      <Deck side={r.me} t={t} />

      {r.hasDetail ? (
        <>
          <div className="trk-block">
            <div className="tilt-rules-title">{s.towers}</div>
            <Towers label={s.them} side={r.them} t={t} />
            <Towers label={s.you} side={r.me} t={t} />
          </div>

          {r.leak && (r.leak.mine !== null || r.leak.theirs !== null) && (
            <div className="trk-block">
              <div className="tilt-rules-title">{s.leakTitle} <span className="muted small">· {s.leakHint}</span></div>
              <span className="small">
                {s.you}: {r.leak.mine?.toFixed(1) ?? '—'}
                {r.leak.usual !== null && <span className="muted"> ({s.leakUsual.replace('{n}', r.leak.usual.toFixed(1))})</span>}
                {' · '}{s.them}: {r.leak.theirs?.toFixed(1) ?? '—'}
              </span>
            </div>
          )}

          {r.levels && Math.abs(r.levels.gap) >= 0.3 && (
            <div className="trk-block">
              <div className="tilt-rules-title">{s.levelsTitle}</div>
              <span className="small">{s.levelsAvg.replace('{a}', r.levels.myAvg.toFixed(1)).replace('{b}', r.levels.oppAvg.toFixed(1))}</span>
              {r.levels.lowestMine && (
                <span className="muted small">
                  {s.lowest.replace('{card}', r.levels.lowestMine.name).replace('{lvl}', String(r.levels.lowestMine.level))}
                </span>
              )}
            </div>
          )}
        </>
      ) : (
        <p className="muted small">{s.noDetail}</p>
      )}

      {r.plus ? (
        <div className="trk-block trk-plus">
          <div className="tilt-rules-title">
            {s.plusTitle}
            {r.hint && <span className="trk-hint-badge">{s.hint}</span>}
          </div>
          {r.plus.betterDeck && <span className="small">{r.plus.betterDeck}</span>}
          {r.plus.vsArch && <span className="small">{r.plus.vsArch}</span>}
          {r.plus.deckVsArchWins !== null && r.plus.deckVsArchLosses !== null && r.arch && (
            <span className="muted small">
              {s.deckVsArch.replace('{a}', r.arch.label)
                .replace('{w}', String(r.plus.deckVsArchWins)).replace('{l}', String(r.plus.deckVsArchLosses))}
            </span>
          )}
        </div>
      ) : !r.unlocked && (
        <button className="ba-locked" style={{ marginTop: 10 }} onClick={onPlus}>
          <span className="ba-locked-icon">🔒</span>
          <span className="ba-locked-text">
            {r.lockedCount > 0 ? s.lockedCard.replace('{n}', String(r.lockedCount)) : s.lockedCardZero}
          </span>
          <span className="ba-locked-arrow">›</span>
        </button>
      )}

      <div className="trk-block">
        <span className="small">
          {s.sessionLine
            .replace('{i}', String(r.session.index)).replace('{n}', String(r.session.count))
            .replace('{w}', String(r.session.wins)).replace('{l}', String(r.session.losses))
            .replace('{t}', signed(r.session.trophies))}
        </span>
        <span className="tilt-series-cells">
          {r.session.strip.split('').map((c, i) => (
            <span key={i} className={`tilt-cell tilt-cell-${c} ${i === r.session.index - 1 ? 'trk-cell-now' : ''}`} />
          ))}
        </span>
      </div>

      {r.them.copyLink && (
        <button className="btn btn-ghost" style={{ width: '100%', marginTop: 12 }}
          onClick={() => { haptic('medium'); openExternalLink(r.them.copyLink!) }}>
          {s.copyOpp}
        </button>
      )}
    </div>
  )
}

function Deck({ side, t }: { side: MatchSide; t: Translations }) {
  return (
    <div className="trk-deck">
      {side.deck.map((c, i) => <Card key={`${c.cardId}-${i}`} c={c} t={t} />)}
      {side.towerTroop && <Card c={side.towerTroop} t={t} tower />}
    </div>
  )
}

/** Карта с уровнем и значком эволюции или героя. */
export function Card({ c, t, tower = false }: { c: MatchCard; t: Translations; tower?: boolean }) {
  const badge = c.form === 2 ? '👑' : c.form === 1 ? '✨' : null
  return (
    <span className={`trk-cardimg ${tower ? 'trk-cardimg-tower' : ''} ${c.form > 0 ? 'mdeck-evo' : ''}`}
      title={`${c.name}${c.form === 2 ? ` · ${t.trk.hero}` : c.form === 1 ? ` · ${t.trk.evo}` : ''}`}>
      {c.iconUrl ? <img src={c.iconUrl} alt={c.name} loading="lazy" /> : <span className="wtop-card-blank" />}
      {badge && <span className="trk-card-badge">{badge}</span>}
      {c.level > 0 && <span className="trk-card-level">{c.level}</span>}
    </span>
  )
}

/** Башни стороны: король и принцессы; снесённые — серые. */
function Towers({ label, side, t }: { label: string; side: MatchSide; t: Translations }) {
  const princess = side.princessHp ?? []
  const bars: { hp: number | null; max: number; king: boolean }[] = [
    { hp: princess[0] ?? null, max: PRINCESS_HP_GUESS, king: false },
    { hp: side.kingHp, max: KING_HP_GUESS, king: true },
    { hp: princess[1] ?? null, max: PRINCESS_HP_GUESS, king: false },
  ]
  return (
    <div className="trk-towers">
      <span className="muted small trk-towers-label">{label}</span>
      {bars.map((b, i) => (
        <span key={i} className={`trk-tower ${b.hp === null || b.hp <= 0 ? 'trk-tower-down' : ''}`}>
          <span className="trk-tower-icon">{b.king ? '👑' : '🏰'}</span>
          <span className="trk-tower-hp">{b.hp !== null && b.hp > 0 ? b.hp : t.trk.destroyed}</span>
          <span className="ba-bar-track">
            <span className="ba-bar-fill ba-bar-good" style={{ width: `${b.hp ? Math.min(100, Math.round(b.hp / Math.max(b.max, b.hp) * 100)) : 0}%` }} />
          </span>
        </span>
      ))}
    </div>
  )
}
