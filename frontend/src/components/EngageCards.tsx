import { useState } from 'react'
import { api, ApiError } from '../lib/api'
import { getBotUsername, haptic, hapticNotify, openTelegramLink, shareToTelegram } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { SectionHead } from './ui/Section'
import { Icon } from './ui/Icon'

const DISMISS_DAYS = 7

function dismissedRecently(key: string): boolean {
  try {
    const at = Number(localStorage.getItem(key) ?? 0)
    return at > 0 && Date.now() - at < DISMISS_DAYS * 86_400_000
  } catch { return false }
}

/**
 * Бот не в чате клана. Лидеру - кнопка «Добавить бота в чат»: ссылка ?startgroup открывает
 * выбор группы, а бот, получив там /start setup_ТЕГ, подключает клан сам - без /setup.
 * Остальным - «попросить лидера»: пересылка готового текста. Обычный игрок может её
 * скрыть на неделю, лидеру карточка нужна, пока чат не подключён.
 */
export function ConnectChatCard({ clanTag, isLeader }: { clanTag: string; isLeader: boolean }) {
  const { t } = useT()
  const e = t.engage
  const key = `connectChatHidden:${clanTag}`
  const [hidden, setHidden] = useState(() => !isLeader && dismissedRecently(key))
  if (hidden) return null

  const connect = () => {
    haptic('medium')
    const bot = getBotUsername()
    if (!bot) return
    openTelegramLink(`https://t.me/${bot}?startgroup=setup_${clanTag.replace('#', '').toUpperCase()}`)
  }
  const ask = () => { haptic('light'); shareToTelegram(e.connectShare) }
  const hide = () => {
    haptic('light')
    try { localStorage.setItem(key, String(Date.now())) } catch { /* приватный режим */ }
    setHidden(true)
  }

  return (
    <section className="card menu-notice fade-in">
      <SectionHead icon="chat" tone="orange" title={e.connectTitle} className="mx-connect-head" />
      <p className="muted small" style={{ margin: '0 0 10px' }}>{isLeader ? e.connectLeader : e.connectMember}</p>
      {isLeader ? (
        <button className="btn btn-nudge" style={{ width: '100%' }} onClick={connect}>
          <Icon name="plus" size={16} /> {e.connectButton}
        </button>
      ) : (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <button className="btn btn-nudge" style={{ flex: 1 }} onClick={ask}>
            <Icon name="send" size={16} /> {e.connectAsk}
          </button>
          <button className="btn-mini" onClick={hide} aria-label="×"><Icon name="x" size={14} /></button>
        </div>
      )}
    </section>
  )
}

/**
 * Лидеру во вкладке «Состав»: сколько игроков не привязано и кнопка «Позвать в чате» -
 * бот пишет в чат клана список с кнопкой привязки. Раз в сутки (проверяет сервер).
 */
export function InviteUnlinkedCard({ unlinked, hasChat }: { unlinked: number; hasChat: boolean }) {
  const { t } = useT()
  const e = t.engage
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)
  if (unlinked <= 0) return null

  const invite = async () => {
    haptic('medium')
    setBusy(true)
    try {
      await api.inviteUnlinked()
      hapticNotify('success')
      setNote(e.inviteDone)
    } catch (err) {
      hapticNotify('error')
      const code = err instanceof ApiError ? err.code : ''
      setNote(code === 'too_soon' ? e.inviteTooSoon
        : code === 'no_chat' ? e.inviteNoChat
        : code === 'nobody' ? e.inviteNobody
        : e.inviteError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card fade-in">
      <SectionHead icon="link" tone="violet" title={e.inviteTitle.replace('{n}', String(unlinked))} />
      <p className="muted small" style={{ margin: '0 0 10px' }}>{hasChat ? e.inviteText : e.inviteNoChat}</p>
      {hasChat && (
        <button className="btn btn-nudge" style={{ width: '100%' }} disabled={busy || note === e.inviteDone} onClick={invite}>
          <Icon name="megaphone" size={16} /> {e.inviteButton}
        </button>
      )}
      {note && <p className="muted small" style={{ margin: '8px 0 0', textAlign: 'center' }}>{note}</p>}
    </section>
  )
}
