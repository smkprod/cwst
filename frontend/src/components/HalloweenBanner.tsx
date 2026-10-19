import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'
import { BOSS_ART, hwNum, loadHalloween, openHalloween } from '../lib/halloween'
import { haptic } from '../lib/telegram'
import type { HalloweenView } from '../types'

/**
 * Баннер босса на первом экране клана: здоровье одной полосой и вход во вкладку.
 * Вкладку в нижнем меню замечают не все, а первый экран видит каждый.
 */
export function HalloweenBanner() {
  const { t } = useT()
  const s = t.hw
  const [data, setData] = useState<HalloweenView | null>(null)

  useEffect(() => {
    let alive = true
    loadHalloween()
      .then(d => { if (alive) setData(d) })
      .catch(() => { /* нет босса - нет баннера */ })
    return () => { alive = false }
  }, [])

  if (!data) return null
  const left = Math.max(0, data.hp - data.damage)
  const pct = data.hp > 0 ? Math.round((100 * left) / data.hp) : 0
  const defeated = data.defeatedUtc !== null

  return (
    <button className={`hwb hwb-${data.bossKey} fade-in`} onClick={() => { haptic('light'); openHalloween() }}>
      <div className="hwb-body">
        <span className="hw-pill hw-pill-red">{s.bossTag}</span>
        <b className="hwb-name">{s.bossNames[data.bossKey]}</b>
        <div className="hw-bar hw-bar-red"><i style={{ width: `${pct}%` }} /></div>
        <span className="hwb-left">
          {defeated ? s.bannerDefeated : s.bannerLeft.replace('{p}', String(pct))}
          {!defeated && data.today > 0 && <> · {s.today.replace('{n}', hwNum(data.today, t.dateLocale))}</>}
        </span>
      </div>
      <img className={`hwb-art ${defeated ? 'hw-boss-dead' : ''}`} src={BOSS_ART[data.bossKey]} alt="" />
    </button>
  )
}
