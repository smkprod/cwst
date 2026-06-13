import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'

type NudgeState = 'idle' | 'sending' | 'done' | 'cooldown'

export function NudgeButton({ notPlayedCount, isPro }: { notPlayedCount: number; isPro: boolean }) {
  const [state, setState] = useState<NudgeState>('idle')
  const [resultText, setResultText] = useState('')

  if (notPlayedCount <= 0 && state === 'idle') return null

  const nudge = async () => {
    haptic('heavy')
    setState('sending')
    try {
      const r = await api.nudgeSlackers()
      hapticNotify('success')
      const parts: string[] = []
      if (r.notifiedDm > 0) parts.push(`✉️ в ЛС: ${r.notifiedDm}`)
      if (r.postedToChat) parts.push(`💬 в чат: ${r.unlinkedCount}`)
      if (r.skippedCooldown > 0) parts.push(`⏸ недавно пинали: ${r.skippedCooldown}`)
      setResultText(parts.length > 0 ? `Пнул! ${parts.join(' · ')}` : 'Все уже сыграли 🎉')
      setState('done')
    } catch (e) {
      hapticNotify('error')
      setResultText(
        e instanceof ApiError && e.code === 'no_war_day'
          ? 'Сейчас не день войны'
          : 'Не получилось — попробуй позже',
      )
      setState('cooldown')
    }
  }

  if (state === 'done' || state === 'cooldown') {
    return <div className={`nudge-result ${state === 'done' ? 'nudge-ok' : 'nudge-fail'} fade-in`}>{resultText}</div>
  }

  return (
    <button className="btn btn-nudge" onClick={nudge} disabled={state === 'sending'}>
      {state === 'sending'
        ? 'Пинаю…'
        : `👊 Пнуть лентяев (${notPlayedCount})${!isPro && notPlayedCount > 20 ? ' · лимит 20 на Free' : ''}`}
    </button>
  )
}
