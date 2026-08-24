import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'

interface Props {
  /** Текущая аватарка игрока (из /players/me). */
  current?: string
  /** Сообщить наверх, что аватарка сменилась — чтобы обновить её рядом с именем. */
  onChange?: (emoji: string | null) => void
}

/**
 * Выбор эмодзи-аватарки. Набор приходит с сервера — он же его и проверяет при
 * сохранении, так что список в одном месте и разъехаться не может.
 *
 * Выбор применяется сразу, без кнопки «сохранить»: одно нажатие — одно действие.
 * Если сервер не принял, возвращаем прежнюю аватарку и говорим об этом, а не
 * оставляем экран, который врёт.
 */
export function AvatarPicker({ current, onChange }: Props) {
  const { t } = useT()
  const [avatars, setAvatars] = useState<string[]>([])
  const [selected, setSelected] = useState<string | null>(current ?? null)
  const [failed, setFailed] = useState(false)

  useEffect(() => { setSelected(current ?? null) }, [current])

  useEffect(() => {
    let alive = true
    api.getAvatars()
      .then(r => { if (alive) setAvatars(r.avatars) })
      .catch(() => { /* список не пришёл — карточка просто не покажется */ })
    return () => { alive = false }
  }, [])

  if (avatars.length === 0) return null

  const pick = async (emoji: string | null) => {
    haptic('light')
    const previous = selected
    setSelected(emoji)
    setFailed(false)
    try {
      await api.setMyAvatar(emoji ?? '')
      onChange?.(emoji)
    } catch {
      setSelected(previous)
      setFailed(true)
    }
  }

  return (
    <section className="card avatar-card">
      <div className="card-title">{t.avatar.title}</div>
      <p className="muted small" style={{ margin: '2px 0 8px' }}>{t.avatar.hint}</p>

      <div className="avatar-grid">
        <button
          className={`avatar-opt avatar-opt-none ${selected === null ? 'avatar-opt-on' : ''}`}
          onClick={() => pick(null)}
          aria-label={t.avatar.none}
          aria-pressed={selected === null}
        >✖️</button>

        {avatars.map(emoji => (
          <button
            key={emoji}
            className={`avatar-opt ${selected === emoji ? 'avatar-opt-on' : ''}`}
            onClick={() => pick(emoji)}
            aria-pressed={selected === emoji}
          >{emoji}</button>
        ))}
      </div>

      {failed && <p className="muted small" style={{ marginTop: 8 }}>⚠️ {t.avatar.error}</p>}
    </section>
  )
}
