import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'

type NudgeState = 'idle' | 'sending' | 'done' | 'cooldown'

export function NudgeButton({ notPlayedCount, isPro }: { notPlayedCount: number; isPro: boolean }) {
  const [state, setState] = useState<NudgeState>('idle')
  const [resultText, setResultText] = useState('')
  const { t } = useT()

  if (notPlayedCount <= 0 && state === 'idle') return null

  const nudge = async () => {
    haptic('heavy')
    setState('sending')
    try {
      const r = await api.nudgeSlackers()
      hapticNotify('success')
      const parts: string[] = []
      if (r.notifiedDm > 0) parts.push(`${t.nudge.dm} ${r.notifiedDm}`)
      if (r.postedToChat) parts.push(`${t.nudge.chat} ${r.taggableCount}`)
      if (r.skippedCooldown > 0) parts.push(`${t.nudge.skipped} ${r.skippedCooldown}`)
      setResultText(parts.length > 0 ? `${t.nudge.done} ${parts.join(' · ')}` : t.nudge.allPlayed)
      setState('done')
    } catch (e) {
      hapticNotify('error')
      setResultText(
        e instanceof ApiError && e.code === 'no_war_day'
          ? t.nudge.noWar
          : e instanceof ApiError && (e.code === 'cr_api_unavailable' || e.code === 'cr_api_token_invalid')
            ? t.nudge.crUnavailable
            : t.nudge.error,
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
        ? t.nudge.sending
        : `👊 ${t.nudge.label} (${notPlayedCount})${!isPro && notPlayedCount > 20 ? t.nudge.freeLimit : ''}`}
    </button>
  )
}
