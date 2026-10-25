import { useState } from 'react'
import type { AiText } from '../types'
import { ApiError } from '../lib/api'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { Icon } from './ui/Icon'

type State = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ready'; text: string } | { kind: 'empty' } | { kind: 'off' } | { kind: 'error' }

/**
 * Кнопка «спросить ИИ» и его ответ. Текст пишется только по нашим цифрам - об этом
 * подпись «по цифрам бота»: человек должен понимать, откуда выводы, и не ждать от
 * модели советов про карты.
 *
 * copy - ответ предназначен, чтобы его отправить (сообщение игроку): рядом кнопка
 * «скопировать», а сам текст выглядит как черновик сообщения.
 */
export function AiBox({ label, hint, load, copy = false, compact = false }: {
  label: string
  hint?: string
  load: () => Promise<AiText | null>
  copy?: boolean
  compact?: boolean
}) {
  const { t } = useT()
  const a = t.ai
  const [state, setState] = useState<State>({ kind: 'idle' })
  const [copied, setCopied] = useState(false)

  const ask = () => {
    haptic('medium')
    setState({ kind: 'loading' })
    load()
      .then(r => setState(r?.text ? { kind: 'ready', text: r.text } : { kind: 'empty' }))
      .catch(e => setState(e instanceof ApiError && e.code === 'ai_off' ? { kind: 'off' } : { kind: 'error' }))
  }

  const doCopy = (text: string) => {
    haptic('light')
    const done = () => { setCopied(true); window.setTimeout(() => setCopied(false), 1800) }
    navigator.clipboard?.writeText(text).then(done).catch(() => { /* старый Telegram - выделит сам */ })
  }

  if (state.kind === 'idle' || state.kind === 'error') {
    return (
      <button className={`ai-ask ${compact ? 'ai-ask-compact' : ''}`} onClick={ask}>
        <span className="ai-spark"><Icon name="sparkles" size={compact ? 13 : 16} /></span>
        <span className="ai-ask-text">
          <b>{label}</b>
          {!compact && (state.kind === 'error' ? <span className="small ai-err">{a.error}</span> : hint && <span className="muted small">{hint}</span>)}
        </span>
        {!compact && <span className="ai-badge">{a.badge}</span>}
      </button>
    )
  }

  if (state.kind === 'loading') {
    return (
      <div className={`ai-box ai-box-loading ${compact ? 'ai-box-compact' : ''}`}>
        <span className="ai-dots"><i /><i /><i /></span> <span className="muted small">{a.writing}</span>
      </div>
    )
  }

  if (state.kind !== 'ready') {
    return <div className={`ai-box ${compact ? 'ai-box-compact' : ''}`}><p className="muted small" style={{ margin: 0 }}>{state.kind === 'off' ? a.off : a.empty}</p></div>
  }

  const lines = state.text.split('\n').filter(Boolean)
  return (
    <div className={`ai-box fade-in ${copy ? 'ai-box-draft' : ''} ${compact ? 'ai-box-compact' : ''}`}>
      <div className="ai-box-head">
        <span className="ai-badge"><Icon name="sparkles" size={11} /> {a.badge}</span>
        <span className="muted small">{label} · {a.note}</span>
      </div>
      {copy
        ? <p className="ai-draft">{state.text}</p>
        : lines.map((l, i) => <p key={i} className="ai-line" style={{ animationDelay: `${i * 120}ms` }}>{l}</p>)}
      {copy && (
        <button className="btn-mini ai-copy" onClick={() => doCopy(state.text)}>
          <Icon name={copied ? 'check' : 'copy'} size={13} /> {copied ? a.copied : a.copy}
        </button>
      )}
    </div>
  )
}
