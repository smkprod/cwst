import { useEffect, useMemo, useState } from 'react'
import { useT } from '../lib/i18n'
import { haptic, hapticNotify } from '../lib/telegram'
import { useCardArt } from '../lib/cardArt'
import { CARD_NAMES, cardInfo } from '../lib/trainer'
import { Sim, canDeploy, sideOf, type Team } from '../lib/sim/engine'
import { COST } from '../lib/sim/stats'
import { emojiOf } from '../lib/sim/emoji'
import emblem from '../assets/clanify-emblem.webp'
import { ArenaSim } from './ArenaSim'
import { Icon } from './ui/Icon'

/**
 * Песочница: игрок сам расставляет карты за обе стороны и смотрит, что будет, - тот же
 * движок и та же арена, что в задачах. Эликсир не считается: ставь что угодно и сколько
 * угодно, на паузе или прямо в бою. Радиусы показывают, кто кого видит и достаёт.
 */

/** Все карты, которые умеет движок, - в порядке «войска, здания, заклинания». */
const PALETTE = ['Knight', 'Hog Rider', 'Skeletons', 'Goblins', 'Ice Spirit', 'Bats', 'Mini P.E.K.K.A', 'Musketeer', 'Archers', 'Minions',
  'Valkyrie', 'Miner', 'Ice Golem', 'Giant', 'Balloon', 'Mega Knight', 'P.E.K.K.A', 'Golem', 'Cannon', 'Tesla', 'Mortar', 'X-Bow',
  'The Log', 'Tornado', 'Fireball', 'Goblin Barrel']

/** Куда встаёт карта стороны: здание 2×2 - на угол клеток, войско - в центр клетки, заклинание - на полклетки. */
function snapFor(card: string, x: number, y: number): { x: number; y: number } {
  const kind = cardInfo(card).kind
  if (kind === 'building') return { x: Math.min(17, Math.max(1, Math.round(x))), y: Math.min(31, Math.max(1, Math.round(y))) }
  if (kind === 'spell') return { x: Math.round(x * 2) / 2, y: Math.round(y * 2) / 2 }
  return { x: Math.min(17.5, Math.max(0.5, Math.floor(x) + 0.5)), y: Math.min(31.5, Math.max(0.5, Math.floor(y) + 0.5)) }
}

export function Sandbox({ onExit, details, onDetails }: { onExit: () => void; details: boolean; onDetails: () => void }) {
  const { t, lang } = useT()
  const s = t.trainer
  const art = useCardArt()
  const n = (card: string) => CARD_NAMES[lang][card] ?? card
  // Подписи радиусов: 🏰 - бьёт только здания, 🛩 - достаёт и воздух
  const [round, setRound] = useState(0)
  const sim = useMemo(() => new Sim(10), [round])
  const [team, setTeam] = useState<Team>('enemy')
  const [card, setCard] = useState('Hog Rider')
  const [running, setRunning] = useState(false)
  const [fast, setFast] = useState(false)
  const [hint, setHint] = useState<string | null>(null)
  const [, setTick] = useState(0)

  // Счёт урона по башням обновляется вместе с боем
  useEffect(() => {
    const id = setInterval(() => setTick(v => v + 1), 250)
    return () => clearInterval(id)
  }, [])

  const tap = (x: number, y: number) => {
    const p = snapFor(card, x, y)
    if (!canDeploy(team, card, p.x, p.y)) {
      setHint(cardInfo(card).kind === 'spell' ? null : sideOf(p.y) === team ? s.onTower : s.sandboxSide)
      hapticNotify('error')
      return
    }
    sim.play(team, card, p.x, p.y)
    setHint(null)
    haptic('light')
  }

  const reset = () => { haptic('light'); setRunning(false); setRound(r => r + 1) }

  return (
    <div className="tr-game fade-in">
      <div className="tr-top">
        <button className="btn-mini" onClick={() => { haptic('light'); onExit() }}><Icon name="chevronLeft" size={15} /> {s.back}</button>
        <button className={`tr-toggle ${details ? 'tr-toggle-on' : ''}`} onClick={onDetails}>📐 {s.details}</button>
      </div>
      <div className="tr-head">
        <h2 className="tr-title">{s.sandbox}</h2>
        <p className="tr-task">{s.sandboxLead}</p>
      </div>

      <div className="tr-stage">
        <div className="tr-stage-head"><img src={emblem} alt="" /><b>{s.arena}</b></div>
        <div className="tr-board tr-board-live">
          <ArenaSim key={round} sim={sim} running={running} speed={fast ? 2.5 : 1} onTap={tap} names={n} details={details} />
          <div className={`tr-turn ${hint ? 'tr-turn-bad' : ''}`}>
            {hint ?? `${team === 'me' ? s.sideMe : s.sideFoe}: ${n(card)} — ${s.sandboxTap}`}
          </div>
          <div className="sb-score">
            <span className="sb-foe">🏰 −{Math.round(sim.towerDamage.enemy)}</span>
            <span className="sb-me">🏰 −{Math.round(sim.towerDamage.me)}</span>
          </div>
        </div>

        <div className="tr-tray">
          <div className="sb-controls">
            <div className="sb-sides">
              <button className={`sb-side sb-side-foe ${team === 'enemy' ? 'on' : ''}`} onClick={() => { haptic('light'); setTeam('enemy') }}>{s.sideFoe}</button>
              <button className={`sb-side sb-side-me ${team === 'me' ? 'on' : ''}`} onClick={() => { haptic('light'); setTeam('me') }}>{s.sideMe}</button>
            </div>
            <button className={`sb-btn ${running ? '' : 'sb-play'}`} onClick={() => { haptic('medium'); setRunning(r => !r) }}>{running ? '⏸' : '▶'}</button>
            <button className={`sb-btn ${fast ? 'sb-fast' : ''}`} onClick={() => { haptic('light'); setFast(f => !f) }}>⏩</button>
            <button className="sb-btn" onClick={reset}><Icon name="refresh" size={16} /></button>
          </div>
          <div className="sb-palette">
            {PALETTE.map(c => (
              <button key={c} className={`sb-card ${card === c ? 'on' : ''}`} onClick={() => { haptic('light'); setCard(c) }}>
                <span className="sb-card-art">{art[c] ? <img src={art[c]} alt="" /> : <span className="tr-emoji">{emojiOf(c)}</span>}</span>
                <span className="tr-cost">{COST[c] ?? ''}</span>
                <small>{n(c)}</small>
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
