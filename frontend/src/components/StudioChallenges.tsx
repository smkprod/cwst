import { useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ChallengeStatus, Studio, StudioChallenge } from '../types'
import { botAppLink, copyText, haptic, hapticNotify, shareToTelegram } from '../lib/telegram'
import { useBotUsername } from '../lib/botUsername'
import type { Translations } from '../lib/i18n'
import { CHALLENGE_WIDGET } from '../lib/overlayLinks'
import { toLocalInput } from './TournamentForm'
import { WidgetLinks } from './WidgetLinks'
import { Icon, type IconName } from './ui/Icon'
import { Chip, SectionHead, type Tone } from './ui/Section'
import { InfoButton } from './ui/Info'

/** Сервер не примет челлендж длиннее — проверяем заранее, чтобы не гонять форму туда-обратно. */
const MAX_LENGTH_MS = 14 * 24 * 3600_000
/** Длина по умолчанию — обычный стрим: три часа. */
const DEFAULT_LENGTH_MS = 3 * 3600_000

type FormState = { edit: StudioChallenge | null } | null

/** Форматы челленджа (ChallengeRules на сервере) и их иконки. Порядок — как в выборе. */
export const CHALLENGE_RULES: { key: string; icon: IconName }[] = [
  { key: 'tickets', icon: 'ticket' },
  { key: 'threecrowns', icon: 'crown' },
  { key: 'flawless', icon: 'shieldCheck' },
  { key: 'streak', icon: 'flame' },
  { key: 'heavy', icon: 'droplet' },
]

/**
 * «Мои челленджи»: челлендж блогера для своих зрителей.
 *
 * Механика та же, что у уикенд-челленджа, — билеты за победы, — но таблица своя:
 * в ней только те, кто вступил по ссылке блогера. Поэтому главное в карточке —
 * сама ссылка и виджет для OBS, а не настройки.
 */
export function StudioChallenges({ items, onChanged, t }: {
  items: StudioChallenge[]
  onChanged: (s: Studio) => void
  t: Translations
}) {
  const [form, setForm] = useState<FormState>(null)
  const formRef = useRef<HTMLDivElement>(null)

  // Идущие сверху: про них спрашивают во время эфира. Завершённые — вниз, свежие первыми.
  const sorted = useMemo(() => {
    const rank: Record<ChallengeStatus, number> = { live: 0, upcoming: 1, ended: 2 }
    return [...items].sort((a, b) => rank[a.status] - rank[b.status]
      || (a.status === 'ended' ? Date.parse(b.endUtc) - Date.parse(a.endUtc) : Date.parse(a.startUtc) - Date.parse(b.startUtc)))
  }, [items])

  const openForm = (edit: StudioChallenge | null) => {
    haptic('light')
    setForm({ edit })
    // Правка открывается над списком — уводим экран к форме, иначе тап «Изменить»
    // внизу длинного списка выглядит как кнопка, которая ничего не делает
    window.setTimeout(() => formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 0)
  }

  return (
    <section className="card st-card">
      <SectionHead icon="ticket" tone="green" title={t.studio.chTitle} aside={items.length || undefined}
        info={<ul className="stc-rules">{t.studio.chRules.map((r, i) => <li key={i}>{r}</li>)}</ul>} />

      <div ref={formRef}>
        {form ? (
          <ChallengeForm key={form.edit?.code ?? 'new'} edit={form.edit} t={t}
            onSaved={s => { setForm(null); onChanged(s) }}
            onCancel={() => { haptic('light'); setForm(null) }} />
        ) : (
          <button className="ui-btn ui-btn-primary stc-create" onClick={() => openForm(null)}>
            <Icon name="plus" size={17} /> {t.studio.chCreate}
          </button>
        )}
      </div>

      {items.length === 0 && !form && <p className="muted small st-empty stc-empty">{t.studio.chEmpty}</p>}

      {sorted.length > 0 && (
        <div className="stc-list">
          {sorted.map(c => (
            <ChallengeItem key={c.code} item={c} t={t} editing={form?.edit?.code === c.code}
              onEdit={() => openForm(c)} onChanged={onChanged} />
          ))}
        </div>
      )}
    </section>
  )
}

const STATUS_LOOK: Record<ChallengeStatus, { tone: Tone; icon: IconName }> = {
  upcoming: { tone: 'blue', icon: 'clock' },
  live: { tone: 'red', icon: 'dot' },
  ended: { tone: 'gray', icon: 'flag' },
}

function ChallengeItem({ item, editing, onEdit, onChanged, t }: {
  item: StudioChallenge
  editing: boolean
  onEdit: () => void
  onChanged: (s: Studio) => void
  t: Translations
}) {
  const s = t.studio
  const ended = item.status === 'ended'
  const look = STATUS_LOOK[item.status]
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const remove = async () => {
    // Вместе с челленджем пропадает таблица зрителей — поэтому второй тап
    if (!confirm) { haptic('medium'); setConfirm(true); return }
    haptic('medium')
    setBusy(true)
    try {
      onChanged(await api.deleteStudioChallenge(item.code))
      hapticNotify('success')
    } catch (e) {
      hapticNotify('error')
      // Уже удалён (с другого устройства) — просто перечитываем список
      if (e instanceof ApiError && e.code === 'not_found') api.getStudio().then(onChanged).catch(() => { /* останемся как есть */ })
      else setError(errorText(e, t))
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  return (
    <div className={`stc-item ${ended ? 'stc-item-off' : ''} ${editing ? 'stc-item-editing' : ''}`}>
      <div className="stc-top">
        <span className="st-trow-name">{item.title}</span>
        <span className="st-trow-chips">
          <Chip icon={look.icon} tone={look.tone}>{s.chStatus[item.status]}</Chip>
          <Chip icon="user" tone="gray">{s.chParticipants.replace('{n}', String(item.participants))}</Chip>
          <Chip icon={CHALLENGE_RULES.find(r => r.key === item.rule)?.icon ?? 'ticket'} tone="violet">{t.ch.ruleNames[item.rule] ?? item.rule}</Chip>
          {item.prize && <Chip icon="gift" tone="gold">{item.prize}</Chip>}
        </span>
        <span className="muted small stc-period"><Icon name="calendar" size={13} /> {fmtPeriod(item.startUtc, item.endUtc, t)}</span>
      </div>

      {/* Звать зрителей в закончившийся челлендж незачем; виджет же остаётся —
          итоговую таблицу показывают в эфире и после финиша */}
      {!ended && <ViewerLink item={item} t={t} />}
      <WidgetLinks widgets={[CHALLENGE_WIDGET]} overlayKey={item.code} param="c" compact />

      {error && <p className="form-error small">{error}</p>}

      <div className="stc-actions">
        {!ended && !confirm && (
          <button className="btn-mini" disabled={busy} onClick={onEdit}><Icon name="edit" size={14} /> {s.chEdit}</button>
        )}
        <button className={`btn-mini ${confirm ? 'btn-mini-danger' : ''}`} disabled={busy} onClick={remove}>
          <Icon name={confirm ? 'alert' : 'trash'} size={14} /> {confirm ? s.chDeleteConfirm : s.chDelete}
        </button>
        {confirm && <button className="btn-mini" disabled={busy} onClick={() => setConfirm(false)}>{t.tournament.cancelForm}</button>}
      </div>
    </div>
  )
}

/** Ссылка для зрителей: открывает мини-приложение сразу на этом челлендже. */
function ViewerLink({ item, t }: { item: StudioChallenge; t: Translations }) {
  const s = t.studio
  // Хук, а не getBotUsername: юзернейм может приехать с сервера уже после отрисовки
  const bot = useBotUsername()
  const link = bot ? botAppLink(`ch_${item.code}`) : ''
  const [copied, setCopied] = useState<'ok' | 'fail' | null>(null)
  const timer = useRef<number | undefined>(undefined)
  useEffect(() => () => window.clearTimeout(timer.current), [])

  const copy = async () => {
    haptic('light')
    const ok = await copyText(link)
    hapticNotify(ok ? 'success' : 'error')
    setCopied(ok ? 'ok' : 'fail')
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => setCopied(null), 1800)
  }

  const share = () => {
    haptic('medium')
    const text = s.chShareText.replace('{title}', item.title)
      + (item.prize ? ` ${s.chSharePrize.replace('{prize}', item.prize)}` : '')
    shareToTelegram(text, link)
  }

  return (
    <div className="stc-link">
      <div className="stc-link-head">
        <Icon name="link" size={14} /> {s.chLink}
        <InfoButton size={18} title={s.chLink}>{s.chLinkHint}</InfoButton>
      </div>
      <code className="stc-link-val">{link ? link.replace(/^https:\/\//, '') : s.chNoBot}</code>
      <div className="st-widget-actions">
        <button className={`ui-btn st-copy ${copied === 'ok' ? 'st-copy-ok' : ''}`} disabled={!link} onClick={copy}>
          <Icon name={copied === 'ok' ? 'check' : 'copy'} size={15} />
          {copied === 'ok' ? s.copied : copied === 'fail' ? s.copyFailed : s.copy}
        </button>
        <button className="ui-btn st-open" disabled={!link} onClick={share}>
          <Icon name="send" size={15} /> {s.chShare}
        </button>
      </div>
    </div>
  )
}

/** Начало по умолчанию — ближайший целый час: «в 20:00» блогер объявит, «в 19:37» — нет. */
function nextHour(): Date {
  const d = new Date()
  d.setMinutes(0, 0, 0)
  d.setHours(d.getHours() + 1)
  return d
}

function ChallengeForm({ edit, onSaved, onCancel, t }: {
  edit: StudioChallenge | null
  onSaved: (s: Studio) => void
  onCancel: () => void
  t: Translations
}) {
  const s = t.studio
  const [title, setTitle] = useState(edit?.title ?? '')
  const [prize, setPrize] = useState(edit?.prize ?? '')
  const [rule, setRule] = useState(edit?.rule ?? 'tickets')
  // Посреди челленджа смена правил переписала бы таблицу — сервер её не примет
  const ruleLocked = edit !== null && edit.status !== 'upcoming'
  const [start, setStart] = useState(() => toLocalInput(edit?.startUtc ?? nextHour().toISOString()))
  const [end, setEnd] = useState(() => toLocalInput(edit?.endUtc ?? new Date(nextHour().getTime() + DEFAULT_LENGTH_MS).toISOString()))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // datetime-local — местное время без зоны; new Date() его так и читает,
  // а toISOString переводит в UTC, которое ждёт сервер
  const startMs = new Date(start).getTime()
  const endMs = new Date(end).getTime()
  const length = endMs - startMs
  const datesOk = Number.isFinite(length) && length > 0 && length <= MAX_LENGTH_MS

  // Сдвинули начало — конец едет следом, сохраняя длину: иначе, перенеся стрим на
  // завтра, легко получить конец раньше начала и непонятную ошибку
  const moveStart = (value: string) => {
    const next = new Date(value).getTime()
    if (Number.isFinite(next) && Number.isFinite(length) && length > 0) {
      setEnd(toLocalInput(new Date(next + length).toISOString()))
    }
    setStart(value)
    setError(null)
  }

  const submit = async () => {
    if (!title.trim()) { setError(s.chErr.bad_title); return }
    if (!datesOk) { setError(s.chErr.bad_dates); return }
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      const body = {
        title: title.trim(),
        prize: prize.trim() || null,
        startUtc: new Date(startMs).toISOString(),
        endUtc: new Date(endMs).toISOString(),
        rule,
      }
      const fresh = edit ? await api.updateStudioChallenge(edit.code, body) : await api.createStudioChallenge(body)
      hapticNotify('success')
      onSaved(fresh)
    } catch (e) {
      hapticNotify('error')
      setError(errorText(e, t))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="stc-form">
      <div className="stc-form-title"><Icon name={edit ? 'edit' : 'plus'} size={15} /> {edit ? s.chFormEdit : s.chFormCreate}</div>

      <div className="form-field">
        <label className="muted small">{s.chNameLabel}</label>
        <input className="search-input" value={title} maxLength={60} placeholder={s.chNamePh}
          onChange={e => { setTitle(e.target.value); setError(null) }} />
      </div>

      <div className="form-field">
        <label className="muted small">{s.chPrizeLabel}</label>
        <input className="search-input" value={prize} maxLength={60} placeholder={s.chPrizePh}
          onChange={e => setPrize(e.target.value)} />
      </div>

      <div className="form-field">
        <label className="muted small">{s.chRuleLabel}</label>
        <div className="stc-rules">
          {CHALLENGE_RULES.map(r => (
            <button key={r.key} type="button" disabled={ruleLocked}
              className={`stc-rule ${rule === r.key ? 'stc-rule-on' : ''}`}
              onClick={() => { haptic('light'); setRule(r.key) }}>
              <Icon name={r.icon} size={16} />
              <span>{t.ch.ruleNames[r.key]}</span>
            </button>
          ))}
        </div>
        <p className="muted small stc-rule-desc">{ruleLocked ? s.chRuleLocked : t.ch.ruleDescs[rule]}</p>
      </div>

      <div className="form-field">
        <label className="muted small">{s.chStartLabel}</label>
        <input className="search-input" type="datetime-local" value={start} onChange={e => moveStart(e.target.value)} />
      </div>

      <div className="form-field">
        <label className="muted small">{s.chEndLabel}</label>
        <input className="search-input" type="datetime-local" value={end} onChange={e => { setEnd(e.target.value); setError(null) }} />
      </div>

      {Number.isFinite(length) && length > 0 && (
        <p className={`small stc-duration ${datesOk ? 'muted' : 'stc-duration-bad'}`}>
          <Icon name="hourglass" size={13} /> {s.chDuration.replace('{d}', fmtLength(length, t))}
        </p>
      )}

      {error && <p className="form-error small">{error}</p>}

      <div className="recruit-actions">
        <button className="btn" disabled={busy || !title.trim()} onClick={submit}>
          {busy ? s.chSaving : edit ? s.chSaveBtn : s.chCreateBtn}
        </button>
        <button className="btn-mini" disabled={busy} onClick={onCancel}>{t.tournament.cancelForm}</button>
      </div>
    </div>
  )
}

function errorText(e: unknown, t: Translations): string {
  const code = e instanceof ApiError ? e.code : ''
  return t.studio.chErr[code] ?? t.studio.chErr.generic
}

/** «3 ч», «1 д 4 ч», «2 ч 30 м» — длина челленджа одним взглядом. */
function fmtLength(ms: number, t: Translations): string {
  const min = Math.round(ms / 60_000)
  const d = Math.floor(min / 1440), h = Math.floor(min % 1440 / 60), m = min % 60
  return [d && `${d} ${t.ch.daysShort}`, h && `${h} ${t.ch.hoursShort}`, m && `${m} ${t.ch.minShort}`]
    .filter(Boolean).join(' ') || `0 ${t.ch.minShort}`
}

/** «6 окт., 20:00 — 23:00»: в один день — дата один раз, иначе обе. */
function fmtPeriod(a: string, b: string, t: Translations): string {
  const from = new Date(a), to = new Date(b)
  const date: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }
  const time: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit' }
  const sameDay = from.toDateString() === to.toDateString()
  const left = `${from.toLocaleDateString(t.dateLocale, date)}, ${from.toLocaleTimeString(t.dateLocale, time)}`
  const right = sameDay
    ? to.toLocaleTimeString(t.dateLocale, time)
    : `${to.toLocaleDateString(t.dateLocale, date)}, ${to.toLocaleTimeString(t.dateLocale, time)}`
  return `${left} — ${right}`
}
