import { useEffect, useRef, useState } from 'react'
import { useT } from '../lib/i18n'
import { copyText, haptic, hapticNotify, openExternalLink } from '../lib/telegram'
import { overlayUrl, type OverlayParam, type WidgetSpec } from '../lib/overlayLinks'
import { Icon } from './ui/Icon'
import { IconTile, type Tone } from './ui/Section'

const TONES: Record<WidgetSpec['id'], Tone> = {
  bracket: 'violet', match: 'orange', list: 'blue', league: 'gold', challenge: 'green',
}

/**
 * Список виджетов OBS со ссылками: имя, размер источника, «копировать» и «открыть».
 *
 * Один и тот же блок стоит в Студии и в карточке турнира: блогер, открывший свой
 * турнир, не должен идти за ссылкой в другую вкладку посреди трансляции.
 */
export function WidgetLinks({ widgets, overlayKey, compact = false, param = 'k' }: {
  widgets: WidgetSpec[]
  overlayKey: string
  /** Без описаний — в карточке турнира, где место дороже. */
  compact?: boolean
  /** c — overlayKey на самом деле код челленджа блогера. */
  param?: OverlayParam
}) {
  return (
    <div className="st-widgets">
      {widgets.map(w => <WidgetRow key={w.id} spec={w} overlayKey={overlayKey} compact={compact} param={param} />)}
    </div>
  )
}

function WidgetRow({ spec, overlayKey, compact, param }: { spec: WidgetSpec; overlayKey: string; compact: boolean; param: OverlayParam }) {
  const { t, lang } = useT()
  const [copied, setCopied] = useState<'ok' | 'fail' | null>(null)
  const timer = useRef<number | undefined>(undefined)
  useEffect(() => () => window.clearTimeout(timer.current), [])

  // Ссылка — на языке блогера: виджет смотрят его зрители, и язык у них общий.
  const url = overlayUrl(spec.id, overlayKey, lang, param)

  const copy = async () => {
    haptic('light')
    const ok = await copyText(url)
    hapticNotify(ok ? 'success' : 'error')
    setCopied(ok ? 'ok' : 'fail')
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => setCopied(null), 1800)
  }

  return (
    <div className={`st-widget ${compact ? 'st-widget-compact' : ''}`}>
      <div className="st-widget-top">
        <IconTile name={spec.icon} tone={TONES[spec.id]} size={compact ? 30 : 36} />
        <span className="st-widget-text">
          <span className="st-widget-name">{t.studio.widget[spec.id]}</span>
          {!compact && <span className="muted small">{t.studio.widgetDesc[spec.id]}</span>}
        </span>
        {/* Размер — то, что блогер вводит в OBS руками, поэтому он на виду, а не в подсказке */}
        <span className="st-size">{spec.width}×{spec.height}</span>
      </div>
      <div className="st-widget-actions">
        <button className={`ui-btn st-copy ${copied === 'ok' ? 'st-copy-ok' : ''}`} onClick={copy}>
          <Icon name={copied === 'ok' ? 'check' : 'copy'} size={15} />
          {copied === 'ok' ? t.studio.copied : copied === 'fail' ? t.studio.copyFailed : t.studio.copy}
        </button>
        <button className="ui-btn st-open" onClick={() => { haptic('light'); openExternalLink(url) }}>
          <Icon name="external" size={15} /> {t.studio.open}
        </button>
      </div>
    </div>
  )
}

/** Шаги «как добавить в OBS» — одни и те же в Студии и в карточке турнира. */
export function ObsSteps({ steps }: { steps: string[] }) {
  return <ol>{steps.map((s, i) => <li key={i}>{s}</li>)}</ol>
}
