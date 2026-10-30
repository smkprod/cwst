import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'
import { BOSS_ART, hwDate, loadHalloween, openHalloween } from '../lib/halloween'
import { afterChallengePromo, promoDone } from '../lib/promo'
import { haptic, initData, startParam } from '../lib/telegram'
import type { HalloweenView } from '../types'

const seenKey = (boss: string) => `cwst_hw_splash_${boss}`

/**
 * Заставка Хэллоуина: один раз на каждого босса, при открытии приложения. Только
 * тем, у кого есть клан с боссом, и только без ссылки на конкретный раздел: пришёл
 * по кнопке «Разбор» - пришёл за разбором. Окно челленджа важнее: если оно уже
 * показалось, заставка ждёт следующего захода.
 */
export function HalloweenSplash() {
  const { t } = useT()
  const s = t.hw
  const [data, setData] = useState<HalloweenView | null>(null)

  useEffect(() => {
    if (!initData || startParam !== '') {
      afterChallengePromo().then(() => promoDone())
      return
    }
    let alive = true
    afterChallengePromo()
      .then(shown => {
        if (shown) { promoDone(); return }
        return loadHalloween().then(d => {
          if (!alive) return
          let seen = false
          try { seen = localStorage.getItem(seenKey(d.bossKey)) === '1' } catch { /* без памяти - покажем */ }
          if (seen) { promoDone(); return }
          try { localStorage.setItem(seenKey(d.bossKey), '1') } catch { /* пусть так */ }
          setData(d)
          haptic('medium')
        })
      })
      .catch(() => promoDone())
    return () => { alive = false }
  }, [])

  if (!data) return null
  const close = () => { haptic('light'); setData(null); promoDone() }
  const go = () => { haptic('medium'); setData(null); promoDone(); openHalloween() }
  const days = String(data.rewardPlusDays)

  return (
    <div className="hws" role="dialog" aria-modal="true">
      <div className="hws-inner fade-up">
        <img className="hws-bat" src="/hw/bat.webp" alt="" />
        <img className="hws-scene" src="/hw/scene.webp" alt="" />
        <div className="hw-pill hw-pill-or">{s.until.replace('{date}', hwDate(data.eventEndUtc, t.dateLocale))}</div>
        <h1 className="hw-title hws-title">{s.splashTitle}</h1>
        <p className="hws-text">{s.splashText}</p>
        <Feat img={BOSS_ART[data.bossKey]} title={`${s.bossTag}: ${s.bossNames[data.bossKey]}`} text={s.splashBossText} />
        {data.nextBossKey && data.nextStartUtc && (
          <Feat img={BOSS_ART[data.nextBossKey]}
            title={s.splashNextTitle.replace('{date}', hwDate(data.nextStartUtc, t.dateLocale))}
            text={s.bossNames[data.nextBossKey]} />
        )}
        <Feat img="/hw/pumpkin-s.webp" title={s.splashRewardTitle} text={s.splashRewardText.replace('{days}', days)} />
        <Feat img="/hw/ghost.webp" title={s.huntSoonTitle} text={s.huntSoonText.replace('{days}', '3')} />
        <button className="hw-cta" onClick={go}>
          <img src="/hw/pumpkin-s.webp" alt="" /> {s.splashGo}
        </button>
        <button className="hws-later" onClick={close}>{s.splashLater}</button>
      </div>
    </div>
  )
}

function Feat({ img, title, text }: { img: string; title: string; text: string }) {
  return (
    <div className="hws-feat">
      <div className="hws-feat-pic"><img src={img} alt="" /></div>
      <div><b>{title}</b><span>{text}</span></div>
    </div>
  )
}
