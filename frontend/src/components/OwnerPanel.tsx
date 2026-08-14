import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { BroadcastTarget, OwnerClan, OwnerClanDetail, OwnerStats } from '../types'
import { haptic, hapticNotify, openExternalLink } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; clans: OwnerClan[]; stats: OwnerStats }

type Section = 'overview' | 'clans' | 'broadcast'
type ClanFilter = 'all' | 'pro' | 'free' | 'silent' | 'expiring'

/** Сколько дней назад (для «активность» и «подключён»). null — даты нет. */
function daysAgo(iso: string | null): number | null {
  if (!iso) return null
  return Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
}

function ago(iso: string | null): string {
  const d = daysAgo(iso)
  if (d === null) return '—'
  if (d === 0) return 'сегодня'
  if (d === 1) return 'вчера'
  return `${d} дн. назад`
}

export function OwnerPanel() {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [section, setSection] = useState<Section>('overview')
  const { t } = useT()

  const load = useCallback(() => {
    Promise.all([api.ownerGetClans(), api.ownerGetStats()])
      .then(([clans, stats]) => setState({ kind: 'ready', clans, stats }))
      .catch(() => setState({ kind: 'error' }))
  }, [])

  useEffect(load, [load])

  if (state.kind === 'loading') return <div className="center"><div className="spinner" /></div>
  if (state.kind === 'error') return <p className="center muted">{t.owner.error}</p>

  const { stats, clans } = state
  const sections: { key: Section; label: string }[] = [
    { key: 'overview', label: '📊 Сводка' },
    { key: 'clans', label: `🏰 Кланы ${clans.length}` },
    { key: 'broadcast', label: '📣 Рассылка' },
  ]

  return (
    <div>
      <h2 className="section-title">{t.owner.title}</h2>

      <div className="adm-tabs">
        {sections.map(s => (
          <button
            key={s.key}
            className={`adm-tab ${section === s.key ? 'adm-tab-on' : ''}`}
            onClick={() => { haptic('light'); setSection(s.key) }}
          >
            {s.label}
          </button>
        ))}
      </div>

      {section === 'overview' && <Overview stats={stats} clans={clans} />}
      {section === 'clans' && <ClansSection clans={clans} onChanged={load} t={t} />}
      {section === 'broadcast' && (
        <BroadcastBox dmCount={stats.totalLinkedUsers} chatCount={stats.chatsWithBot} t={t} />
      )}
    </div>
  )
}

/* ---------- Сводка ---------- */

function Overview({ stats, clans }: { stats: OwnerStats; clans: OwnerClan[] }) {
  const conversion = stats.totalLinkedUsers > 0
    ? Math.round(stats.usersWithClan * 100 / stats.totalLinkedUsers)
    : 0
  const proShare = stats.totalClans > 0
    ? Math.round(stats.proClans * 100 / stats.totalClans)
    : 0
  const expiring = clans.filter(c => c.daysLeft !== null && c.daysLeft <= 7)
  const silent = clans.filter(c => !c.isActive)

  return (
    <>
      {/* Главные три числа — то, что смотришь первым делом */}
      <div className="adm-hero">
        <HeroStat value={stats.totalClans} label="кланов" sub={`${stats.proClans} PRO`} />
        <HeroStat value={stats.totalLinkedUsers} label="игроков" sub={`+${stats.newUsers7d} за неделю`} />
        <HeroStat value={`${proShare}%`} label="на PRO" sub={`${stats.freeClans} на Free`} />
      </div>

      {/* Требует внимания */}
      {(expiring.length > 0 || silent.length > 0) && (
        <div className="card adm-alert-card">
          <p className="adm-block-title">⚠️ Требует внимания</p>
          {expiring.length > 0 && (
            <p className="adm-alert-row">
              <b>{expiring.length}</b> клан(ов) с истекающим PRO:{' '}
              <span className="muted">{expiring.map(c => c.name).join(', ')}</span>
            </p>
          )}
          {silent.length > 0 && (
            <p className="adm-alert-row">
              <b>{silent.length}</b> клан(ов) молчат больше недели:{' '}
              <span className="muted">{silent.slice(0, 5).map(c => c.name).join(', ')}
                {silent.length > 5 && ` и ещё ${silent.length - 5}`}</span>
            </p>
          )}
        </div>
      )}

      <Block title="🏰 Кланы">
        <Row label="Всего подключено" value={stats.totalClans} />
        <Row label="На PRO" value={stats.proClans} accent="good" />
        <Row label="На Free" value={stats.freeClans} />
        <Row label="Активны за неделю" value={stats.activeClans7d} accent={stats.silentClans > 0 ? undefined : 'good'} />
        <Row label="Молчат больше недели" value={stats.silentClans} accent={stats.silentClans > 0 ? 'bad' : undefined} />
        <Row label="Подключён чат бота" value={stats.chatsWithBot} />
        <Row label="Игроков на клан (в среднем)" value={stats.avgLinkedPerClan} />
      </Block>

      <Block title="👥 Игроки">
        <Row label="Привязали аккаунт" value={stats.totalLinkedUsers} />
        <Row label="Состоят в клане" value={stats.usersWithClan} />
        <Row label="Без клана" value={stats.usersWithoutClan} accent={stats.usersWithoutClan > 0 ? 'warn' : undefined} />
        <Row label="Конверсия в клан" value={`${conversion}%`} />
        <Row
          label="Можно тегнуть (есть @username)"
          value={stats.usersWithUsername}
          accent={stats.usersWithUsername < stats.totalLinkedUsers ? 'warn' : 'good'}
        />
        <Row label="Пришли по приглашению" value={stats.invitedUsers} />
      </Block>

      <Block title="📈 Рост">
        <Row label="Новых кланов за 7 дней" value={stats.newClans7d} />
        <Row label="Новых кланов за 30 дней" value={stats.newClans30d} />
        <Row label="Новых игроков за 7 дней" value={stats.newUsers7d} />
        <Row label="Новых игроков за 30 дней" value={stats.newUsers30d} />
        <p className="muted small adm-note">
          Считается только по записям с датой подключения: {stats.clansWithKnownDate} из {stats.totalClans} кланов,
          {' '}{stats.usersWithKnownDate} из {stats.totalLinkedUsers} игроков. У подключённых раньше даты нет.
        </p>
      </Block>

      <Block title="💎 PRO">
        <Row label="Активных PRO" value={stats.proClans} accent="good" />
        <Row label="Истекает в ближайшие 7 дней" value={stats.proExpiring7d} accent={stats.proExpiring7d > 0 ? 'warn' : undefined} />
        <Row label="Уже истёк" value={stats.proExpired} accent={stats.proExpired > 0 ? 'bad' : undefined} />
        <Row label="Бессрочный" value={stats.proForever} />
      </Block>

      <Block title="🔥 Вовлечённость">
        <Row label="Респектов за неделю" value={stats.respects7d} />
      </Block>
    </>
  )
}

function HeroStat({ value, label, sub }: { value: number | string; label: string; sub?: string }) {
  return (
    <div className="card adm-hero-cell">
      <span className="adm-hero-value">{value}</span>
      <span className="adm-hero-label">{label}</span>
      {sub && <span className="muted small">{sub}</span>}
    </div>
  )
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card adm-block">
      <p className="adm-block-title">{title}</p>
      {children}
    </div>
  )
}

function Row({ label, value, accent }: {
  label: string; value: number | string; accent?: 'good' | 'bad' | 'warn'
}) {
  return (
    <div className="adm-row">
      <span className="adm-row-label">{label}</span>
      <span className={`adm-row-value ${accent ? `adm-${accent}` : ''}`}>{value}</span>
    </div>
  )
}

/* ---------- Кланы ---------- */

function ClansSection({ clans, onChanged, t }: {
  clans: OwnerClan[]; onChanged: () => void; t: Translations
}) {
  const [filter, setFilter] = useState<ClanFilter>('all')
  const [query, setQuery] = useState('')

  const filters: { key: ClanFilter; label: string; count: number }[] = [
    { key: 'all', label: 'Все', count: clans.length },
    { key: 'pro', label: 'PRO', count: clans.filter(c => c.plan === 'pro').length },
    { key: 'free', label: 'Free', count: clans.filter(c => c.plan === 'free').length },
    { key: 'expiring', label: 'Истекают', count: clans.filter(c => c.daysLeft !== null && c.daysLeft <= 7).length },
    { key: 'silent', label: 'Молчат', count: clans.filter(c => !c.isActive).length },
  ]

  const q = query.trim().toLowerCase()
  const shown = clans
    .filter(c => filter === 'all'
      || (filter === 'pro' && c.plan === 'pro')
      || (filter === 'free' && c.plan === 'free')
      || (filter === 'silent' && !c.isActive)
      || (filter === 'expiring' && c.daysLeft !== null && c.daysLeft <= 7))
    .filter(c => q === '' || c.name.toLowerCase().includes(q) || c.clanTag.toLowerCase().includes(q))

  return (
    <>
      <input
        className="adm-search"
        placeholder="Поиск по названию или тегу"
        value={query}
        onChange={e => setQuery(e.target.value)}
      />

      <div className="adm-filters">
        {filters.map(f => (
          <button
            key={f.key}
            className={`btn-mini ${filter === f.key ? 'adm-filter-on' : ''}`}
            onClick={() => { haptic('light'); setFilter(f.key) }}
          >
            {f.label} {f.count}
          </button>
        ))}
      </div>

      {shown.length === 0 && <p className="center muted">Ничего не найдено</p>}

      <ul className="owner-list">
        {shown.map(c => <ClanCard key={c.id} clan={c} onChanged={onChanged} t={t} />)}
      </ul>
    </>
  )
}

function ClanCard({ clan: c, onChanged, t }: {
  clan: OwnerClan; onChanged: () => void; t: Translations
}) {
  const [open, setOpen] = useState(false)
  const [detail, setDetail] = useState<OwnerClanDetail | null>(null)
  const [busy, setBusy] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const toggle = () => {
    haptic('light')
    const next = !open
    setOpen(next)
    if (next && !detail) {
      api.ownerGetClanDetail(c.id).then(setDetail).catch(() => setDetail(null))
    }
  }

  const setPlan = async (tier: 'pro' | 'free', days?: number) => {
    haptic('medium')
    setBusy(true)
    try {
      await api.ownerSetPlan(c.id, tier, days)
      hapticNotify('success')
      onChanged()
    } catch { hapticNotify('error') } finally { setBusy(false) }
  }

  const remove = async () => {
    haptic('medium')
    if (!confirmDelete) { setConfirmDelete(true); return }
    setBusy(true)
    try {
      await api.ownerDeleteClan(c.id)
      hapticNotify('success')
      onChanged()
    } catch { hapticNotify('error') } finally { setBusy(false); setConfirmDelete(false) }
  }

  const expiringSoon = c.daysLeft !== null && c.daysLeft <= 7

  return (
    <li className="card owner-card">
      <button className="adm-card-head" onClick={toggle}>
        <div className="owner-card-info">
          <span className="owner-clan-name">
            {c.name}
            {!c.isActive && <span className="adm-dot adm-dot-bad" title="нет данных больше недели" />}
          </span>
          <span className="muted small">
            {c.clanTag} · 👥 {c.linkedPlayers} · {c.hasChat ? '💬 чат' : '⚠️ без чата'}
          </span>
          <span className="muted small">
            Активность: {ago(c.lastActivityUtc)}
            {c.createdAtUtc && ` · подключён ${ago(c.createdAtUtc)}`}
          </span>
        </div>
        <div className="adm-card-right">
          <span className={`plan-badge ${c.plan === 'pro' ? 'plan-pro' : 'plan-free'}`}>
            {c.plan === 'pro' ? 'PRO' : 'FREE'}
          </span>
          {c.daysLeft !== null && (
            <span className={`muted small ${expiringSoon ? 'adm-warn' : ''}`}>{c.daysLeft} дн.</span>
          )}
          <span className="muted small">{open ? '▲' : '▼'}</span>
        </div>
      </button>

      {open && (
        <div className="adm-card-body">
          {!detail && <div className="center"><div className="spinner" /></div>}

          {detail && (
            <>
              <p className="muted small">
                В клане по CR: {detail.clanMemberCount || '—'} · привязано к боту: {detail.members.length}
                {detail.members.some(m => !m.telegramUsername) &&
                  ` · без @username: ${detail.members.filter(m => !m.telegramUsername).length}`}
              </p>

              {detail.members.length === 0 && (
                <p className="muted small">Никто ещё не привязал аккаунт.</p>
              )}

              {detail.members.map(m => (
                <div key={m.playerTag} className={`adm-member ${m.isLeader ? 'adm-member-leader' : ''}`}>
                  <span className="adm-member-name">
                    {m.isLeader && '👑 '}{m.name}
                  </span>
                  {m.telegramUsername ? (
                    <button
                      className="adm-member-tg"
                      onClick={() => { haptic('light'); openExternalLink(`https://t.me/${m.telegramUsername}`) }}
                    >
                      @{m.telegramUsername}
                    </button>
                  ) : (
                    <span className="muted small">нет @username</span>
                  )}
                </div>
              ))}
            </>
          )}

          <div className="owner-actions">
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro', 30)}>{t.owner.pro30}</button>
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro', 90)}>{t.owner.pro90}</button>
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro')}>{t.owner.proInf}</button>
            <button className="btn-mini btn-mini-danger" disabled={busy} onClick={() => setPlan('free')}>{t.owner.free}</button>
            <button
              className="btn-mini btn-mini-danger owner-delete"
              disabled={busy}
              onClick={remove}
              onBlur={() => setConfirmDelete(false)}
            >
              {confirmDelete ? t.owner.confirmDelete : t.owner.delete}
            </button>
          </div>
          {confirmDelete && <p className="muted small owner-delete-hint">{t.owner.deleteHint}</p>}
        </div>
      )}
    </li>
  )
}

/* ---------- Рассылка ---------- */

function BroadcastBox({ dmCount, chatCount, t }: { dmCount: number; chatCount: number; t: Translations }) {
  const [text, setText] = useState('')
  const [target, setTarget] = useState<BroadcastTarget>('both')
  const [busy, setBusy] = useState(false)
  const [confirm, setConfirm] = useState(false)
  const [result, setResult] = useState<string | null>(null)

  const willDm = target === 'dm' || target === 'both'
  const willChats = target === 'chats' || target === 'both'
  const recipients = [
    willDm ? `${dmCount} ${t.owner.bcDmUnit}` : null,
    willChats ? `${chatCount} ${t.owner.bcChatsUnit}` : null,
  ].filter(Boolean).join(' · ')

  const send = async () => {
    haptic('medium')
    if (!confirm) { setConfirm(true); return }
    setBusy(true)
    setResult(null)
    try {
      const r = await api.ownerBroadcast(text.trim(), target)
      hapticNotify('success')
      setResult(`${t.owner.bcDone} ${r.sentDm} ${t.owner.bcDmUnit} · ${r.sentChats} ${t.owner.bcChatsUnit}`)
      setText('')
    } catch {
      hapticNotify('error')
      setResult(t.owner.bcError)
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  const targets: { key: BroadcastTarget; label: string }[] = [
    { key: 'dm', label: t.owner.bcTargetDm },
    { key: 'chats', label: t.owner.bcTargetChats },
    { key: 'both', label: t.owner.bcTargetBoth },
  ]

  return (
    <div className="card owner-bc-card">
      <p className="adm-block-title">{t.owner.bcTitle}</p>
      <textarea
        className="owner-bc-text"
        rows={4}
        maxLength={4000}
        placeholder={t.owner.bcPlaceholder}
        value={text}
        onChange={e => { setText(e.target.value); setConfirm(false) }}
      />
      <div className="owner-bc-targets">
        {targets.map(tg => (
          <button
            key={tg.key}
            className={`btn-mini ${target === tg.key ? 'owner-bc-target-on' : ''}`}
            onClick={() => { haptic('light'); setTarget(tg.key); setConfirm(false) }}
          >
            {tg.label}
          </button>
        ))}
      </div>
      <p className="muted small owner-bc-recipients">{t.owner.bcRecipients} {recipients}</p>
      <button
        className="btn btn-nudge"
        disabled={busy || text.trim().length === 0}
        onClick={send}
        onBlur={() => setConfirm(false)}
      >
        {busy ? t.owner.bcSending : confirm ? t.owner.bcConfirm : t.owner.bcSend}
      </button>
      {result && <p className="muted small owner-bc-result">{result}</p>}
    </div>
  )
}
