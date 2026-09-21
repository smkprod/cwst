import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'
import { BotTourCard } from './BotTourCard'

/**
 * Первый экран человека, который ещё не привязал себя.
 *
 * Раньше здесь была нарисована команда /link #ТВОЙ_ТЕГ и всё: чтобы привязаться,
 * надо было закрыть приложение, найти чат с ботом и набрать её руками. Половина
 * на этом и заканчивала. Теперь поле и кнопка прямо тут, приложение закрывать не надо.
 *
 * Под формой короткий экскурс: человек, который ещё не привязался, не видит ни одного
 * рабочего экрана и иначе не узнает, ради чего вообще вводить тег.
 */
export function LinkPrompt({ onLinked }: { onLinked?: () => void }) {
  const { t } = useT()
  const [tag, setTag] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async () => {
    const clean = tag.trim()
    if (clean.length < 3) return

    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.linkMe(clean)
      hapticNotify('success')
      // Перезагружаем целиком: привязка меняет всё, что показывает приложение,
      // и точечно обновлять тут нечего.
      if (onLinked) onLinked()
      else window.location.reload()
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError && e.code === 'player_not_found'
        ? t.link.notFound
        : t.link.error)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fade-in">
      <div className="lang-switcher-bar">
        <LangSwitcher />
      </div>

      <section className="card link-card">
        <div className="card-title">{t.link.title}</div>
        <p className="muted small link-desc">{t.link.descApp}</p>

        <input
          className="search-input link-input"
          value={tag}
          onChange={e => setTag(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') submit() }}
          placeholder="#ABC123"
          autoCapitalize="characters"
          autoCorrect="off"
          spellCheck={false}
          maxLength={16}
        />

        <p className="muted small link-hint">{t.link.hint}</p>

        {error && <p className="form-error small">{error}</p>}

        <button className="btn link-btn" disabled={busy || tag.trim().length < 3} onClick={submit}>
          {busy ? t.link.linking : t.link.submit}
        </button>
      </section>

      <BotTourCard />
    </div>
  )
}
