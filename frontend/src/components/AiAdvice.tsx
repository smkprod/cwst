import { useState } from 'react'
import { ApiError } from '../lib/api'
import type { AiAnswer } from '../types'
import { useT } from '../lib/i18n'
import { haptic, hapticNotify } from '../lib/telegram'
import { usePlusSheet } from '../lib/plusSheet'
import { Icon } from './ui/Icon'

/**
 * Кнопка «спросить ИИ» и его ответ. Лимит - не ошибка, а повод показать Плюс: бесплатно
 * попробовать раз в день, с Плюсом пользоваться. Ответ - обычный текст с пунктами «• ».
 */
export function AiAdvice({ run, label, compact = false }: {
  run: () => Promise<AiAnswer>
  label: string
  /** Внутри карточки колоды - без отдельной рамки. */
  compact?: boolean
}) {
  const { t } = useT()
  const a = t.ai
  const openPlus = usePlusSheet()
  const [busy, setBusy] = useState(false)
  const [answer, setAnswer] = useState<AiAnswer | null>(null)
  const [failed, setFailed] = useState(false)

  const ask = async () => {
    haptic('medium')
    setBusy(true)
    setFailed(false)
    try {
      const r = await run()
      setAnswer(r)
      hapticNotify(r.status === 'ok' ? 'success' : 'warning')
    } catch (e) {
      hapticNotify('error')
      setFailed(true)
      if (e instanceof ApiError && e.code === 'player_not_linked') setAnswer(null)
    } finally {
      setBusy(false)
    }
  }

  const status = answer?.status
  return (
    <div className={`ai-advice ${compact ? 'ai-advice-compact' : ''}`}>
      {answer?.text && <div className="ai-advice-text">{answer.text}</div>}
      {status === 'limit' && (
        <div className="ai-advice-note">
          <span>{answer!.plus ? a.limitPlus : a.limitFree.replace('{m}', String(answer!.plusLimit))}</span>
          {!answer!.plus && (
            <button className="btn plus-cta ai-advice-plus" onClick={() => openPlus()}>
              <Icon name="gem" size={15} /> {a.getPlus}
            </button>
          )}
        </div>
      )}
      {status === 'noai' && <p className="muted small ai-advice-note">{a.noAi}</p>}
      {(status === 'failed' || failed) && <p className="muted small ai-advice-note">{a.failed}</p>}
      {status !== 'limit' && (
        <button className={compact ? 'btn-mini ai-advice-btn' : 'btn ai-advice-main'} disabled={busy} onClick={ask}>
          <Icon name="sparkles" size={compact ? 14 : 16} />
          {busy ? a.thinking : answer?.text ? a.again : label}
        </button>
      )}
      {status === 'ok' && (
        <p className="muted small ai-advice-left">
          {a.left.replace('{n}', String(answer!.left)).replace('{m}', String(answer!.limit))}
        </p>
      )}
    </div>
  )
}
