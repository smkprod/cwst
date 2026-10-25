import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../lib/api'
import { useT, type Translations } from '../lib/i18n'
import { haptic, hapticNotify } from '../lib/telegram'
import { openTrainer } from '../lib/trainer'
import { openTip } from '../lib/tipSheet'
import { ARENA_TIPS } from '../lib/tips'
import { QUESTS_CHANGED, QUEST_PINGED, markQuestsSeen, openDecks, rememberQuests, type QuestPinged } from '../lib/quests'
import type { DailyQuests, QuestItem, QuestKey } from '../types'
import { GameIcon, type GameIconName } from './GameIcon'
import { Icon } from './ui/Icon'

const ICONS: Record<QuestKey, GameIconName> = {
  war2: 'swords', win2: 'trophy', play5: 'play', crowns3: 'crown', flawless: 'gem', streak2: 'chart',
  trainer: 'target', tip: 'scroll', decks: 'cards',
}

export function questLabel(t: Translations, item: Pick<QuestItem, 'key' | 'target'>): string {
  return t.quests.labels[item.key].replace('{n}', String(item.target))
}

/**
 * «Задания дня» - первая карточка профиля: три задания, серия 🔥, что дадут за неё
 * и кто в клане сегодня уже закрыл. Повод зайти каждый день, а не только к войне.
 */
export function QuestsCard() {
  const { t } = useT()
  const s = t.quests
  const [q, setQ] = useState<DailyQuests | null>(null)
  const [hidden, setHidden] = useState(false)
  const [busy, setBusy] = useState(false)
  const [burst, setBurst] = useState(false)
  const [now, setNow] = useState(() => Date.now())

  const load = useCallback(() => {
    setBusy(true)
    api.getQuests()
      .then(next => { markQuestsSeen(); rememberQuests(next) })
      // Нет привязки или сервер не ответил - карточки просто нет
      .catch(() => setHidden(true))
      .finally(() => setBusy(false))
  }, [])

  useEffect(() => {
    load()
    const on = (e: Event) => {
      const next = (e as CustomEvent<DailyQuests>).detail
      if (next) setQ(next)
    }
    window.addEventListener(QUESTS_CHANGED, on)
    return () => window.removeEventListener(QUESTS_CHANGED, on)
  }, [load])

  // Таймер «новые через» - раз в минуту хватает
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 60_000)
    return () => window.clearInterval(id)
  }, [])

  // Вернулся в приложение после боёв - сразу пересчитываем по журналу
  useEffect(() => {
    const onVisible = () => { if (document.visibilityState === 'visible') load() }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [load])

  const celebrated = useRef<string | null>(null)
  useEffect(() => {
    if (!q?.justCompleted || celebrated.current === q.day) return
    celebrated.current = q.day
    hapticNotify('success')
    setBurst(true)
    confetti()
    const id = window.setTimeout(() => setBurst(false), 2400)
    return () => window.clearTimeout(id)
  }, [q])

  if (hidden || !q) return null

  const left = Math.max(0, new Date(q.resetsUtc).getTime() - now)
  const h = Math.floor(left / 3_600_000)
  const m = Math.floor(left / 60_000) % 60
  const leftText = h > 0 ? s.hours.replace('{h}', String(h)).replace('{m}', String(m)) : s.minutes.replace('{m}', String(m))
  const doneCount = q.items.filter(i => i.done).length

  const lead = q.completed ? s.doneLead
    : q.atRisk ? s.riskLead.replace('{n}', String(q.streak)).replace('{h}', leftText)
    : s.lead

  const reward = q.rewardDays > 0 ? s.rewarded.replace('{d}', String(q.rewardDays))
    : q.nextRewardIn === 1 ? (q.completed ? s.rewardNext : s.rewardToday).replace('{d}', String(q.nextRewardDays))
    : s.reward.replace('{n}', String(q.nextRewardIn)).replace('{d}', String(q.nextRewardDays))

  // Неделя серии точками: какой это день из семи, подарки на 3-м (в первую неделю) и 7-м
  const cycle = q.streak > 0 && q.streak % 7 === 0 ? 7 : q.streak % 7
  const firstWeek = q.streak < 7

  const go = (item: QuestItem) => {
    haptic('medium')
    if (item.key === 'trainer') openTrainer()
    else if (item.key === 'tip') openTip(ARENA_TIPS[new Date().getDate() % ARENA_TIPS.length])
    else if (item.key === 'decks') openDecks()
  }

  const toggleRemind = () => {
    haptic('light')
    const on = !q.remindOn
    setQ({ ...q, remindOn: on })
    api.setQuestRemind(on).catch(() => setQ(prev => prev && { ...prev, remindOn: !on }))
  }

  return (
    <section className={`card qs ${q.completed ? 'qs-done' : ''} ${q.atRisk ? 'qs-risk' : ''} ${burst ? 'qs-burst' : ''}`}>
      <div className="qs-head">
        <span className="qs-icon"><GameIcon name="target" size={26} /></span>
        <div className="qs-title">
          <b>{s.title}</b>
          <small>{q.completed ? s.resets.replace('{h}', leftText) : `${doneCount}/${q.items.length}`}</small>
        </div>
        <span className={`qs-flame ${q.streak > 0 ? 'on' : ''}`} title={s.best.replace('{n}', String(q.bestStreak))}>
          <GameIcon name="flame" size={20} /> {q.streak}
          <small>{s.streak}</small>
        </span>
      </div>

      <p className="qs-lead">{burst ? s.justDone.replace('{n}', String(q.streak)) : lead}</p>

      <ul className="qs-list">
        {q.items.map(item => {
          const pct = Math.round(100 * Math.min(item.progress, item.target) / item.target)
          return (
            <li key={item.key} className={`qs-item ${item.done ? 'done' : ''} qs-${item.kind}`}>
              <span className="qs-item-icon"><GameIcon name={ICONS[item.key]} size={22} /></span>
              <div className="qs-item-body">
                <span>{questLabel(t, item)}</span>
                {item.target > 1 && !item.done && <span className="qs-bar"><i style={{ width: `${pct}%` }} /></span>}
              </div>
              {item.done
                ? <span className="qs-check"><Icon name="check" size={16} /></span>
                : item.kind === 'app'
                  ? <button className="qs-go" onClick={() => go(item)}>{s.go}</button>
                  : <span className="qs-count">{item.target > 1 ? `${item.progress}/${item.target}` : '—'}</span>}
            </li>
          )
        })}
      </ul>

      {!q.completed && q.items.some(i => i.kind !== 'app' && !i.done) && (
        <button className="qs-refresh" onClick={() => { haptic('light'); load() }} disabled={busy}>
          <Icon name="refresh" size={13} /> {s.refresh}
          <span className="muted"> · {s.battlesNote}</span>
        </button>
      )}

      <div className="qs-reward">
        <div className="qs-week">
          {Array.from({ length: 7 }, (_, i) => {
            const day = i + 1
            const gift = day === 7 || (day === 3 && firstWeek)
            return <span key={day} className={`qs-dot ${day <= cycle ? 'on' : ''} ${gift ? 'gift' : ''}`}>{gift ? '🎁' : ''}</span>
          })}
        </div>
        <span className="qs-reward-text">{reward}</span>
      </div>

      {q.clan && (
        <div className="qs-clan">
          <span className="muted small">{q.clan.doneToday > 0 ? s.clanDone.replace('{n}', String(q.clan.doneToday)) : s.clanNone}</span>
          {q.clan.top.length > 0 && (
            <div className="qs-mates">
              {q.clan.top.map((m, i) => (
                <span key={`${m.name}-${i}`} className={`qs-mate ${m.me ? 'me' : ''} ${m.doneToday ? 'today' : ''}`}>
                  {m.name} <b>🔥{m.streak}</b>
                </span>
              ))}
            </div>
          )}
        </div>
      )}

      <label className="qs-remind" onClick={toggleRemind}>
        <span className={`qs-switch ${q.remindOn ? 'on' : ''}`}><i /></span>
        <span className="small">{s.remind}</span>
      </label>
    </section>
  )
}

/** Тост поверх любого экрана: задание из приложения засчитано или день закрыт. */
export function QuestToast() {
  const { t } = useT()
  const [msg, setMsg] = useState<{ text: string; big: boolean } | null>(null)

  useEffect(() => {
    let timer = 0
    const on = (e: Event) => {
      const { q, kind } = (e as CustomEvent<QuestPinged>).detail
      const item = q.items.find(i => i.key === kind)
      const text = q.justCompleted
        ? t.quests.justDone.replace('{n}', String(q.streak))
          + (q.rewardDays > 0 ? ` · ${t.quests.toastPlus.replace('{d}', String(q.rewardDays))}` : '')
        : `✓ ${t.quests.toastDone}: ${item ? questLabel(t, item) : ''} · ${q.items.filter(i => i.done).length}/${q.items.length}`
      if (q.justCompleted) confetti()
      hapticNotify('success')
      setMsg({ text, big: q.justCompleted })
      window.clearTimeout(timer)
      timer = window.setTimeout(() => setMsg(null), 3500)
    }
    window.addEventListener(QUEST_PINGED, on)
    return () => { window.removeEventListener(QUEST_PINGED, on); window.clearTimeout(timer) }
  }, [t])

  if (!msg) return null
  return (
    <div className={`qs-toast fade-up ${msg.big ? 'big' : ''}`} onClick={() => setMsg(null)}>
      <GameIcon name={msg.big ? 'flame' : 'target'} size={22} />
      <span>{msg.text}</span>
    </div>
  )
}

function confetti() {
  import('canvas-confetti').then(({ default: fire }) => {
    const base = { disableForReducedMotion: true, zIndex: 300, colors: ['#ffd34d', '#ff8a3d', '#ff5a5a', '#9c6bff', '#ffffff'] }
    fire({ ...base, particleCount: 80, spread: 70, startVelocity: 38, origin: { x: 0.5, y: 0.35 } })
  }).catch(() => { /* без салюта - не страшно */ })
}
