import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ChannelFeedStatus, ChannelPost, ChannelState, ChannelToggle } from '../types'
import { copyText, haptic, hapticNotify, openTelegramLink, openExternalLink } from '../lib/telegram'
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

/** Один и тот же источник, записанный по-разному: с www, .rss или слешем на конце. */
function sameFeed(a: string, b: string): boolean {
  const norm = (u: string) => u.trim().toLowerCase()
    .replace(/^https?:\/\/(www\.|old\.)?/, '').replace(/\.rss$/, '').replace(/\/+$/, '')
  return norm(a) === norm(b)
}

/** Проверенные источники: их можно добавить в ленты одной кнопкой. */
const SUGGESTED_FEEDS: { label: string; url: string }[] = [
  { label: 'Блог Supercell', url: 'https://supercell.com/en/games/clashroyale/blog/' },
  { label: 'YouTube Clash Royale', url: 'https://www.youtube.com/feeds/videos.xml?channel_id=UC_F8DoJf9MZogEOU51TpTbQ' },
  { label: 'Reddit r/ClashRoyale', url: 'https://www.reddit.com/r/ClashRoyale' },
]

const KIND_LABEL: Record<string, string> = {
  manual: 'Свой пост', ai: 'Нейро-пост', news: 'Новость', daily: 'Топ дня', weekly: 'Неделя лиги',
  chstart: 'Старт челленджа', chend: 'Итоги челленджа', card: 'Новая карта', links: 'Связи карт',
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
    case 'no_ai': return 'Нейросеть не подключена: добавьте DEEPSEEK_API_KEY в .env сервера'
    case 'ai_failed': return e.message && e.message !== 'ai_failed' ? e.message : 'Нейросеть не ответила, попробуйте ещё раз'
    case 'caption_too_long': return 'У поста с картинкой текст не длиннее 1024 символов - сократите'
    default: return 'Не получилось, попробуйте ещё раз'
  }
}

function when(iso: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  return d.toLocaleString('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
}

/** Текст для копирования: без звёздочек разметки, ссылки - адресом в скобках. */
function toPlain(text: string): string {
  return text
    .replace(/\[([^\]\n]+)\]\((https?:\/\/[^\s)]+)\)/g, '$1 ($2)')
    .replace(/\*\*([^*\n]+)\*\*/g, '$1')
}

function CopyButton({ text }: { text: string }) {
  const [done, setDone] = useState(false)
  return (
    <button className="btn btn-ghost" onClick={async () => {
      haptic('light')
      if (await copyText(toPlain(text))) {
        hapticNotify('success')
        setDone(true)
        setTimeout(() => setDone(false), 1800)
      }
    }}>
      <Icon name={done ? 'check' : 'copy'} size={16} />{done ? 'Скопировано' : 'Копировать'}
    </button>
  )
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
    <div className="ch-root">
      <ConnectCard state={state} onState={setState} />
      {state.id !== null && (
        <>
          <AiComposeCard state={state} onState={setState} />
          <ComposeCard onState={setState} />
          <DraftsCard state={state} onState={setState} />
          <AutoCard state={state} onState={setState} />
          <NewsCard state={state} onState={setState} />
          <RecentCard posts={state.recent} onState={setState} />
        </>
      )}
    </div>
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

  const preview = async (kind: 'daily' | 'weekly' | 'links') => {
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
        <button className="btn-mini" disabled={busy !== null} onClick={() => preview('links')}>
          <Icon name="link" size={14} />{busy === 'links' ? '…' : 'Пост: связи карт (картинка)'}
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
  const hasReddit = feeds.includes('reddit.com/r/')

  return (
    <div className="card">
      <SectionHead icon="globe" tone="orange" title="Новости Clash Royale"
        info={<>
          <p>Бот раз в 30 минут проверяет источники, открывает каждую новую статью целиком и собирает из неё пост: заголовок, суть, список изменений. Пост ждёт вас в черновиках.</p>
          <p>Можно добавить и обычную страницу блога без RSS - бот будет следить за новыми статьями на ней.</p>
          <p>Подходит любая RSS или Atom-лента: сайт, YouTube-канал (youtube.com/feeds/videos.xml?channel_id=…), Reddit (просто reddit.com/r/ClashRoyale - бот сам возьмёт топ дня).</p>
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
      <AiStatusBlock state={state} onState={onState} />
      <div className="ch-toggles">
        <ToggleLine icon="message" tone="green" label="Живые итоги дня в чатах кланов" on={state.toggles.recapai}
          disabled={!state.ai.enabled}
          desc="«Итоги дня» в 21:00 пишет нейросеть - по тем же цифрам, но каждый вечер по-новому и на языке клана. Не ответила - уйдёт обычный шаблон. По умолчанию выключено."
          onToggle={() => flip('recapai')} />
      </div>
      <label className="small muted">Ленты - по одной в строке</label>
      <div className="owner-bc-targets ch-suggest">
        {SUGGESTED_FEEDS.filter(f => !feeds.split('\n').some(x => sameFeed(x, f.url))).map(f => (
          <button key={f.url} className="btn-mini" onClick={() => { haptic('light'); setFeeds(v => (v.trim() ? v.trim() + '\n' : '') + f.url) }}>
            <Icon name="plus" size={13} />{f.label}
          </button>
        ))}
      </div>
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
                <span className="muted">{!st ? 'ещё не проверялась' : st.ok ? feedLine(st) : st.error}</span>
                {st?.ok && st.note && <span className="muted ch-feed-note">{st.note}</span>}
              </li>
            )
          })}
        </ul>
      )}
      {hasReddit && <RedditLimits state={state} onState={onState} />}
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

/**
 * Отбор с Reddit: из топа дня берутся только посты с заметным числом апвоутов, и не
 * больше N в день; дальше нейросеть решает, интересен ли пост.
 */
function RedditLimits({ state, onState }: Props) {
  const [min, setMin] = useState(String(state.redditMinScore))
  const [perDay, setPerDay] = useState(String(state.redditPerDay))
  const [busy, setBusy] = useState(false)
  const dirty = min !== String(state.redditMinScore) || perDay !== String(state.redditPerDay)

  const save = async () => {
    haptic('light')
    setBusy(true)
    try {
      const s = await api.channelSettings({}, undefined, {
        redditMinScore: Math.max(0, parseInt(min, 10) || 0),
        redditPerDay: Math.max(0, parseInt(perDay, 10) || 0),
      })
      onState(s)
      setMin(String(s.redditMinScore))
      setPerDay(String(s.redditPerDay))
      hapticNotify('success')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="ch-reddit">
      <div className="ch-reddit-head">
        <b className="small">Отбор с Reddit</b>
        <InfoButton title="Отбор с Reddit">
          <p>Бот смотрит топ сабреддита за сутки и берёт только посты, набравшие не меньше указанных апвоутов, - самые заметные первыми.</p>
          <p>Дальше нейросеть оставляет только интересное: находки, стратегии, баги, лики, смешные мемы с картинкой. Вопросы, жалобы и шум отсеиваются.</p>
        </InfoButton>
      </div>
      <div className="ch-reddit-row">
        <label className="small muted">Мин. апвоутов
          <input className="search-input" inputMode="numeric" value={min} onChange={e => setMin(e.target.value.replace(/\D/g, ''))} />
        </label>
        <label className="small muted">Постов в день
          <input className="search-input" inputMode="numeric" value={perDay} onChange={e => setPerDay(e.target.value.replace(/\D/g, ''))} />
        </label>
      </div>
      {!state.translator && <p className="small ch-warn">Без нейросети отбор только по апвоутам - мемы и вопросы тоже могут пройти.</p>}
      {dirty && <button className="btn-mini" disabled={busy} onClick={save}><Icon name="check" size={14} />Сохранить отбор</button>}
    </div>
  )
}

/** Нейросеть для пересказа: какая подключена, работает ли, и кнопка проверки. */
function AiStatusBlock({ state, onState }: Props) {
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)
  const ai = state.ai

  const check = async () => {
    haptic('light')
    setBusy(true)
    try {
      const s = await api.channelCheckAi()
      onState(s)
      hapticNotify(s.ai.ok ? 'success' : 'error')
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  if (!ai.enabled) {
    return (
      <div className="ch-ai ch-ai-off">
        <span className="ch-ai-dot" />
        <div className="ch-ai-text">
          <b>Нейросеть не подключена</b>
          <span className="small muted">Новости придут на английском. Добавьте строку DEEPSEEK_API_KEY в .env сервера и задеплойте.</span>
        </div>
      </div>
    )
  }

  const runAnalyst = async () => {
    haptic('medium')
    setBusy(true)
    setNote(null)
    try {
      onState(await api.channelRunAnalyst())
      hapticNotify('success')
      setNote('Мета разобрана - смотри «Выбор аналитика» в билдере колод')
    } catch (e) {
      hapticNotify('error')
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const tone = ai.ok === true ? 'ch-ai-ok' : ai.ok === false ? 'ch-ai-bad' : 'ch-ai-wait'
  const label = ai.ok === true ? 'Работает' : ai.ok === false ? 'Ошибка' : 'Ещё не проверялась'
  return (
    <>
    <div className={`ch-ai ${tone}`}>
      <span className="ch-ai-dot" />
      <div className="ch-ai-text">
        <b>{ai.provider} · {label}</b>
        {ai.ok === false && ai.error && <span className="small">{ai.error}</span>}
        {ai.atUtc && <span className="small muted">{ai.ok ? 'Последний ответ' : 'Проверено'}: {when(ai.atUtc)}</span>}
      </div>
      <button className="btn-mini" disabled={busy} onClick={check}>
        <Icon name="refresh" size={14} />{busy ? '…' : 'Проверить'}
      </button>
    </div>
    <div className="ch-analyst small">
      <span className="muted">
        Разбор меты для билдера колод: {state.analyst ? `${state.analyst.day}, отмечено колод: ${state.analyst.picks}` : 'ещё не было'} (сам - каждое утро)
      </span>
      <button className="btn-mini" disabled={busy} onClick={runAnalyst}>
        <Icon name="sparkles" size={14} />{busy ? '…' : 'Разобрать сейчас'}
      </button>
      {note && <span className="muted">{note}</span>}
    </div>
    </>
  )
}

/** «10 записей · новых 2 · отсеяно нейросетью 5» - видно, где теряются записи. */
function feedLine(st: ChannelFeedStatus): string {
  const parts = [`${st.items} записей`]
  if (st.new !== undefined) parts.push(`новых ${st.new}`)
  if (st.rejected) parts.push(`отсеяно нейросетью ${st.rejected}`)
  if (st.skipped) parts.push(`мало апвоутов ${st.skipped}`)
  return parts.join(' · ')
}

function DraftsCard({ state, onState }: Props) {
  if (state.drafts.length === 0) return null
  return (
    <div className="card">
      <SectionHead icon="edit" tone="gold" title={`Черновики · ${state.drafts.length}`}
        info={<p>Разметка: **жирный** и [текст](https://ссылка). Правьте прямо здесь и публикуйте.</p>} />
      <div className="ch-drafts">
        {state.drafts.map(p => <DraftItem key={p.id} post={p} aiEnabled={state.ai.enabled} onState={onState} />)}
      </div>
    </div>
  )
}

function DraftItem({ post, aiEnabled, onState }: { post: ChannelPost; aiEnabled: boolean; onState: (s: ChannelState) => void }) {
  const [aiOpen, setAiOpen] = useState(false)
  const [instruction, setInstruction] = useState('')
  const [text, setText] = useState(post.text)
  const [photo, setPhoto] = useState(post.photoUrl ?? '')
  const [photoOpen, setPhotoOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [confirm, setConfirm] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const dirty = text !== post.text || photo.trim() !== (post.photoUrl ?? '')

  // Текст черновика переписала нейросеть - показываем новый, а не старый из поля
  useEffect(() => { setText(post.text) }, [post.text])

  const rewrite = () => {
    if (!instruction.trim()) return
    haptic('medium')
    run(() => api.channelAiRewrite(post.id, instruction.trim(), text), () => { setInstruction(''); setAiOpen(false) })
  }

  const run = async (action: () => Promise<ChannelState>, ok?: () => void) => {
    setBusy(true)
    setError(null)
    try {
      onState(await action())
      hapticNotify('success')
      ok?.()
    } catch (e) {
      hapticNotify('error')
      setError(errorText(e))
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  const publish = () => {
    haptic('medium')
    if (!confirm) { setConfirm(true); return }
    run(() => api.channelPublishDraft(post.id,
      text !== post.text ? text : undefined,
      photo.trim() !== (post.photoUrl ?? '') ? photo.trim() : undefined))
  }
  const save = () => {
    haptic('light')
    run(() => api.channelSaveDraft(post.id, text, photo.trim()), () => { setSaved(true); setTimeout(() => setSaved(false), 1800) })
  }
  const reject = () => { haptic('light'); run(() => api.channelRejectDraft(post.id)) }

  return (
    <div className="ch-draft">
      <div className="ch-draft-head">
        <span className="ch-kind">{KIND_LABEL[post.kind] ?? post.kind}</span>
        <span className="muted small">{when(post.createdUtc)}</span>
        {post.state === 'failed' && <span className="ch-bad small"><Icon name="alert" size={12} /> не опубликован</span>}
      </div>
      {photo.trim() && <img className="ch-draft-photo" src={photo.trim()} alt="" loading="lazy" />}
      <textarea className="owner-bc-text" rows={Math.min(14, Math.max(4, text.split('\n').length + 1))} value={text}
        maxLength={4000} onChange={e => { setText(e.target.value); setConfirm(false) }} />
      {photoOpen
        ? <input className="search-input ch-input" placeholder="Ссылка на картинку (пусто - без картинки)" value={photo}
            onChange={e => { setPhoto(e.target.value); setConfirm(false) }} />
        : <button className="ch-link small" onClick={() => setPhotoOpen(true)}>
            <Icon name="image" size={12} /> {photo.trim() ? 'Заменить картинку' : 'Добавить картинку'}
          </button>}
      {post.linkUrl && (
        <button className="ch-link small" onClick={() => openExternalLink(post.linkUrl!)}>
          <Icon name="external" size={12} /> Первоисточник
        </button>
      )}
      {aiEnabled && (aiOpen ? (
        <div className="ch-ai-rewrite">
          <input className="search-input" placeholder="Как переписать? Например: короче и смешнее" value={instruction}
            maxLength={500} onChange={e => setInstruction(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') rewrite() }} />
          <div className="owner-bc-targets ch-suggest">
            {['Короче', 'Смешнее', 'Серьёзнее', 'Добавь призыв зайти в бота', 'Больше эмодзи'].map(h => (
              <button key={h} className="btn-mini" onClick={() => setInstruction(h)}>{h}</button>
            ))}
          </div>
          <button className="btn btn-ghost" disabled={busy || !instruction.trim()} onClick={rewrite}>
            <Icon name="sparkles" size={16} />{busy ? 'Переписываю… до 40 сек' : 'Переписать'}
          </button>
        </div>
      ) : (
        <button className="ch-link small" onClick={() => setAiOpen(true)}>
          <Icon name="sparkles" size={12} /> Переписать с ИИ
        </button>
      ))}
      {(error || post.error) && <p className="form-error small">{error ?? post.error}</p>}
      <div className="ch-actions">
        <button className="btn btn-nudge" disabled={busy || !text.trim()} onClick={publish} onBlur={() => setConfirm(false)}>
          <Icon name={confirm ? 'alert' : 'send'} size={16} />{busy ? '…' : confirm ? 'Точно опубликовать?' : 'Опубликовать'}
        </button>
      </div>
      <div className="ch-actions ch-actions-row">
        {dirty || saved
          ? <button className="btn btn-ghost" disabled={busy || !text.trim() || saved} onClick={save}>
              <Icon name="check" size={16} />{saved ? 'Сохранено' : 'Сохранить'}
            </button>
          : null}
        <CopyButton text={text} />
        <button className="btn btn-ghost" disabled={busy} onClick={reject}><Icon name="trash" size={16} />Убрать</button>
      </div>
    </div>
  )
}

/** Быстрые просьбы: нажал - текст в поле, можно дописать своё. */
const AI_PRESETS: { label: string; text: string }[] = [
  { label: 'Итоги дня', text: 'Пост с итогами дня: что в мировом топе, кто взлетел, что происходит в лиге 1×1.' },
  { label: 'Лига 1×1', text: 'Пост про лигу дуэлей 1×1: кто в топе и с каким рангом, кто рванул за неделю. В конце позови всех бросать вызовы друзьям.' },
  { label: 'Мета', text: 'Разбор меты топ-1000: какие карты на подъёме, какие падают, и что это значит для обычного игрока.' },
  { label: 'Челлендж', text: 'Пост про текущий челлендж: правила, приз, кто лидирует. Призыв вступить.' },
  { label: 'Реклама бота', text: 'Пост, который продаёт бота новым игрокам: что он умеет и зачем добавить его в свой клан. Опирайся на цифры бота.' },
  { label: 'Вопрос подписчикам', text: 'Короткий вовлекающий пост-вопрос к подписчикам про текущую мету, чтобы хотелось ответить в комментариях.' },
]

function AiComposeCard({ state, onState }: Props) {
  const [request, setRequest] = useState('')
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)

  const generate = async () => {
    if (!request.trim()) return
    haptic('medium')
    setBusy(true)
    setNote(null)
    try {
      onState(await api.channelAiGenerate(request.trim()))
      hapticNotify('success')
      setNote('Готово - пост в черновиках ниже: поправьте и опубликуйте')
      setRequest('')
    } catch (e) {
      hapticNotify('error')
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card">
      <SectionHead icon="sparkles" tone="violet" title="Нейро-пост"
        info={<>
          <p>Напишите, какой пост нужен. Бот соберёт свежие данные - мировой топ и мету, лигу 1×1, челлендж, статистику бота, последние новости игры - и нейросеть напишет пост только по ним, без выдумок.</p>
          <p>Пост ляжет в черновики: в канал он уйдёт, только когда вы нажмёте «Опубликовать».</p>
        </>} />
      {!state.ai.enabled && <p className="small ch-warn">Нейросеть не подключена: добавьте DEEPSEEK_API_KEY в .env сервера и задеплойте.</p>}
      <div className="owner-bc-targets ch-suggest">
        {AI_PRESETS.map(p => (
          <button key={p.label} className="btn-mini" disabled={!state.ai.enabled}
            onClick={() => { haptic('light'); setRequest(p.text) }}>{p.label}</button>
        ))}
      </div>
      <textarea className="owner-bc-text" rows={3} maxLength={1500} value={request} disabled={!state.ai.enabled}
        placeholder="Например: итоги недели в лиге 1×1 с юмором, упомяни тех, кто вырос сильнее всех"
        onChange={e => setRequest(e.target.value)} />
      <button className="btn btn-nudge" disabled={busy || !request.trim() || !state.ai.enabled} onClick={generate}>
        <Icon name="sparkles" size={16} />{busy ? 'Пишу пост… до 40 сек' : 'Сгенерировать'}
      </button>
      {note && <p className="muted small owner-bc-result">{note}</p>}
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

function RecentCard({ posts, onState }: { posts: ChannelPost[]; onState: (s: ChannelState) => void }) {
  const [open, setOpen] = useState<number | null>(null)
  if (posts.length === 0) return null
  return (
    <div className="card">
      <SectionHead icon="history" tone="gray" title="Опубликовано"
        info={<p>Нажмите на пост, чтобы скопировать текст или исправить его прямо в канале. Картинку и кнопку у опубликованного поста Telegram менять не даёт.</p>} />
      <ul className="ch-recent">
        {posts.map(p => (
          <li key={p.id} className={open === p.id ? 'ch-recent-open' : ''}>
            <button className="ch-recent-row" onClick={() => { haptic('light'); setOpen(o => o === p.id ? null : p.id) }}>
              <span className="ch-kind">{KIND_LABEL[p.kind] ?? p.kind}</span>
              <span className="ch-recent-title">{p.title ?? p.text.slice(0, 60)}</span>
              <span className="muted small">{when(p.publishedUtc)}</span>
              <Icon name={open === p.id ? 'chevronUp' : 'chevronDown'} size={14} />
            </button>
            {open === p.id && <PublishedEditor post={p} onState={onState} />}
          </li>
        ))}
      </ul>
    </div>
  )
}

function PublishedEditor({ post, onState }: { post: ChannelPost; onState: (s: ChannelState) => void }) {
  const [text, setText] = useState(post.text)
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)
  const dirty = text.trim() !== post.text.trim()

  const save = async () => {
    haptic('medium')
    setBusy(true)
    setNote(null)
    try {
      onState(await api.channelEditPost(post.id, text))
      hapticNotify('success')
      setNote('Исправлено в канале')
    } catch (e) {
      hapticNotify('error')
      setNote(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="ch-draft ch-published">
      {post.photoUrl && <img className="ch-draft-photo" src={post.photoUrl} alt="" loading="lazy" />}
      <textarea className="owner-bc-text" rows={Math.min(14, Math.max(4, text.split('\n').length + 1))} value={text}
        maxLength={4000} onChange={e => { setText(e.target.value); setNote(null) }} />
      {note && <p className="muted small">{note}</p>}
      <div className="ch-actions ch-actions-row">
        {dirty && (
          <button className="btn btn-nudge" disabled={busy || !text.trim()} onClick={save}>
            <Icon name="edit" size={16} />{busy ? '…' : 'Сохранить в канале'}
          </button>
        )}
        <CopyButton text={text} />
        {post.postUrl && (
          <button className="btn btn-ghost" onClick={() => openTelegramLink(post.postUrl!)}>
            <Icon name="external" size={16} />Открыть
          </button>
        )}
      </div>
    </div>
  )
}
