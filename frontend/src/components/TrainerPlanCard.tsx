import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TrainerPlan } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { usePlusSheet } from '../lib/plusSheet'
import { useCardArt } from '../lib/cardArt'
import { CARD_NAMES, PUZZLES, loadStars, openTrainer } from '../lib/trainer'
import { emojiOf } from '../lib/sim/emoji'
import { Icon } from './ui/Icon'

/**
 * «Тренер подобрал задачи»: против каких карт игрок проигрывает чаще обычного - по
 * его журналу боёв - и сразу кнопка на задачу против этой карты. Выбор - статистика,
 * его видят все; слово тренера от ИИ - в Плюсе.
 */
export function TrainerPlanCard() {
  const { t, lang } = useT()
  const a = t.ai
  const art = useCardArt()
  const openPlus = usePlusSheet()
  const [plan, setPlan] = useState<TrainerPlan | null>(null)

  useEffect(() => {
    let alive = true
    api.getTrainerPlan(lang).then(p => { if (alive) setPlan(p) }).catch(() => { /* карточки просто не будет */ })
    return () => { alive = false }
  }, [lang])

  if (!plan) return null
  const stars = loadStars()

  // Задача против карты: сначала ещё не решённая на три звезды
  const puzzleFor = (card: string) => {
    const all = PUZZLES.filter(p => p.enemyCard === card)
    return all.find(p => (stars[p.id] ?? 0) < 3) ?? all[0]
  }

  return (
    <section className="card tp-card fade-in">
      <div className="tp-head">
        <span className="tp-icon"><Icon name="target" size={18} /></span>
        <div>
          <b>{a.trainerTitle}</b>
          <span className="muted small">{plan.focus.length > 0 ? a.trainerLead : a.trainerNone}</span>
        </div>
      </div>

      {plan.focus.length > 0 && (
        <div className="tp-list">
          {plan.focus.map(f => {
            const pz = puzzleFor(f.card)
            return (
              <button key={f.card} className="tp-item" disabled={!pz}
                onClick={() => { if (pz) { haptic('medium'); openTrainer(pz.id) } }}>
                <span className="tp-art">{art[f.card] ? <img src={art[f.card]} alt="" /> : <span className="tr-emoji">{emojiOf(f.card)}</span>}</span>
                <span className="tp-name">{CARD_NAMES[lang][f.card] ?? f.card}</span>
                <span className="tp-win">{a.trainerWin.replace('{p}', String(Math.round(f.winPercent)))}</span>
                <span className="tp-go"><Icon name="play" size={11} /> {a.trainerGo}</span>
              </button>
            )
          })}
        </div>
      )}

      {plan.coach && (
        <div className="ai-box tp-coach fade-in">
          <div className="ai-box-head">
            <span className="ai-badge"><Icon name="sparkles" size={11} /> {a.badge}</span>
            <span className="muted small">{a.trainerGames.replace('{n}', String(plan.games))}</span>
          </div>
          <p className="ai-line">{plan.coach}</p>
        </div>
      )}
      {plan.locked && plan.focus.length > 0 && (
        <button className="tp-locked" onClick={() => { haptic('light'); openPlus('plus') }}>
          <Icon name="sparkles" size={14} /> {a.trainerLocked} <Icon name="chevronRight" size={14} />
        </button>
      )}
    </section>
  )
}
