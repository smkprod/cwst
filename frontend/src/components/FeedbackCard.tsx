import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { FEEDBACK_DISMISSED, openFeedback } from '../lib/feedback'
import { haptic } from '../lib/telegram'
import type { FeedbackMine } from '../types'
import { Icon } from './ui/Icon'

/** Карточка «что добавить» на первом экране - пока человек не ответил и не закрыл её. */
export function FeedbackCard() {
  const { t } = useT()
  const s = t.fb
  const [mine, setMine] = useState<FeedbackMine | null>(null)
  const [off, setOff] = useState(() => {
    try { return localStorage.getItem(FEEDBACK_DISMISSED) === '1' } catch { return false }
  })

  useEffect(() => {
    if (off) return
    api.getFeedback().then(setMine).catch(() => { /* нет ответа - нет карточки */ })
  }, [off])

  if (off || !mine || mine.answered) return null
  const dismiss = () => {
    haptic('light')
    setOff(true)
    try { localStorage.setItem(FEEDBACK_DISMISSED, '1') } catch { /* пусть так */ }
  }

  return (
    <div className="card fb-card fade-in">
      <button className="fb-card-x" aria-label={s.close} onClick={dismiss}><Icon name="x" size={14} /></button>
      <span className="fb-card-ic"><Icon name="message" size={20} /></span>
      <div className="fb-card-body">
        <b>{s.cardTitle}</b>
        <span className="muted small">{s.cardText}</span>
        {mine.rewardPlusDays > 0 && <span className="fb-card-reward"><Icon name="gem" size={13} /> {s.cardReward.replace('{days}', String(mine.rewardPlusDays))}</span>}
      </div>
      <button className="btn-mini" onClick={() => { haptic('light'); openFeedback() }}>{s.cardGo}</button>
    </div>
  )
}
