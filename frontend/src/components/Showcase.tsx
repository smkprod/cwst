import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { ArchetypeRow, BackgroundKey, MatchRow, MatchSession, Matchup } from '../types'
import { useT, type Translations } from '../lib/i18n'
import { haptic, tgUser } from '../lib/telegram'
import { REDUCED, useInView } from '../lib/anim'
import { usePlusSheet } from '../lib/plusSheet'
import { rowBackground, SponsorMarks } from '../lib/sponsorMarks'
import { ArchRadar, HeatMap, SessionChart } from './TrackerGraphics'
import { MatchupPanel } from './MatchupPanel'
import { Icon, type IconName } from './ui/Icon'
import { IconTile, SectionHead } from './ui/Section'

export type ShowcaseTab = 'plus' | 'sponsor'

/**
 * Витрина Плюса и спонсорства: что внутри, как это выглядит и чем одно отличается от
 * другого. Граница простая: Плюс - для себя, чтобы побеждать (это видишь только ты);
 * спонсорство - для статуса и клана (это видят все), и Плюс уже внутри.
 *
 * Всё на демо-данных и настоящими компонентами приложения: графики разбора, отчёт о
 * бое, строка рейтинга с фоном, тема клана. Фоны и темы можно примерить - нажал и
 * сразу видишь на себе.
 */
export function Showcase({ initialTab = 'plus', onClose }: { initialTab?: ShowcaseTab; onClose: () => void }) {
  const { t } = useT()
  const s = t.showcase
  const [tab, setTab] = useState<ShowcaseTab>(initialTab)
  const openPlus = usePlusSheet()

  const pick = (next: ShowcaseTab) => { haptic('light'); setTab(next) }

  return (
    <div className="sc-overlay" role="dialog" aria-modal="true">
      <div className="sc-page">
        <div className="sc-top">
          <button className="sc-back" onClick={() => { haptic('light'); onClose() }} aria-label="×">
            <Icon name="chevronLeft" size={20} />
          </button>
          <div className="sc-top-text">
            <div className="sc-title">{s.title}</div>
            <div className="sc-sub">{s.sub}</div>
          </div>
        </div>

        <div className="sc-tabs">
          <button className={`sc-tab ${tab === 'plus' ? 'sc-tab-on sc-tab-plus' : ''}`} onClick={() => pick('plus')}>
            <Icon name="gem" size={16} /> {s.tabPlus}
          </button>
          <button className={`sc-tab ${tab === 'sponsor' ? 'sc-tab-on sc-tab-sponsor' : ''}`} onClick={() => pick('sponsor')}>
            <Icon name="star" size={16} /> {s.tabSponsor}
          </button>
        </div>

        <p className={`sc-lead ${tab === 'plus' ? 'sc-lead-plus' : 'sc-lead-sponsor'}`}>
          {tab === 'plus' ? s.plusLead : s.sponsorLead}
        </p>

        {tab === 'plus'
          ? <PlusPart t={t} onBuy={() => openPlus('plus')} />
          : <SponsorPart t={t} onBuy={() => openPlus('sponsor')} />}

        <Compare t={t} highlight={tab} />

        <button className={`btn sc-cta ${tab === 'plus' ? 'sc-cta-plus' : 'sc-cta-sponsor'}`}
          onClick={() => { haptic('medium'); openPlus(tab) }}>
          <Icon name={tab === 'plus' ? 'gem' : 'star'} size={18} /> {tab === 'plus' ? s.buyPlus : s.buySponsor}
        </button>
      </div>
    </div>
  )
}

/* ================= Плюс ================= */

function PlusPart({ t, onBuy }: { t: Translations; onBuy: () => void }) {
  const s = t.showcase
  const demo = useMemo(demoData, [])
  return (
    <>
      <Block icon="message" tone="blue" title={s.trkTitle} hint={s.trkHint}>
        <ChatDemo bubbles={[
          ...s.trkLines.map((text, i) => ({ text, delay: 0.5 + i * 0.45, strong: i === 0 || i === s.trkLines.length - 1 })),
          { text: s.trkPlus, delay: 0.6 + s.trkLines.length * 0.45, plus: s.plusTag },
        ]} card replay={s.replay} />
      </Block>

      <Block icon="flame" tone="red" title={s.tiltTitle} hint={s.tiltHint}>
        <ChatDemo bubbles={[
          { text: s.tiltLoss1, delay: 0.3, mine: true },
          { text: s.tiltLoss2, delay: 1.0, mine: true },
          { text: s.tiltMsg, delay: 2.1, plus: s.plusTag, buttons: [s.tiltPause, s.tiltMore] },
        ]} replay={s.replay} />
      </Block>

      <Block icon="activity" tone="violet" title={s.reviewTitle} hint={s.reviewHint} demo={s.demo}>
        <div className="sc-graph"><SessionChart session={demo.session} t={t} /></div>
        <div className="sc-graph"><HeatMap cells={demo.heat} basePct={55} t={t} /></div>
        <div className="sc-graph"><ArchRadar rows={demo.arch} basePct={55} locked={false} onUnlock={onBuy} t={t} /></div>
      </Block>

      <Block icon="target" tone="green" title={s.matchupTitle} hint={s.matchupHint} demo={s.demo}>
        <MatchupPanel m={demo.matchup} t={t} onPlus={onBuy} />
      </Block>

      <section className="card sc-block">
        <SectionHead icon="list" tone="gold" title={s.plusTable} />
        <Table head={[s.colFree, s.colPlus]} rows={s.plusRows} accent={1} />
      </section>
    </>
  )
}

/* ================= Спонсорство ================= */

const PLAYER_BGS: BackgroundKey[] = ['arena', 'sunset', 'night', 'ice', 'lava']
const CLAN_BGS: BackgroundKey[] = ['kingdomSun', 'kingdom', 'kingdom2', 'kingdomFire']
const DESIGNS = ['royal', 'gold', 'neon', 'stone', 'blood', 'frost'] as const

function SponsorPart({ t }: { t: Translations; onBuy: () => void }) {
  const s = t.showcase
  const name = tgUser?.first_name || s.you
  const [bg, setBg] = useState<BackgroundKey>('sunset')
  const [clanBg, setClanBg] = useState<BackgroundKey>('kingdomFire')
  const [design, setDesign] = useState<(typeof DESIGNS)[number]>('gold')
  const row = rowBackground({ backgroundKey: bg })

  return (
    <>
      <Block icon="star" tone="gold" title={s.bgTitle} hint={s.bgHint}>
        <div className="sc-rows">
          <div className="sc-lrow sc-lrow-dim"><span className="sc-lrow-rank">1</span><span className="sc-lrow-name">ME!Narko?</span><span className="sc-lrow-val">3 300</span></div>
          <div className={`sc-lrow sc-lrow-me ${row.className}`} style={row.style} key={bg}>
            <span className="sponsor-bg-veil" />
            <span className="sc-lrow-rank">2</span>
            <span className="sc-lrow-name">{name} <SponsorMarks of={{ isSponsor: true }} /></span>
            <span className="sc-lrow-val">3 150</span>
          </div>
          <div className="sc-lrow sc-lrow-dim"><span className="sc-lrow-rank">3</span><span className="sc-lrow-name">PHARAON</span><span className="sc-lrow-val">3 100</span></div>
        </div>
        <Swatches keys={PLAYER_BGS} value={bg} onPick={setBg} />
      </Block>

      <Block icon="castle" tone="violet" title={s.themeTitle} hint={s.themeHint}>
        <div className="sc-theme" data-clan-theme={clanBg} key={clanBg}>
          <div className="sc-theme-hero" style={{ backgroundImage: `url(/bg/${clanBg}.webp)` }}>
            <span className="sc-theme-veil" />
            <div className="sc-theme-name">{s.clanName} <Icon name="star" size={14} fill="currentColor" /></div>
            <div className="sc-theme-war">{s.themeWar}</div>
          </div>
          <div className="sc-theme-body">
            <div className="sc-theme-stat"><b>41 250</b> {s.themeStat}</div>
            <div className="sc-theme-bar"><i /></div>
            <button className="sc-theme-btn" tabIndex={-1}><Icon name="bolt" size={14} /> {s.themeButton}</button>
          </div>
        </div>
        <Swatches keys={CLAN_BGS} value={clanBg} onPick={setClanBg} />
      </Block>

      <Block icon="palette" tone="orange" title={s.designTitle} hint={s.designHint}>
        <div className={`sc-design design-${design}`} key={design}>
          <header className="page-hero page-hero-bg" style={{ backgroundImage: `url(/bg/${clanBg}.webp)` }}>
            <span className="page-hero-veil" />
            <div className="page-hero-body page-hero-split">
              <div className="page-hero-left">
                <span className="page-hero-crest cl-crest"><IconTile name="castle" tone="violet" size={40} /></span>
                <h2 className="page-hero-name">{s.clanName}</h2>
                <span className="page-hero-tag">#2PYG0QV</span>
                <p className="page-hero-motto">«{s.clanMotto}»</p>
              </div>
              <div className="rank-medal rank-medal-1">
                <span className="rank-medal-num">1</span>
                <span className="rank-medal-of">/ 34</span>
              </div>
            </div>
          </header>
        </div>
        <div className="sc-chips">
          {DESIGNS.map(k => (
            <button key={k} className={`sc-chip ${design === k ? 'sc-chip-on' : ''}`}
              onClick={() => { haptic('light'); setDesign(k) }}>{t.hallPage.designs[k]}</button>
          ))}
        </div>
      </Block>

      <Block icon="medal" tone="gold" title={s.achTitle} hint={s.achHint}>
        <ChatDemo replay={s.replay} bubbles={[
          { text: '', delay: 0.4, achievement: s.achText.replace('{name}', name) },
        ]} />
      </Block>

      <div className="sc-duo">
        <section className="card sc-mini">
          <span className="sc-gift"><IconTile name="gift" tone="violet" size={44} /></span>
          <b>{s.giftTitle}</b>
          <span className="muted small">{s.giftHint}</span>
        </section>
        <section className="card sc-mini">
          <span className="sc-gift sc-gift-alt"><IconTile name="megaphone" tone="orange" size={44} /></span>
          <b>{s.voiceTitle}</b>
          <span className="muted small">{s.voiceHint}</span>
        </section>
      </div>
    </>
  )
}

function Swatches({ keys, value, onPick }: { keys: BackgroundKey[]; value: BackgroundKey; onPick: (k: BackgroundKey) => void }) {
  return (
    <div className="sc-swatches">
      {keys.map(k => (
        <button key={k} className={`sc-swatch ${value === k ? 'sc-swatch-on' : ''}`}
          style={{ backgroundImage: `url(/bg/${k}.webp)` }}
          onClick={() => { haptic('light'); onPick(k) }} aria-label={k} />
      ))}
    </div>
  )
}

/* ================= Общее ================= */

function Block({ icon, tone, title, hint, demo, children }: {
  icon: IconName; tone: 'blue' | 'red' | 'violet' | 'green' | 'gold' | 'orange'
  title: string; hint: string; demo?: string; children: ReactNode
}) {
  return (
    <section className="card sc-block fade-up">
      <SectionHead icon={icon} tone={tone} title={title} />
      <p className="muted small sc-hint">{hint}</p>
      {children}
      {demo && <p className="sc-demo"><Icon name="info" size={12} /> {demo}</p>}
    </section>
  )
}

interface Bubble {
  text: string
  delay: number
  mine?: boolean
  strong?: boolean
  plus?: string
  buttons?: string[]
  achievement?: string
}

/**
 * Переписка с ботом, которая «печатается» на глазах: сообщения появляются по одному, а
 * перед ответом бота - «печатает…». Запускается, когда блок долистали до экрана;
 * «Ещё раз» - проиграть заново.
 */
function ChatDemo({ bubbles, replay, card = false }: { bubbles: Bubble[]; replay: string; card?: boolean }) {
  const [ref, seen] = useInView<HTMLDivElement>()
  const [run, setRun] = useState(0)
  const [shown, setShown] = useState(REDUCED ? bubbles.length : 0)

  // Сообщения берём из ref: родитель передаёт новый массив на каждой отрисовке, и от
  // этого переписка не должна начинаться заново
  const list = useRef(bubbles)
  list.current = bubbles
  useEffect(() => {
    if (!seen) return
    if (REDUCED) { setShown(list.current.length); return }
    setShown(0)
    const timers = list.current.map((b, i) => window.setTimeout(() => setShown(i + 1), b.delay * 1000))
    return () => timers.forEach(id => window.clearTimeout(id))
  }, [seen, run])

  const next = bubbles[shown]
  const done = shown >= bubbles.length

  return (
    <div ref={ref} className="sc-chat">
      <div className={`sc-chat-feed ${card ? 'sc-chat-card' : ''}`}>
        {bubbles.slice(0, shown).map((b, i) => b.achievement ? (
          <div key={`${run}-${i}`} className="sc-bubble sc-ach">
            <div className="sc-ach-card">
              <span className="sc-ach-medal"><Icon name="medal" size={30} /></span>
              <span className="sc-ach-shine" />
            </div>
            <div className="sc-ach-text">🏅 {b.achievement}</div>
          </div>
        ) : (
          <div key={`${run}-${i}`}
            className={`sc-bubble ${b.mine ? 'sc-mine' : 'sc-bot'} ${b.strong ? 'sc-strong' : ''} ${b.plus ? 'sc-plusline' : ''}`}>
            {b.plus && <span className="sc-plus-tag"><Icon name="gem" size={10} /> {b.plus}</span>}
            {b.text}
            {b.buttons && (
              <div className="sc-bubble-btns">{b.buttons.map(x => <span key={x}>{x}</span>)}</div>
            )}
          </div>
        ))}
        {seen && next && !next.mine && <div className="sc-typing"><i /><i /><i /></div>}
      </div>
      {done && (
        <button className="sc-replay" onClick={() => { haptic('light'); setRun(n => n + 1) }}>
          <Icon name="refresh" size={13} /> {replay}
        </button>
      )}
    </div>
  )
}

function Table({ head, rows, accent }: { head: string[]; rows: string[]; accent: number }) {
  return (
    <div className="sc-table" style={{ gridTemplateColumns: `1.6fr repeat(${head.length}, 1fr)` }}>
      <span />
      {head.map((h, i) => <span key={h} className={`sc-th ${i === accent ? 'sc-th-on' : ''}`}>{h}</span>)}
      {rows.map(r => {
        const [feature, ...cells] = r.split('|')
        return [
          <span key={`${r}-f`} className="sc-td-f">{feature}</span>,
          ...cells.map((c, i) => (
            <span key={`${r}-${i}`} className={`sc-td ${i === accent ? 'sc-td-on' : ''} ${c === '—' ? 'sc-td-no' : ''}`}>
              {c === '✓' ? <Icon name="check" size={15} /> : c}
            </span>
          )),
        ]
      })}
    </div>
  )
}

function Compare({ t, highlight }: { t: Translations; highlight: ShowcaseTab }) {
  const s = t.showcase
  return (
    <section className="card sc-block">
      <SectionHead icon="columns" tone="blue" title={s.compareTitle} />
      <Table head={[s.colFree, s.colPlus, s.colSponsor]} rows={s.compareRows} accent={highlight === 'plus' ? 1 : 2} />
    </section>
  )
}

/* ================= Демо-данные ================= */

function demoData() {
  const start = Date.UTC(2026, 9, 17, 16, 42)
  const results: [number, number, number, number][] = [
    [1, 3, 1, 31], [-1, 0, 1, -29], [1, 2, 0, 30], [1, 1, 0, 28], [-1, 1, 2, -30], [-1, 0, 3, -31], [1, 2, 1, 32], [1, 3, 0, 33],
  ]
  const archs = ['Log Bait', 'Golem', 'Hog Cycle', 'X-Bow', 'Lava Loon', 'Mega Knight', 'Hog Cycle', 'Log Bait']
  const matches: MatchRow[] = results.map(([result, cf, ca, tc], i) => ({
    id: i + 1,
    timeUtc: new Date(start + i * 4 * 60_000).toISOString(),
    mode: 'ladder',
    result,
    crownsFor: cf,
    crownsAgainst: ca,
    trophyChange: tc,
    oppName: null,
    arch: { key: archs[i], label: archs[i], card: null },
    keyCards: [],
    verdict: null,
    levelGap: null,
  }))
  const session: MatchSession = {
    startUtc: matches[0].timeUtc,
    endUtc: matches[matches.length - 1].timeUtc,
    wins: 5, losses: 3, draws: 0,
    trophies: matches.reduce((a, m) => a + (m.trophyChange ?? 0), 0),
    tilt: false,
    strip: 'WLWWLLWW',
    matches,
  }

  // Тепловая карта (части дня: 0 ночь, 1 утро, 2 день, 3 вечер): вечера будней - лучшее
  // время, ночь - худшее. Так в демо видно, ради чего карта: «когда садиться играть»
  const heat: { weekday: number; part: number; games: number; wins: number }[] = []
  for (let d = 0; d < 7; d++) {
    for (let p = 0; p < 4; p++) {
      const games = [4, 5, 7, 12][p] + ((d * 3 + p) % 4)
      const rate = [d >= 5 ? 0.3 : 0.4, 0.5, 0.56, d < 5 ? 0.7 : 0.6][p]
      heat.push({ weekday: d, part: p, games, wins: Math.round(games * rate) })
    }
  }

  const arch: ArchetypeRow[] = [
    ['Hog Cycle', 34, 22, 6], ['Log Bait', 28, 17, 4], ['Golem', 21, 7, -21], ['Mega Knight', 19, 7, -18],
    ['X-Bow', 12, 8, 11], ['Lava Loon', 10, 6, 5],
  ].map(([label, games, wins, deltaPp]) => ({
    arch: { key: String(label), label: String(label), card: null },
    games: Number(games), wins: Number(wins), losses: Number(games) - Number(wins), deltaPp: Number(deltaPp),
  }))

  const matchup: Matchup = {
    top: [
      { key: 'exact', wins: 418, draws: 22, losses: 371, games: 811, winPercent: 51.5, reliability: 'reliable' },
      { key: 'seven', wins: 1240, draws: 61, losses: 1012, games: 2313, winPercent: 53.6, reliability: 'reliable' },
      { key: 'wincon', wins: 5210, draws: 230, losses: 4402, games: 9842, winPercent: 52.9, reliability: 'reliable' },
    ],
    topBattles: 12_480,
    headline: 'exact',
    own: [
      { key: 'exact', wins: 6, draws: 0, losses: 9, games: 15, winPercent: 40, reliability: 'adequate' },
      { key: 'wincon', wins: 14, draws: 1, losses: 16, games: 31, winPercent: 45.2, reliability: 'adequate' },
    ],
    ownLocked: false,
    me: { avgElixir: 3.1, cycle: 7 },
    them: { avgElixir: 4.4, cycle: 13 },
  }

  return { session, heat, arch, matchup }
}
