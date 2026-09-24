import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, adminClan } from '../lib/api'
import type { BroadcastTarget, Moderator, OwnerClan, OwnerClanDetail, OwnerStats, ServiceIdentity, ServicePermission } from '../types'

/** «Можно ли мне вот это». Прокидывается вниз, чтобы правила жили в одном месте. */
type Can = (p: ServicePermission) => boolean
import { haptic, hapticNotify, openExternalLink } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { SignupsChart } from './SignupsChart'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; clans: OwnerClan[]; stats: OwnerStats }

/** Лидер в клане ровно один, соруков может быть много — значки обязаны различаться. */
const ROLE_LABEL: Record<string, string> = {
  leader: '👑 Глава',
  coLeader: '⚜️ Сорук',
  elder: '⭐ Старейшина',
}

type Section = 'overview' | 'clans' | 'broadcast' | 'moderators'
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

export function OwnerPanel({ me }: { me: ServiceIdentity }) {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [section, setSection] = useState<Section>('overview')
  const { t } = useT()

  // Права разбираются здесь один раз и дальше передаются вниз. Сервер всё равно
  // проверяет сам — это только про то, чтобы не рисовать кнопку, которая
  // гарантированно ответит отказом.
  const can = useCallback(
    (p: ServicePermission) => me.role === 'owner' || me.permissions.includes(p),
    [me],
  )

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
    // Рассылка и модераторы — только владельцу. Вкладки, которые всё равно
    // ответят отказом, лучше не рисовать вовсе.
    ...(can('Broadcast') ? [{ key: 'broadcast' as Section, label: '📣 Рассылка' }] : []),
    ...(can('ManageModerators') ? [{ key: 'moderators' as Section, label: '🛡 Модераторы' }] : []),
  ]

  return (
    <div>
      <h2 className="section-title">{t.owner.title}</h2>

      {me.role !== 'owner' && <p className="muted small adm-role-note">{t.owner.moderatorNote}</p>}

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
      {section === 'clans' && <ClansSection clans={clans} onChanged={load} can={can} t={t} />}
      {section === 'broadcast' && can('Broadcast') && (
        <BroadcastBox dmCount={stats.usersReachableByDm} chatCount={stats.chatsWithBot} t={t} />
      )}
      {section === 'moderators' && can('ManageModerators') && <ModeratorsSection can={can} t={t} />}
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

      <SignupsChart
        title="📈 Кто и когда приходил"
        points={stats.signups ?? []}
        metrics={[
          { key: 'users', label: 'Игроки', noun: 'игроков' },
          { key: 'clans', label: 'Кланы', noun: 'кланов' },
        ]}
      />

      {/* Отдельной карточкой, а не ещё одним переключателем в графике прихода:
          «сколько пришло впервые» и «сколько заходит каждый день» — разные вопросы,
          и мешать их в одном заголовке значит путать самого себя. */}
      <SignupsChart
        title="🔥 Кто заходит каждый день"
        points={stats.signups ?? []}
        metrics={[
          { key: 'active', label: 'Заходили', noun: 'человек' },
          { key: 'acting', label: 'Что-то делали', noun: 'человек' },
        ]}
      />

      <p className="muted small chart-footnote">
        Активность считается с момента, когда бот начал её записывать, — за более
        ранние дни здесь нули, а не «никого не было».
      </p>

      {/* Честность: у привязавшихся до появления поля даты нет, и в график они
          не попадут. Молча занизить прошлое хуже, чем сказать об этом строкой. */}
      {stats.totalLinkedUsers > stats.usersWithKnownDate && (
        <p className="muted small chart-footnote">
          {stats.totalLinkedUsers - stats.usersWithKnownDate} игроков привязались до того,
          как бот начал запоминать дату — в графике их нет.
        </p>
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
        <Row
          label="Дойдёт рассылка в ЛС"
          value={stats.usersReachableByDm}
          accent={stats.usersReachableByDm < stats.totalLinkedUsers ? 'warn' : 'good'}
        />
        <Row label="Пришли по приглашению" value={stats.invitedUsers} />
        {stats.usersReachableByDm < stats.totalLinkedUsers && (
          <p className="muted small adm-note">
            Привязанных лидером через /bind бот тегает в чате, но написать им в личные
            сообщения не может, пока человек сам не нажмёт «Старт» — так устроен Telegram.
          </p>
        )}
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

function ClansSection({ clans, onChanged, can, t }: {
  clans: OwnerClan[]; onChanged: () => void; can: Can; t: Translations
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
        {shown.map(c => <ClanCard key={c.id} clan={c} onChanged={onChanged} can={can} t={t} />)}
      </ul>
    </>
  )
}

function ClanCard({ clan: c, onChanged, can, t }: {
  clan: OwnerClan; onChanged: () => void; can: Can; t: Translations
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
              {/* Каждый пункт целиком, иначе перенос рвёт «без @username:» и число */}
              <p className="muted small adm-detail-meta">
                <span>В клане по CR: {detail.clanMemberCount || '—'}</span>
                <span>· привязано к боту: {detail.members.length}</span>
                {detail.members.some(m => !m.telegramUsername) && (
                  <span>· без @username: {detail.members.filter(m => !m.telegramUsername).length}</span>
                )}
                {detail.members.some(m => m.telegramUsername && m.telegramUserId === null) && (
                  <span>· 💬 только тег: {detail.members.filter(m => m.telegramUsername && m.telegramUserId === null).length}</span>
                )}
              </p>

              {detail.members.length === 0 && (
                <p className="muted small">Никто ещё не привязал аккаунт.</p>
              )}

              {detail.members.map(m => (
                <div key={m.playerTag} className={`adm-member ${m.isLeader ? 'adm-member-leader' : ''}`}>
                  <span className="adm-member-name">
                    {m.role && ROLE_LABEL[m.role] && (
                      <span className={`role-badge role-${m.role}`}>{ROLE_LABEL[m.role]}</span>
                    )}
                    <span className="adm-member-nick">{m.name}</span>
                  </span>
                  {m.telegramUsername ? (
                    <button
                      className="adm-member-tg"
                      onClick={() => { haptic('light'); openExternalLink(`https://t.me/${m.telegramUsername}`) }}
                    >
                      @{m.telegramUsername}
                      {/* Привязан лидером: тегнуть можно, написать в ЛС — нет */}
                      {m.telegramUserId === null && <span className="adm-tag-only" title="только тег в чате">💬</span>}
                    </button>
                  ) : (
                    <span className="muted small">нет @username</span>
                  )}
                </div>
              ))}
            </>
          )}

          <div className="owner-actions">
            {/* Заход в клан — не «ещё одна кнопка тарифа», поэтому стоит отдельно
                и первым: это главное, зачем сюда открывают карточку. */}
            {can('EnterClans') && (
              <button
                className="btn-mini adm-enter-btn"
                onClick={() => { haptic('medium'); adminClan.enter(c.id); window.location.reload() }}
              >
                {t.owner.enterClan}
              </button>
            )}
          </div>

          <div className="owner-actions">
            {can('Plans') && <>
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro', 30)}>{t.owner.pro30}</button>
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro', 90)}>{t.owner.pro90}</button>
            <button className="btn-mini" disabled={busy} onClick={() => setPlan('pro')}>{t.owner.proInf}</button>
            <button className="btn-mini btn-mini-danger" disabled={busy} onClick={() => setPlan('free')}>{t.owner.free}</button>
            </>}
            {can('DeleteClans') && <button
              className="btn-mini btn-mini-danger owner-delete"
              disabled={busy}
              onClick={remove}
              onBlur={() => setConfirmDelete(false)}
            >
              {confirmDelete ? t.owner.confirmDelete : t.owner.delete}
            </button>}
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

/* ---------- Модераторы ---------- */

/** Порядок в списке — от безобидного к опасному: галочки читают сверху вниз. */
const PERMISSIONS: ServicePermission[] = [
  'EnterClans', 'ManageClans', 'ChatAdmin',
  'Plans', 'Broadcast', 'DeleteClans', 'Maintenance', 'ManageModerators',
]

/**
 * Назначение по юзернейму с набором прав.
 *
 * Юзернейм — единственное, чем можно назвать человека, который ещё ни разу не
 * открывал приложение: числового id у нас до этого момента просто нет. Поэтому
 * запись сначала живёт «неподтверждённой», и это состояние честно показано:
 * пока человек не зайдёт, она держится на имени, а имя в Telegram можно сменить.
 */
function ModeratorsSection({ can, t }: { can: Can; t: Translations }) {
  const [list, setList] = useState<Moderator[] | null>(null)
  const [username, setUsername] = useState('')
  const [note, setNote] = useState('')
  const [picked, setPicked] = useState<ServicePermission[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    api.ownerGetModerators().then(setList).catch(() => setList([]))
  }, [])
  useEffect(load, [load])

  const toggle = (p: ServicePermission) => {
    haptic('light')
    setPicked(prev => prev.includes(p) ? prev.filter(x => x !== p) : [...prev, p])
  }

  const add = async () => {
    const clean = username.trim().replace(/^@/, '')
    if (clean.length < 5) { setError(t.owner.modBadUsername); return }

    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.ownerAddModerator(clean, note.trim() || undefined, picked)
      hapticNotify('success')
      setUsername('')
      setNote('')
      setPicked([])
      load()
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError && e.code === 'already_moderator'
        ? t.owner.modAlready
        : e instanceof ApiError && e.code === 'bad_username'
          ? t.owner.modBadUsername
          : e instanceof ApiError && e.code === 'cannot_grant'
            ? t.owner.modCannotGrant
            : t.owner.error)
    } finally {
      setBusy(false)
    }
  }

  const remove = async (m: Moderator) => {
    haptic('medium')
    setBusy(true)
    try {
      await api.ownerRemoveModerator(m.id)
      hapticNotify('success')
      load()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card">
      <p className="adm-block-title">{t.owner.modTitle}</p>
      <p className="muted small">{t.owner.modHint}</p>

      <div className="form-field">
        <input
          className="search-input"
          value={username}
          onChange={e => setUsername(e.target.value)}
          placeholder="@username"
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          maxLength={33}
        />
      </div>
      <div className="form-field">
        <input
          className="search-input"
          value={note}
          onChange={e => setNote(e.target.value)}
          placeholder={t.owner.modNotePlaceholder}
          maxLength={200}
        />
      </div>

      <p className="muted small adm-perm-title">{t.owner.modRights}</p>
      <PermissionPicker picked={picked} can={can} onToggle={toggle} t={t} />

      {error && <p className="form-error small">{error}</p>}

      <button className="btn" disabled={busy || username.trim().length < 5} onClick={add}>
        {busy ? t.owner.saving : t.owner.modAdd}
      </button>

      {list === null && <div className="center"><div className="spinner" /></div>}
      {list !== null && list.length === 0 && (
        <p className="muted small adm-mod-empty">{t.owner.modEmpty}</p>
      )}

      <ul className="owner-list adm-mod-list">
        {(list ?? []).map(m => (
          <ModeratorRow key={m.id} mod={m} can={can} onChanged={load} onRemove={() => remove(m)} t={t} />
        ))}
      </ul>
    </div>
  )
}

/**
 * Галочки прав.
 *
 * Право, которого нет у самого выдающего, показано выключенным и с подписью:
 * сервер такую выдачу всё равно отклонит, и честнее объяснить это заранее, чем
 * дать нажать и ответить отказом.
 */
function PermissionPicker({ picked, can, onToggle, t }: {
  picked: ServicePermission[]; can: Can
  onToggle: (p: ServicePermission) => void; t: Translations
}) {
  return (
    <div className="adm-perms">
      {PERMISSIONS.map(p => {
        const allowed = can(p)
        return (
          <label key={p} className={`adm-perm ${allowed ? '' : 'adm-perm-off'}`}>
            <input
              type="checkbox"
              checked={picked.includes(p)}
              disabled={!allowed}
              onChange={() => onToggle(p)}
            />
            <span className="adm-perm-text">
              <span className="adm-perm-name">{t.owner.perm[p]}</span>
              <span className="muted small">{t.owner.permHint[p]}</span>
            </span>
          </label>
        )
      })}
    </div>
  )
}

/** Строка модератора: права можно поменять на месте, не пересоздавая запись. */
function ModeratorRow({ mod, can, onChanged, onRemove, t }: {
  mod: Moderator; can: Can; onChanged: () => void; onRemove: () => void; t: Translations
}) {
  const [open, setOpen] = useState(false)
  const [picked, setPicked] = useState<ServicePermission[]>(mod.permissions)
  const [busy, setBusy] = useState(false)

  const toggle = (p: ServicePermission) => {
    haptic('light')
    setPicked(prev => prev.includes(p) ? prev.filter(x => x !== p) : [...prev, p])
  }

  const save = async () => {
    haptic('medium')
    setBusy(true)
    try {
      await api.ownerSetModeratorPermissions(mod.id, picked)
      hapticNotify('success')
      setOpen(false)
      onChanged()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="adm-mod-row-wrap">
      <div className="adm-mod-row">
        <div className="adm-mod-main">
          <span className="adm-mod-name">@{mod.username}</span>
          {mod.note && <span className="muted small">{mod.note}</span>}
          <span className="muted small">
            {mod.confirmed ? t.owner.modConfirmed : t.owner.modPending}
          </span>
          <span className="muted small">
            {mod.permissions.length === 0
              ? t.owner.permNone
              : mod.permissions.map(p => t.owner.perm[p]).join(' · ')}
          </span>
        </div>
        <div className="adm-mod-btns">
          <button className="btn-mini" onClick={() => { haptic('light'); setOpen(o => !o) }}>
            {open ? t.owner.modClose : t.owner.modEditRights}
          </button>
          <button className="btn-mini btn-mini-danger" disabled={busy} onClick={onRemove}>
            {t.owner.modRemove}
          </button>
        </div>
      </div>

      {open && (
        <div className="adm-mod-edit">
          <PermissionPicker picked={picked} can={can} onToggle={toggle} t={t} />
          <button className="btn-mini btn-mini-primary" disabled={busy} onClick={save}>
            {busy ? t.owner.saving : t.owner.modSaveRights}
          </button>
        </div>
      )}
    </li>
  )
}
