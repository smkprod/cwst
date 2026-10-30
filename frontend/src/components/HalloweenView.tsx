import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'
import { BOSS_ART, hwDate, hwNum, loadHalloween } from '../lib/halloween'
import { haptic } from '../lib/telegram'
import { usePlayerSheet } from '../lib/playerSheet'
import type { HalloweenHitter, HalloweenView as View } from '../types'
import { Icon, type IconName } from './ui/Icon'
import { Pumpkin, PumpkinHunt } from './Pumpkin'

/**
 * Вкладка «Хэллоуин»: босс клана. Здоровье, урон за сегодня, как бить, своя строка,
 * таблица урона и награда. Своя ночная палитра на весь экран - вкладка должна
 * выглядеть праздником, а не ещё одной карточкой в ряду.
 */
export function HalloweenView() {
  const { t } = useT()
  const s = t.hw
  const [data, setData] = useState<View | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let alive = true
    loadHalloween(true)
      .then(d => { if (alive) setData(d) })
      .catch(() => { if (alive) setFailed(true) })
    return () => { alive = false }
  }, [])

  // Ночное небо на весь экран, пока открыта вкладка
  useEffect(() => {
    document.documentElement.classList.add('hw-night')
    return () => document.documentElement.classList.remove('hw-night')
  }, [])

  if (failed) {
    return <div className="hw"><div className="hw-glass hw-empty">{s.error}</div></div>
  }
  if (!data) {
    return (
      <div className="hw">
        <div className="hw-hero hw-skeleton" />
        <div className="hw-glass hw-skeleton" style={{ height: 96 }} />
      </div>
    )
  }

  const locale = t.dateLocale
  const left = Math.max(0, data.hp - data.damage)
  const leftPct = data.hp > 0 ? Math.round((100 * left) / data.hp) : 0
  const defeated = data.defeatedUtc !== null

  return (
    <div className="hw fade-in">
      <Stars />
      <section className={`hw-hero hw-hero-${data.bossKey}`}>
        <div className="hw-pills">
          <span className="hw-pill hw-pill-red">{s.bossTag}</span>
          <span className="hw-pill hw-pill-or">{s.until.replace('{date}', hwDate(data.endUtc, locale))}</span>
        </div>
        <h1 className="hw-title hw-title-red">{s.bossNames[data.bossKey]}</h1>
        <p className="hw-hero-text">{s.heroText}</p>
        <img className={`hw-boss hw-boss-${data.bossKey} ${defeated ? 'hw-boss-dead' : ''}`}
          src={BOSS_ART[data.bossKey]} alt="" />
      </section>

      <section className="hw-glass hw-rim-red">
        <div className="hw-hp-head">
          <b>{defeated ? s.defeatedTitle : s.hp}</b>
          <span>{leftPct}%</span>
        </div>
        <div className="hw-bar hw-bar-red"><i style={{ width: `${leftPct}%` }} /></div>
        <div className="hw-hp-foot">
          <span>{hwNum(left, locale)} / {hwNum(data.hp, locale)}</span>
          {defeated
            ? <span>{s.defeatedText.replace('{date}', hwDate(data.defeatedUtc!, locale))}</span>
            : data.today > 0 && <span className="hw-today">{s.today.replace('{n}', hwNum(data.today, locale))}</span>}
        </div>
      </section>

      <Pumpkin id={1} side="mid" />
      <PumpkinHunt />

      <MyDamage data={data} />

      <section className="hw-glass hw-rim-red">
        <div className="hw-h">
          <div className="hw-ico hw-ico-red"><img src="/hw/bat.webp" alt="" /></div>
          <div className="hw-h-title">{s.howTitle}</div>
        </div>
        <HitRow icon="swords" title={s.hitWar} dmg={data.warWin} />
        <HitRow icon="crown" title={s.hitThree} dmg={data.warThreeCrowns} />
        <HitRow icon="trophy" title={s.hitLadder}
          note={s.hitLadderNote.replace('{n}', String(data.ladderWinsPerDay))} dmg={data.ladderWin} />
        <p className="hw-note">{s.howNote}</p>
      </section>

      <TopDamage data={data} />

      <Reward data={data} />

      {data.previous && <Previous data={data} />}
      {data.nextBossKey && data.nextStartUtc && (
        <section className="hw-glass hw-next">
          <img className="hw-next-art" src={BOSS_ART[data.nextBossKey]} alt="" />
          <div>
            <div className="hw-muted-cap">{s.nextTitle}</div>
            <b className="hw-next-name">{s.bossNames[data.nextBossKey]}</b>
            <div className="hw-muted">{s.nextText.replace('{date}', hwDate(data.nextStartUtc, locale))}</div>
          </div>
        </section>
      )}

    </div>
  )
}

function Stars() {
  // Звёзды раз и навсегда: случайные на каждый рендер мигали бы при обновлении данных
  const [stars] = useState(() => Array.from({ length: 28 }, (_, i) => ({
    left: (i * 37) % 100,
    top: (i * 53) % 46,
    o: 0.25 + ((i * 7) % 10) / 16,
  })))
  return (
    <div className="hw-sky" aria-hidden="true">
      <div className="hw-moon" />
      {stars.map((st, i) => <i key={i} style={{ left: `${st.left}%`, top: `${st.top}%`, opacity: st.o }} />)}
    </div>
  )
}

function HitRow({ icon, title, note, dmg }: { icon: IconName; title: string; note?: string; dmg: number }) {
  return (
    <div className="hw-dmg">
      <span className="hw-dmg-ic"><Icon name={icon} size={16} /></span>
      <span className="hw-dmg-t">{title}{note && <small>{note}</small>}</span>
      <span className="hw-dmg-v">−{dmg}</span>
    </div>
  )
}

function MyDamage({ data }: { data: View }) {
  const { t } = useT()
  const s = t.hw
  const me = data.me
  if (!me) {
    return <section className="hw-glass hw-me hw-me-none"><Icon name="swords" size={18} /> {s.meNone}</section>
  }
  const parts = [
    me.warWins > 0 && `${s.meWar} ${me.warWins}`,
    me.threeCrowns > 0 && `${s.meThree} ${me.threeCrowns}`,
    me.ladderWins > 0 && `${s.meLadder} ${me.ladderWins}`,
  ].filter(Boolean).join(' · ')
  return (
    <section className="hw-glass hw-rim-or hw-me">
      <div>
        <div className="hw-muted-cap">{s.meTitle}</div>
        <b className="hw-me-dmg">{hwNum(me.damage, t.dateLocale)}</b>
        <div className="hw-muted">{parts}</div>
      </div>
      {data.myRank && (
        <span className="hw-pill hw-pill-or">{s.meRank.replace('{n}', String(data.myRank)).replace('{of}', String(data.hitters))}</span>
      )}
    </section>
  )
}

function TopDamage({ data }: { data: View }) {
  const { t } = useT()
  const s = t.hw
  const openPlayer = usePlayerSheet()
  const rest = data.hitters - data.top.length
  const open = (h: HalloweenHitter) => { haptic('light'); openPlayer(h.tag) }
  return (
    <section className="hw-glass hw-rim-or">
      <div className="hw-h">
        <div className="hw-ico hw-ico-or"><img src="/hw/pumpkin-s.webp" alt="" /></div>
        <div className="hw-h-title">{s.topTitle}</div>
      </div>
      {data.top.length === 0 && <p className="hw-muted">{s.topEmpty}</p>}
      {data.top.map((h, i) => (
        <button key={h.tag} className={`hw-row ${data.me?.tag === h.tag ? 'hw-row-me' : ''}`} onClick={() => open(h)}>
          <span className={`hw-num ${i < 3 ? `hw-n${i + 1}` : ''}`}>{i + 1}</span>
          <b>{h.name}</b>
          <span className="hw-pts">{hwNum(h.damage, t.dateLocale)}</span>
        </button>
      ))}
      {rest > 0 && <p className="hw-muted hw-more">{s.andMore.replace('{n}', String(rest))}</p>}
    </section>
  )
}

function Reward({ data }: { data: View }) {
  const { t } = useT()
  const s = t.hw
  const days = String(data.rewardPlusDays)
  const defeated = data.defeatedUtc !== null
  const text = !defeated
    ? s.rewardText.replace('{days}', days)
    : data.me
      ? s.rewardGot.replace('{days}', days)
      : s.rewardMissed
  return (
    <section className={`hw-glass hw-reward ${defeated && data.me ? 'hw-reward-got' : ''}`}>
      <span className="hw-reward-ic"><Icon name={defeated && data.me ? 'checkCircle' : 'gem'} size={22} /></span>
      <div>
        <b>{s.rewardTitle}</b>
        <div className="hw-muted">{text}</div>
      </div>
    </section>
  )
}

function Previous({ data }: { data: View }) {
  const { t } = useT()
  const s = t.hw
  const p = data.previous!
  const left = Math.max(0, p.hp - p.damage)
  const pct = p.hp > 0 ? Math.round((100 * left) / p.hp) : 0
  return (
    <section className="hw-glass hw-prev">
      <img className={`hw-prev-art ${p.defeatedUtc ? 'hw-boss-dead' : ''}`} src={BOSS_ART[p.key]} alt="" />
      <div>
        <b>{s.bossNames[p.key]}</b>
        <div className="hw-muted">{p.defeatedUtc
          ? s.prevDefeated.replace('{date}', hwDate(p.defeatedUtc, t.dateLocale))
          : s.prevEscaped.replace('{p}', String(pct))}</div>
      </div>
    </section>
  )
}
