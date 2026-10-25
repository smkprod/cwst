import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { PlusStatus } from '../types'
import { haptic } from '../lib/telegram'
import { PLUS_CHANGED, usePlusSheet } from '../lib/plusSheet'
import { useT } from '../lib/i18n'
import type { AppMode } from '../lib/mode'
import emblem from '../assets/clanify-emblem.webp'
import { Icon, type IconName } from './ui/Icon'

/** Статус Плюса и Клан Про - один запрос на приложение, перечитывается после покупки. */
export function usePlusState(): PlusStatus | null {
  const [status, setStatus] = useState<PlusStatus | null>(null)
  useEffect(() => {
    let alive = true
    const load = () => api.getPlus().then(s => { if (alive) setStatus(s) }).catch(() => { /* кнопка покупки останется */ })
    load()
    window.addEventListener(PLUS_CHANGED, load)
    return () => { alive = false; window.removeEventListener(PLUS_CHANGED, load) }
  }, [])
  return status
}

/**
 * Шапка режима: знак Clanify, в каком режиме ты сейчас, и покупка на самом видном
 * месте - в режиме игрока Плюс, в режиме клана Клан Про. Купленное не прячем:
 * кнопка становится статусом и ведёт в то же окно - продлить или подарить.
 */
export function ModeHeader({ mode, plus }: { mode: AppMode; plus: PlusStatus | null }) {
  const { t } = useT()
  const m = t.mode
  const openPlus = usePlusSheet()
  const clan = mode === 'clan'

  const owned = clan ? Boolean(plus?.isSponsor || plus?.clanPro) : Boolean(plus?.active || plus?.isSponsor)
  const price = clan
    ? (plus?.sponsorPrice ?? 0)
    : Math.min(...[plus?.promo7, plus?.price7].filter((v): v is number => typeof v === 'number' && v > 0), Infinity)

  return (
    <div className={`mode-head mode-head-${mode}`}>
      <span className="mode-brand">
        <img src={emblem} alt="" />
        <span className="mode-brand-text">
          <b>Clanify</b>
          <span className="muted small">{clan ? m.clanCaption : m.playerCaption}</span>
        </span>
      </span>
      <button
        className={`buy-pill buy-pill-${clan ? 'pro' : 'plus'} ${owned ? 'owned' : ''}`}
        onClick={() => { haptic('medium'); openPlus(clan ? 'sponsor' : 'plus') }}
      >
        <Icon name={clan ? 'crown' : 'gem'} size={15} />
        <span>{clan ? m.clanPro : m.plus}</span>
        {owned
          ? <Icon name="check" size={13} />
          : Number.isFinite(price) && price > 0 && <span className="buy-price">{clan ? `${price}★` : m.from.replace('{p}', String(price))}</span>}
      </button>
    </div>
  )
}

export interface BarTab<T extends string> { id: T; icon: IconName; label: string; badge?: 'plus' | 'pro' | 'dot' }

/**
 * Нижнее меню: первым - переключатель режима, дальше вкладки режима. Переключатель
 * показывает, где ты сейчас, а точка на нём - что в другом режиме тебя что-то ждёт.
 */
export function TabBar<T extends string>({ tabs, active, onSelect, mode, onToggleMode, modeDot = false }: {
  tabs: BarTab<T>[]
  active: T
  onSelect: (id: T) => void
  mode?: AppMode
  onToggleMode?: () => void
  modeDot?: boolean
}) {
  const { t } = useT()
  return (
    <nav className="tabbar" role="tablist">
      {mode && onToggleMode && (
        <button className={`tab mode-switch mode-switch-${mode}`} onClick={() => { haptic('medium'); onToggleMode() }}
          aria-label={mode === 'clan' ? t.mode.toPlayer : t.mode.toClan}>
          <span className="mode-switch-track">
            <span className="mode-switch-knob"><Icon name={mode === 'clan' ? 'castle' : 'user'} size={15} /></span>
            <Icon name={mode === 'clan' ? 'user' : 'castle'} size={13} className="mode-switch-other" />
            {modeDot && <span className="mode-dot" />}
          </span>
          <span className="tab-label">{mode === 'clan' ? t.mode.clan : t.mode.player}</span>
        </button>
      )}
      {tabs.map(tb => (
        <button
          key={tb.id}
          role="tab"
          aria-selected={active === tb.id}
          className={`tab ${active === tb.id ? 'tab-active' : ''}`}
          onClick={() => { haptic('light'); onSelect(tb.id) }}
        >
          <span className="tab-icon">
            {tb.id === 'halloween'
              ? <img className="tab-img" src="/hw/pumpkin-s.webp" alt="" />
              : <Icon name={tb.icon} size={20} />}
            {tb.badge === 'plus' && <span className="tab-plus"><Icon name="gem" size={9} /></span>}
            {tb.badge === 'pro' && <span className="tab-plus tab-pro"><Icon name="crown" size={9} /></span>}
            {tb.badge === 'dot' && <span className="tab-dot" />}
          </span>
          <span className="tab-label">{tb.label}</span>
        </button>
      ))}
    </nav>
  )
}
