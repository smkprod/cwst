import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ChannelPost, ChannelState, ChannelToggle } from '../types'
import { haptic, hapticNotify, openTelegramLink, openExternalLink } from '../lib/telegram'
import { Icon, type IconName } from './ui/Icon'
import { IconTile, SectionHead, type Tone } from './ui/Section'
import { InfoButton } from './ui/Info'

/**
 * Канал бота: подключение, автопосты из данных бота, новости Clash Royale из лент
 * (черновиками на проверку) и ручные посты. Только по-русски, как и сам канал.
 */

const AUTO: { key: ChannelToggle; icon: IconName; tone: Tone; label: string; desc: string }[] = [
  { key: 'daily', icon: 'globe', tone: 'blue', label: 'Мировой топ дня',
    desc: 'Каждый день в 19:00 по Киеву: топ-3 мира, взлёт дня, порог топ-1000, популярные карты и игрок дня в лиге 1×1.' },
  { key: 'weekly', icon: 'swords', tone: 'orange', label: 'Неделя лиги 1×1',
    desc: 'По понедельникам в 12:00: топ-5 лиги дуэлей с рангами, сколько дуэлей сыграно и рывок недели.' },
  { key: 'challenge', icon: 'ticket', tone: 'green', label: 'Челленджи',
    desc: 'Анонс, когда челлендж стартует, и итоги с победителями через 45 минут после конца.' },
  { key: 'cards', icon: 'cards', tone: 'violet', label: 'Новые карты',
    desc: 'Бот раз в 6 часов сверяет справочник карт игры: новая карта или эволюция - сразу пост с картинкой.' },
]

const BUTTONS: { key: string; label: string; url: string }[] = [
  { key: 'app', label: 'Открыть бота', url: 'startapp:' },
  { key: 'duel', label: 'Дуэли', url: 'startapp:duel' },
  { key: 'challenge', label: 'Челлендж', url: 'startapp:challenge' },
  { key: 'meta', label: 'Мировой топ', url: 'startapp:meta' },
  { key: 'link', label: 'Своя ссылка', url: '' },
  { key: 'none', label: 'Без кнопки', url: '' },
]

const KIND_LABEL: Record<string, string> = {
  manual: 'Свой пост', news: 'Новость', daily: 'Топ дня', weekly: 'Неделя лиги',
  chstart: 'Старт челленджа', chend: 'Итоги челленджа', card: 'Новая карта',
}

function errorText(e: unknown): string {
  if (!(e instanceof ApiError)) return 'Не получилось, попробуйте ещё раз'
  switch (e.code) {
    case 'not_found': return 'Канал не найден. Проверьте @имя и что бот добавлен в канал. Для приватного канала нужен его числовой id (-100…), ссылка-приглашение не подойдёт'
    case 'not_admin': return 'Бот не админ канала или у него нет права «Публикация сообщений»'
    case 'no_channel': return 'Сначала подключите канал'
    case 'empty': return 'Пустой текст'
    case 'too_long': return 'Слишком длинно - до 4000 символов'
    case 'telegram_failed': return 'Telegram не принял пост: проверьте права бота в канале'
    case 'no_data': return 'Пока нет данных для такого поста'
    default: return 'Не получилось, попробуйте ещё раз'
  }
}

function when(iso: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  return d.toLocaleString('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
}

export function ChannelSection() {
  const [state, setState] = useState<ChannelState | null>(null)
  const [failed, setFailed] = useState(false)

  const load = useCallback(() => {
    api.channelGet().then(setState).catch(() => setFailed(true))
  }, [])
  useEffect(load, [load])

  if (failed) return <p className="center muted">Не удалось загрузить канал</p>
  if (!state) return <div className="center"><div className="spinner" /></div>

  return (
    <>
      <ConnectCard state={state} onState={setState} />
      {state.id !== null && (
        <>
          <ComposeCard onState={setState} />
          <DraftsCard state={state} onState={setState} />
          <AutoCard state={state} onState={setState} />
          <NewsCard state={state} onState={setState} />
          <RecentCard posts={state.recent} />
        </>
      )}
    </>
  )
}

type Props = { state: ChannelState; onState: (s: ChannelState) => void }

function ConnectCard({ state, onState }: Props) {
  const [handle, setHandle] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmOff, setConfirmOff] = useState(false)

  const connect = async () => {
    if (!handle.trim()) return
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      onState(await api.channelConnect(handle.trim()))
      hapticNotify('success')
      setHandle('')
    } catch (e) {
      hapticNotify('error')
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const disconnect = async () => {
    if (!confirmOff) { setConfirmOff(true); return }
    setBusy(true)
    try { onState(await api.channelDisconnect()) } finally { setBusy(false); setConfirmOff(false) }
  }

  if (state.id !== null) {
    return (
      <div className="card">
        <SectionHead icon="send" tone="blue" title="Канал" />
        <div className="ch-connected">
          <IconTile name="megaphone" tone="blue" size={40} />
          <div className="ch-connected-text">
            <b>{state.title}</b>
            {state.username
              ? <button className="ch-link" onClick={() => openTelegramLink(`https://t.me/${state.username}`)}>@{state.username}</button>
              : <span className="muted small">приватный канал</span>}
          </div>
          <button className="btn-mini" disabled={busy} onClick={disconnect} onBlur={() => setConfirmOff(false)}>
            {confirmOff ? 'Точно?' : 'Отключить'}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="card">
      <SectionHead icon="send" tone="blue" title="Подключить канал" />
      <ol className="ch-steps small">
        <li>Создайте канал в Telegram.</li>
        <li>Добавьте бота в администраторы канала с правом «Публикация сообщений».</li>
        <li>Впишите сюда @имя канала или ссылку на него.</li>
      </ol>
      <div className="adm-sales-row" style={{ alignItems: 'flex-end' }}>
        <input className="search-input" placeholder="@clanify_news" value={handle}
          onChange={e => setHandle(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') connect() }} />
        <button className="btn" disabled={busy || !handle.trim()} onClick={connect}>
          {busy ? '…' : <><Icon name="link" size={16} />Подключить</>}
        </button>
      </div>
      {error && <p className="form-error small">{error}</p>}
    </div>
  )
}

function Toggle({ on, disabled, onToggle }: { on: boolean; disabled?: boolean; onToggle: () => void }) {
  return (
    <button className={`notif-switch ${on ? 'notif-switch-on' : ''}`} role="switch" aria-checked={on} disabled={disabled}
      onClick={() => { haptic('light'); onToggle() }}>
      <span className="notif-switch-knob" />
    </button>
  )
}

function ToggleLine({ icon, tone, label, desc, on, disabled, onToggle }: {
  icon: IconName; tone: Tone; label: string; desc: string; on: boolean; disabled?: boolean; onToggle: () => void
}) {
  return (
    <div className="notif-row ch-toggle">
      <div className="ch-toggle-label">
        <IconTile name={icon} tone={tone} size={30} />
        <span className="notif-row-label">{label}</span>
        <InfoButton title={label}>{desc}</InfoButton>
      </div>
      <Toggle on={on} disabled={disabled} onToggle={onToggle} />
    </div>
  )
}

function AutoCard({ state, onState }: Props) {
  const [busy, setBusy] = useState<string | null>(null)
  const [note, setNote] = useState<string | null>(null)

  const flip = async (key: ChannelToggle) => {
    const next = !state.toggles[key]
    onState({ ...state, toggles: { ...state.toggles, [key]: next } })
    try { onState(await api.channelSettings({ [key]: next })) } catch { onState(state) }
  }

  const preview = async (kind: 'daily' | 'weekly') => {
    haptic('light')
    setBusy(kind)
    setNote(null)
    try {
      onState(await api.channelPreview(kind))
      setNote('Готово - пример лежит в черновиках выше')
    } catch (e) {
      setNote(errorText(e))
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="card">
      <SectionHead icon="bot" tone="violet" title="Автопосты"
        info={<p>Бот сам публикует посты из своих данных: они проверены, поэтому уходят в канал без черновика.</p>} />
      <div className="ch-toggles">
        {AUTO.map(a => (
          <ToggleLine key={a.key} icon={a.icon} tone={a.tone} label={a.label} desc={a.desc} on={state.toggles[a.key]} onToggle={() => flip(a.key)} />
        ))}
      </div>
      <div className="ch-actions">
        <button className="btn-mini" disabled={busy !== null} onClick={() => preview('daily')}>
          <Icon name="eye" size={14} />{busy === 'daily' ? '…' : 'Пример: топ дня'}
        </button>
        <button className="btn-mini" disabled={busy !== null} onClick={() => preview('weekly')}>
          <Icon name="eye" size={14} />{busy === 'weekly' ? '…' : 'Пример: неделя лиги'}
        </button>
      </div>
      {note && <p className="muted small">{note}</p>}
    </div>
  )
}

function NewsCard({ state, onState }: Props) {
  const [feeds, setFeeds] = useState(state.feeds.join('\n'))
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)
  const dirty = feeds.trim() !== state.feeds.join('\n').trim()

  const flip = async (key: ChannelToggle) => {
    const next = !state.toggles[key]
    onState({ ...state, toggles: { ...state.toggles, [key]: next } })
    try { onState(await api.channelSettings({ [key]: next })) } catch { onState(state) }
  }

  const saveFeeds = async () => {
    haptic('light')
    setBusy(true)
    try {
      const s = await api.channelSettings({}, feeds.split('\n').map(f => f.trim()).filter(Boolean))
      onState(s)
      setFeeds(s.feeds.join('\n'))
      hapticNotify('success')
    } catch (e) {
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const refresh = async () => {
    haptic('medium')
    setBusy(true)
    setNote(null)
    try {
      const r = await api.channelRefreshNews()
      onState(r.state)
      setNote(r.drafts + r.published === 0
        ? 'Новых новостей нет'
        : `Новых: ${r.drafts} в черновиках${r.published ? `, ${r.published} опубликовано` : ''}`)
    } catch (e) {
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const statusOf = (url: string) => state.feedStatus.find(f => f.url === url)

  return (
    <div className="card">
      <SectionHead icon="globe" tone="orange" title="Новости Clash Royale"
        info={<>
          <p>Бот раз в 30 минут читает ленты и кладёт новое в черновики - публикуете вы.</p>
          <p>Подходит любая RSS или Atom-лента: сайт, YouTube-канал (youtube.com/feeds/videos.xml?channel_id=…), Reddit (reddit.com/r/ClashRoyale/.rss).</p>
          <p>Твиттер напрямую бесплатно читать нельзя - API X платный. Можно вставить сюда RSS-ленту аккаунта из сервисов вроде rss.app.</p>
        </>} />
      <div className="ch-toggles">
        <ToggleLine key="news" icon="globe" tone="orange" label="Собирать новости" on={state.toggles.news}
          desc="Новые записи из лент ниже попадают в черновики." onToggle={() => flip('news')} />
        <ToggleLine key="newsauto" icon="zap" tone="red" label="Публиковать без проверки" on={state.toggles.newsauto}
          disabled={!state.translator}
          desc="Только для новостей, которые бот пересказал по-русски. Без проверки в канал может попасть лишнее - включайте, когда убедитесь, что черновики выходят хорошими."
          onToggle={() => flip('newsauto')} />
      </div>
      <p className={`small ${state.translator ? 'muted' : 'ch-warn'}`}>
        <Icon name={state.translator ? 'checkCircle' : 'language'} size={13} />{' '}
        {state.translator
          ? 'Пересказ по-русски включён'
          : 'Пересказ по-русски выключен: новости придут на английском. Включается ключом ANTHROPIC_API_KEY и ANTHROPIC_MODEL в .env сервера'}
      </p>
      <label className="small muted">Ленты - по одной в строке</label>
      <textarea className="owner-bc-text ch-feeds" rows={3} value={feeds} onChange={e => setFeeds(e.target.value)}
        placeholder="https://www.youtube.com/feeds/videos.xml?channel_id=…" />
      {state.feeds.length > 0 && (
        <ul className="ch-feed-status small">
          {state.feeds.map(f => {
            const st = statusOf(f)
            return (
              <li key={f}>
                <Icon name={!st ? 'clock' : st.ok ? 'checkCircle' : 'xCircle'} size={13}
                  className={!st ? 'muted' : st.ok ? 'ch-ok' : 'ch-bad'} />
                <span className="ch-feed-url">{f.replace(/^https?:\/\/(www\.)?/, '')}</span>
                <span className="muted">{!st ? 'ещё не проверялась' : st.ok ? `${st.items} записей` : st.error}</span>
              </li>
            )
          })}
        </ul>
      )}
      <div className="ch-actions">
        {dirty && <button className="btn-mini" disabled={busy} onClick={saveFeeds}><Icon name="check" size={14} />Сохранить ленты</button>}
        <button className="btn-mini" disabled={busy || dirty} onClick={refresh}>
          <Icon name="refresh" size={14} />{busy ? 'Проверяю…' : 'Проверить сейчас'}
        </button>
      </div>
      {note && <p className="muted small">{note}</p>}
    </div>
  )
}

function DraftsCard({ state, onState }: Props) {
  if (state.drafts.length === 0) return null
  return (
    <div className="card">
      <SectionHead icon="edit" tone="gold" title={`Черновики · ${state.drafts.length}`}
        info={<p>Разметка: **жирный** и [текст](https://ссылка). Правьте прямо здесь и публикуйте.</p>} />
      <div className="ch-drafts">
        {state.drafts.map(p => <DraftItem key={p.id} post={p} onState={onState} />)}
      </div>
    </div>
  )
}

function DraftItem({ post, onState }: { post: ChannelPost; onState: (s: ChannelState) => void }) {
  const [text, setText] = useState(post.text)
  const [busy, setBusy] = useState(false)
  const [confirm, setConfirm] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const publish = async () => {
    haptic('medium')
    if (!confirm) { setConfirm(true); return }
    setBusy(true)
    setError(null)
    try {
      onState(await api.channelPublishDraft(post.id, text !== post.text ? text : undefined))
      hapticNotify('success')
    } catch (e) {
      hapticNotify('error')
      setError(errorText(e))
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  const reject = async () => {
    haptic('light')
    setBusy(true)
    try { onState(await api.channelRejectDraft(post.id)) } catch (e) { setError(errorText(e)) } finally { setBusy(false) }
  }

  return (
    <div className="ch-draft">
      <div className="ch-draft-head">
        <span className="ch-kind">{KIND_LABEL[post.kind] ?? post.kind}</span>
        <span className="muted small">{when(post.createdUtc)}</span>
        {post.state === 'failed' && <span className="ch-bad small"><Icon name="alert" size={12} /> не опубликован</span>}
      </div>
      {post.photoUrl && <img className="ch-draft-photo" src={post.photoUrl} alt="" loading="lazy" />}
      <textarea className="owner-bc-text" rows={Math.min(10, Math.max(4, text.split('\n').length + 1))} value={text}
        maxLength={4000} onChange={e => { setText(e.target.value); setConfirm(false) }} />
      {post.linkUrl && (
        <button className="ch-link small" onClick={() => openExternalLink(post.linkUrl!)}>
          <Icon name="external" size={12} /> Первоисточник
        </button>
      )}
      {(error || post.error) && <p className="form-error small">{error ?? post.error}</p>}
      <div className="ch-actions">
        <button className="btn btn-nudge" disabled={busy || !text.trim()} onClick={publish} onBlur={() => setConfirm(false)}>
          <Icon name={confirm ? 'alert' : 'send'} size={16} />{busy ? '…' : confirm ? 'Точно опубликовать?' : 'Опубликовать'}
        </button>
        <button className="btn btn-ghost" disabled={busy} onClick={reject}><Icon name="trash" size={16} />Убрать</button>
      </div>
    </div>
  )
}

function ComposeCard({ onState }: { onState: (s: ChannelState) => void }) {
  const [text, setText] = useState('')
  const [photo, setPhoto] = useState('')
  const [button, setButton] = useState('app')
  const [buttonText, setButtonText] = useState('Открыть Clanify')
  const [link, setLink] = useState('')
  const [busy, setBusy] = useState(false)
  const [confirm, setConfirm] = useState(false)
  const [result, setResult] = useState<string | null>(null)

  const pickButton = (key: string) => {
    haptic('light')
    setButton(key)
    setConfirm(false)
    const b = BUTTONS.find(x => x.key === key)!
    if (key !== 'none' && key !== 'link') setButtonText(key === 'app' ? 'Открыть Clanify' : b.label)
  }

  const send = async () => {
    haptic('medium')
    if (!confirm) { setConfirm(true); return }
    setBusy(true)
    setResult(null)
    try {
      const b = BUTTONS.find(x => x.key === button)!
      onState(await api.channelPost({
        text: text.trim(),
        photoUrl: photo.trim() || undefined,
        buttonText: button === 'none' ? undefined : buttonText.trim() || undefined,
        buttonUrl: button === 'none' ? undefined : button === 'link' ? link.trim() : b.url,
      }))
      hapticNotify('success')
      setResult('Опубликовано')
      setText('')
      setPhoto('')
    } catch (e) {
      hapticNotify('error')
      setResult(errorText(e))
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  return (
    <div className="card">
      <SectionHead icon="megaphone" tone="orange" title="Новый пост"
        info={<p>Разметка: **жирный** и [текст](https://ссылка). Картинка - прямая ссылка на изображение.</p>} />
      <textarea className="owner-bc-text" rows={5} maxLength={4000} value={text}
        placeholder="Текст поста. **Жирный** выделяется звёздочками"
        onChange={e => { setText(e.target.value); setConfirm(false) }} />
      <input className="search-input ch-input" placeholder="Картинка: https://…/image.jpg (необязательно)" value={photo}
        onChange={e => { setPhoto(e.target.value); setConfirm(false) }} />
      <div className="owner-bc-targets ch-buttons">
        {BUTTONS.map(b => (
          <button key={b.key} className={`btn-mini ${button === b.key ? 'owner-bc-target-on' : ''}`} onClick={() => pickButton(b.key)}>
            {b.label}
          </button>
        ))}
      </div>
      {button !== 'none' && (
        <input className="search-input ch-input" placeholder="Надпись на кнопке" maxLength={40} value={buttonText}
          onChange={e => setButtonText(e.target.value)} />
      )}
      {button === 'link' && (
        <input className="search-input ch-input" placeholder="https://…" value={link} onChange={e => setLink(e.target.value)} />
      )}
      <button className="btn btn-nudge" disabled={busy || !text.trim()} onClick={send} onBlur={() => setConfirm(false)}>
        <Icon name={confirm ? 'alert' : 'send'} size={16} />{busy ? '…' : confirm ? 'Точно опубликовать?' : 'Опубликовать в канал'}
      </button>
      {result && <p className="muted small owner-bc-result">{result}</p>}
    </div>
  )
}

function RecentCard({ posts }: { posts: ChannelPost[] }) {
  if (posts.length === 0) return null
  return (
    <div className="card">
      <SectionHead icon="history" tone="gray" title="Опубликовано" />
      <ul className="ch-recent">
        {posts.map(p => (
          <li key={p.id}>
            <span className="ch-kind">{KIND_LABEL[p.kind] ?? p.kind}</span>
            <span className="ch-recent-title">{p.title ?? p.text.slice(0, 60)}</span>
            <span className="muted small">{when(p.publishedUtc)}</span>
            {p.postUrl && (
              <button className="btn-mini" aria-label="Открыть пост" onClick={() => openTelegramLink(p.postUrl!)}>
                <Icon name="external" size={13} />
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
