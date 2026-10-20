import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { OPEN_FEEDBACK } from '../lib/feedback'
import { PLUS_CHANGED } from '../lib/plusSheet'
import { haptic, hapticNotify, initData, startParam } from '../lib/telegram'
import type { FeedbackMine } from '../types'
import { Icon } from './ui/Icon'

const WANTS = ['deep_review', 'deck_for_me', 'war_scout', 'share_cards', 'daily_goals', 'tournaments', 'leader_tools', 'rivals']
const BUYER_REASONS = ['tilt', 'review', 'tracker', 'star', 'support']
const PLAYER_REASONS = ['price', 'unclear', 'no_stars', 'not_needed', 'didnt_see']

/**
 * Опрос «что добавить»: какие функции нужны и почему купил (или что мешает купить).
 * Открывается из карточки в клане и по кнопке бота (startapp=feedback).
 */
export function FeedbackSheet() {
  const { t } = useT()
  const s = t.fb
  const [open, setOpen] = useState(startParam === 'feedback' && Boolean(initData))
  const [mine, setMine] = useState<FeedbackMine | null>(null)
  const [wants, setWants] = useState<string[]>([])
  const [reason, setReason] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle')
  const [granted, setGranted] = useState(0)

  useEffect(() => {
    const go = () => setOpen(true)
    window.addEventListener(OPEN_FEEDBACK, go)
    return () => window.removeEventListener(OPEN_FEEDBACK, go)
  }, [])

  useEffect(() => {
    if (!open || mine) return
    api.getFeedback().then(setMine).catch(() => setMine({ answered: false, isBuyer: false, rewardPlusDays: 0 }))
  }, [open, mine])

  if (!open) return null
  const close = () => { haptic('light'); setOpen(false); if (state === 'done') setState('idle') }
  const toggle = (k: string) => { haptic('light'); setWants(w => w.includes(k) ? w.filter(x => x !== k) : [...w, k]) }
  const canSend = wants.length > 0 || reason !== null || text.trim().length > 0
  const reward = mine && !mine.answered ? mine.rewardPlusDays : 0

  const send = async () => {
    if (!canSend || state === 'sending') return
    haptic('medium')
    setState('sending')
    try {
      const r = await api.sendFeedback(wants, reason, text.trim())
      setGranted(r.grantedDays)
      setState('done')
      hapticNotify('success')
      if (r.grantedDays > 0) window.dispatchEvent(new Event(PLUS_CHANGED))
      setMine(m => m ? { ...m, answered: true } : m)
    } catch {
      setState('error')
    }
  }

  return (
    <div className="modal-backdrop fb-backdrop" onClick={close}>
      <div className="fb-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <button className="fb-x" aria-label={s.close} onClick={close}><Icon name="x" size={16} /></button>
        {state === 'done' ? (
          <div className="fb-done">
            <span className="fb-done-ic"><Icon name={granted > 0 ? 'gift' : 'checkCircle'} size={30} /></span>
            <h2>{granted > 0 ? s.thanksReward.replace('{days}', String(granted)) : s.thanks}</h2>
            <button className="btn" onClick={close}>{s.close}</button>
          </div>
        ) : (
          <>
            <h2 className="fb-title">{s.title}</h2>
            <p className="fb-lead">{s.lead}</p>

            <p className="fb-q">{s.wantsQ}</p>
            <div className="fb-chips">
              {WANTS.map(k => (
                <button key={k} className={`fb-chip ${wants.includes(k) ? 'fb-on' : ''}`} onClick={() => toggle(k)}>
                  {wants.includes(k) && <Icon name="check" size={13} />} {s.wants[k]}
                </button>
              ))}
            </div>

            {mine && (
              <>
                <p className="fb-q">{mine.isBuyer ? s.buyerQ : s.playerQ}</p>
                <div className="fb-chips">
                  {(mine.isBuyer ? BUYER_REASONS : PLAYER_REASONS).map(k => (
                    <button key={k} className={`fb-chip ${reason === k ? 'fb-on' : ''}`}
                      onClick={() => { haptic('light'); setReason(r => r === k ? null : k) }}>
                      {s.reasons[k]}
                    </button>
                  ))}
                </div>
              </>
            )}

            <p className="fb-q">{s.textQ}</p>
            <textarea className="fb-text" rows={3} maxLength={500} placeholder={s.textPh}
              value={text} onChange={e => setText(e.target.value)} />

            {state === 'error' && <p className="fb-error">{s.error}</p>}
            <button className="btn fb-send" disabled={!canSend || state === 'sending'} onClick={send}>
              {state === 'sending' ? <span className="spinner" /> : reward > 0 ? s.sendReward.replace('{days}', String(reward)) : s.send}
            </button>
          </>
        )}
      </div>
    </div>
  )
}
