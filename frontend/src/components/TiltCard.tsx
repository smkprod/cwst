import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TiltProfile } from '../types'
import { botStartLink, haptic, hapticNotify, requestWriteAccess, shareToTelegram, tgUser } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { Icon } from './ui/Icon'
import { SectionHead } from './ui/Section'

const LIMITS = [0, 3, 5, 7, 10]

/**
 * «Стоп-тильт» — первая карточка вкладки «Я».
 *
 * Бесплатно — проблема в собственных цифрах: тильт-тип, во что тильт обошёлся за
 * неделю, моменты, когда бот написал бы. С Плюсом — решение: живые сигналы, свои
 * правила, «пауза работает». Продаёт не замок, а узнавание себя в цифрах.
 */
export function TiltCard() {
  const { t } = useT()
  const s = t.tilt
  const openPlus = usePlusSheet()
  const [data, setData] = useState<TiltProfile | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    api.getTilt().then(setData).catch(() => setData(null))
  }, [])
  useEffect(load, [load])
  useEffect(() => {
    window.addEventListener(PLUS_CHANGED, load)
    return () => window.removeEventListener(PLUS_CHANGED, load)
  }, [load])

  if (!data) return null

  const save = async (prefs: Parameters<typeof api.setTiltPrefs>[0]) => {
    haptic('medium')
    setBusy(true)
    try {
      await api.setTiltPrefs(prefs)
      hapticNotify('success')
      load()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  const toggle = async () => {
    if (!data.enabled) {
      // Без права писать в личку бот не достучится до того, кто ни разу не нажал «Старт»
      if (!(await requestWriteAccess())) { hapticNotify('error'); return }
    }
    await save({ enabled: !data.enabled })
  }

  const ready = data.type !== null
  const typeName = typeLabel(data.type, t)

  const share = () => {
    haptic('medium')
    const text = s.shareText
      .replace('{type}', typeName)
      .replace('{a}', String(Math.round(data.after2Percent ?? 0)))
      .replace('{b}', String(Math.round(data.basePercent)))
    shareToTelegram(text, botStartLink(tgUser ? `ref_${tgUser.id}` : undefined))
  }

  return (
    <section className={`card tilt-card tilt-${data.type ?? 'none'}`}>
      <div className="tilt-head">
        <SectionHead icon="snowflake" tone="blue" title={s.title} className="bt-head-flush" />
        <span className="muted small">{s.promise}</span>
      </div>

      {ready ? (
        <div className="tilt-type">
          <span className="tilt-type-name bt-inline">
            <Icon name={data.type === 'ice' ? 'snowflake' : 'flame'} size={20} className={`bt-tilt-icon bt-tilt-${data.type}`} />
            {typeName}
          </span>
          <span className="small">{typeText(data.type, t)}</span>
          {data.after2Percent !== null && (
            <span className="muted small">
              {s.stats.replace('{a}', String(Math.round(data.after2Percent))).replace('{b}', String(Math.round(data.basePercent)))}
            </span>
          )}
          {data.prevType && data.prevType !== data.type && (
            <span className={`small tilt-shift ${rank(data.type) > rank(data.prevType) ? 'tilt-shift-hot' : ''}`}>
              {(rank(data.type) < rank(data.prevType) ? s.cooled : s.heated)
                .replace('{from}', typeLabel(data.prevType, t)).replace('{to}', typeName)}
            </span>
          )}
        </div>
      ) : (
        <Progress data={data} t={t} />
      )}

      {ready && (
        <p className="small tilt-tax">
          {data.weekExtraLosses >= 0.5
            ? s.tax.replace('{n}', String(Math.round(data.weekExtraLosses)))
              + (data.weekTiltTrophies < 0 ? s.taxTrophies.replace('{t}', `−${-data.weekTiltTrophies}`) : '')
            : s.taxNone}
          {data.prevWeekExtraLosses >= 0.5 && (
            <span className="muted"> · {s.taxPrev.replace('{n}', String(Math.round(data.prevWeekExtraLosses)))}</span>
          )}
        </p>
      )}

      {data.lastSeries && (
        <div className="tilt-series" aria-label={s.lastSeries}>
          <span className="muted small">{s.lastSeries}</span>
          <span className="tilt-series-cells">
            {data.lastSeries.split('').map((r, i) => (
              <span key={i} className={`tilt-cell tilt-cell-${r}`} />
            ))}
          </span>
        </div>
      )}

      {/* Самое честное место продать — сразу после серии, пока она свежая */}
      {!data.unlocked && data.recentStreak && (
        <p className="tilt-banner">
          {s.recentStreak
            .replace('{time}', new Date(data.recentStreak.atUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }))
            .replace('{n}', String(data.recentStreak.length))
            .replace('{trophies}', data.recentStreak.trophies < 0 ? `, −${-data.recentStreak.trophies} ${t.duel.trophies}` : '')}
        </p>
      )}
      {!data.unlocked && data.moments14 > 0 && (
        <p className="muted small" style={{ margin: '6px 0 0' }}>
          {s.moments.replace('{n}', String(data.moments14))
            .replace('{w}', String(data.moments14Wins)).replace('{l}', String(data.moments14Losses))}
        </p>
      )}

      <div className="tilt-toggle">
        <div className="tilt-toggle-text">
          <b>{data.enabled ? s.enabled : s.disabled}</b>
          {!data.unlocked && (
            <span className="muted small">
              {data.freeSignalsLeft > 0 ? s.freeLeft.replace('{n}', String(data.freeSignalsLeft)) : s.freeUsed}
            </span>
          )}
          {data.pauseUntil && (
            <span className="muted small">
              {s.pausedUntil.replace('{time}', new Date(data.pauseUntil).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }))}
            </span>
          )}
        </div>
        {(data.unlocked || data.freeSignalsLeft > 0 || data.enabled) && (
          <button
            className={`ba-switch ${data.enabled ? 'ba-switch-on' : ''}`}
            role="switch"
            aria-checked={data.enabled}
            disabled={busy}
            onClick={toggle}
          >
            <span className="ba-switch-knob" />
          </button>
        )}
      </div>
      {data.dmBlocked && <p className="muted small" style={{ margin: '6px 0 0' }}>{s.dmBlocked}</p>}

      {data.unlocked ? (
        <>
          <Rules data={data} busy={busy} onSave={save} t={t} />
          <Works data={data} t={t} />
          <Feed data={data} t={t} />
        </>
      ) : (
        <>
          <button className="ba-locked" onClick={() => openPlus()}>
            <span className="ba-locked-icon bt-locked-icon"><Icon name="lock" size={14} /></span>
            <span className="ba-locked-text">{s.rulesPlusOnly}</span>
            <span className="ba-locked-arrow"><Icon name="chevronRight" size={16} /></span>
          </button>
          <button className="btn plus-cta" style={{ marginTop: 10 }} onClick={() => openPlus()}><Icon name="gem" size={16} /> {s.unlock}</button>
        </>
      )}

      {ready && (
        <button className="btn-mini tilt-share" onClick={share}><Icon name="share" size={13} /> {s.share}</button>
      )}
    </section>
  )
}

function Progress({ data, t }: { data: TiltProfile; t: Translations }) {
  const s = t.tilt
  const needBattles = Math.max(0, data.minBattles - data.games)
  const pct = Math.min(100, Math.round((data.games / data.minBattles) * 100))
  return (
    <div className="tilt-progress">
      <span className="small">
        {needBattles > 0 ? s.progress.replace('{n}', String(needBattles)) : s.progressSamples}
      </span>
      <span className="ba-bar-track"><span className="ba-bar-fill ba-bar-good" style={{ width: `${needBattles > 0 ? pct : 90}%` }} /></span>
    </div>
  )
}

function Rules({ data, busy, onSave, t }: {
  data: TiltProfile
  busy: boolean
  onSave: (prefs: { lossThreshold?: number; dailyLossLimit?: number; quietHours?: boolean }) => void
  t: Translations
}) {
  const s = t.tilt
  return (
    <div className="tilt-rules">
      <SectionHead className="bt-head-sm" icon="sliders" tone="gray" title={s.rulesTitle} />
      <div className="tilt-rule">
        <span className="small">{s.ruleThreshold}</span>
        <span className="tilt-seg">
          {[2, 3].map(n => (
            <button key={n} className={`tilt-seg-btn ${data.lossThreshold === n ? 'tilt-seg-on' : ''}`}
              disabled={busy} onClick={() => onSave({ lossThreshold: n })}>{n}</button>
          ))}
        </span>
      </div>
      <div className="tilt-rule">
        <span className="small">{s.ruleLimit}</span>
        <span className="tilt-seg">
          {LIMITS.map(n => (
            <button key={n} className={`tilt-seg-btn ${(data.dailyLossLimit ?? 0) === n ? 'tilt-seg-on' : ''}`}
              disabled={busy} onClick={() => onSave({ dailyLossLimit: n })}>{n === 0 ? s.ruleLimitOff : n}</button>
          ))}
        </span>
      </div>
      <div className="tilt-rule">
        <span className="small">{s.ruleQuiet}</span>
        <button className={`ba-switch ${data.quietHours ? 'ba-switch-on' : ''}`} role="switch"
          aria-checked={data.quietHours} disabled={busy} onClick={() => onSave({ quietHours: !data.quietHours })}>
          <span className="ba-switch-knob" />
        </button>
      </div>
    </div>
  )
}

/** «Пауза работает» — главный довод продлить разовый пропуск. */
function Works({ data, t }: { data: TiltProfile; t: Translations }) {
  const s = t.tilt
  return (
    <div className="tilt-works">
      <SectionHead className="bt-head-sm" icon="pause" tone="green" title={s.worksTitle} />
      <span className="small">
        {data.pausedPercent !== null && data.notPausedPercent !== null
          ? s.works.replace('{a}', String(data.pausedPercent)).replace('{b}', String(data.notPausedPercent))
          : s.worksEarly}
      </span>
    </div>
  )
}

function Feed({ data, t }: { data: TiltProfile; t: Translations }) {
  const s = t.tilt
  if (data.recentAlerts.length === 0) return null
  const choice = (c: string | null) =>
    c === 'pause' ? s.choicePause : c === 'go' ? s.choiceGo : c === 'mute' ? s.choiceMute : s.choiceNone
  return (
    <div className="tilt-feed">
      <SectionHead className="bt-head-sm" icon="bell" tone="orange" title={s.feedTitle} />
      {data.recentAlerts.map(a => (
        <div key={a.sentUtc} className="tilt-feed-row">
          <span className="muted small">
            {new Date(a.sentUtc).toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}
          </span>
          <span className="small">{a.kind === 'limit' ? s.limitKind : <><span className="tilt-cell tilt-cell-L bt-loss-cell" />×{a.lossStreak}</>} · {choice(a.choice)}</span>
          <span className="small tilt-feed-after">
            {a.kind === 'limit' ? '' : a.summarized
              ? s.feedAfter.replace('{w}', String(a.afterWins)).replace('{l}', String(a.afterLosses))
              : s.feedOpen}
          </span>
        </div>
      ))}
    </div>
  )
}

function typeLabel(type: string | null, t: Translations) {
  return type === 'ice' ? t.tilt.typeIce : type === 'boiling' ? t.tilt.typeBoiling : type === 'volcano' ? t.tilt.typeVolcano : '—'
}

function typeText(type: string | null, t: Translations) {
  return type === 'ice' ? t.tilt.typeIceText : type === 'boiling' ? t.tilt.typeBoilingText : t.tilt.typeVolcanoText
}

/** Чем горячее тип, тем больше: «остыл» — если ранг уменьшился. */
function rank(type: string | null) {
  return type === 'ice' ? 0 : type === 'boiling' ? 1 : type === 'volcano' ? 2 : -1
}
