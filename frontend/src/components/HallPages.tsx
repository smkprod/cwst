import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { ClanDesignKey, ClanPage, PlayerPage, PageWeek } from '../types'
import { fmt } from '../lib/format'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { BADGE_ICONS } from '../lib/sponsorMarks'

const LEVEL_CLS = ['', 'badge-bronze', 'badge-silver', 'badge-gold']

/** Что открыто поверх Аллеи. null — ничего, видна сама Аллея. */
export type HallTarget =
  | { kind: 'clan'; clanId: number }
  | { kind: 'player'; playerTag: string }

/**
 * Страница клана.
 *
 * Здесь же живёт кнопка «написать»: раньше она висела на строке списка, и первые
 * три клана, стоящие на подиуме, оставались недоступны — нажать по картинке было
 * нечем. То есть недоступны были ровно те кланы, которым интереснее всего писать.
 */
export function ClanPageView({ clanId, onOpenPlayer, onBack }: {
  clanId: number
  onOpenPlayer: (playerTag: string) => void
  onBack: () => void
}) {
  const { t } = useT()
  const [page, setPage] = useState<ClanPage | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [editing, setEditing] = useState(false)
  const [writing, setWriting] = useState(false)

  const load = () => {
    setState('loading')
    api.getClanPage(clanId)
      .then(p => { setPage(p); setState('ready') })
      .catch(() => setState('error'))
  }

  useEffect(load, [clanId])

  if (state === 'loading') return <Loading onBack={onBack} t={t} />
  if (state === 'error' || !page) return <Failed text={t.hallPage.clanNotFound} onBack={onBack} t={t} />

  return (
    <div className={`fade-in page-sheet design-${page.designKey}`}>
      <BackBar onBack={onBack} t={t} />

      <header
        className={`page-hero ${page.backgroundKey ? 'page-hero-bg' : ''}`}
        style={page.backgroundKey ? { backgroundImage: `url(/bg/${page.backgroundKey}.webp)` } : undefined}
      >
        <span className="page-hero-veil" />
        <div className="page-hero-body page-hero-split">
          <div className="page-hero-left">
            <span className="page-hero-crest">🏰</span>
            <h2 className="page-hero-name">
              {page.clanName}
              {page.sponsorCount > 0 && <span className="hall-sponsor-tag">★</span>}
            </h2>
            <span className="page-hero-tag">{page.clanTag}</span>
            {page.motto && <p className="page-hero-motto">«{page.motto}»</p>}
            {page.rank === 0 && <span className="muted small">{t.hallPage.notRanked}</span>}
          </div>

          {/* Тот же медальон, что и у игрока: место — главное на обеих страницах,
              и если рисовать его двумя разными способами, они перестают читаться
              как один раздел. Клана вне зачёта нет и медальона: ноль в круге
              выглядел бы как место, которого нет. */}
          {page.rank > 0 && (
            <div className={`rank-medal ${page.rank <= 3 ? `rank-medal-${page.rank}` : ''}`}>
              <span className="rank-medal-num">{page.rank}</span>
              <span className="rank-medal-of">{t.hall.outOf} {page.clansCounted}</span>
            </div>
          )}
        </div>
      </header>

      <div className="page-stats">
        <Stat value={fmt(page.seasonFame)} label={t.hallPage.medals} />
        <Stat value={String(page.weeksPlayed)} label={t.hallPage.weeks} />
        <Stat value={String(page.membersInSeason)} label={t.hallPage.inSeason} />
        <Stat value={String(page.sponsorCount)} label={t.hallPage.sponsors} />
      </div>

      <div className="page-actions">
        {!page.isMine && (
          page.acceptsMail
            ? <button className="btn" onClick={() => { haptic('medium'); setWriting(true) }}>
                {t.hallPage.write}
              </button>
            : <p className="muted small">{t.hallPage.mailOff}</p>
        )}
        {page.canEdit && (
          <button className="btn btn-ghost" onClick={() => { haptic('light'); setEditing(true) }}>
            {t.hallPage.design}
          </button>
        )}
      </div>

      {page.weeks.length > 1 && (
        <section className="card">
          <div className="card-title">{t.hallPage.weeksChart}</div>
          <WeekBars weeks={page.weeks} t={t} />
        </section>
      )}

      <section className="card">
        <div className="card-title">{t.hallPage.members}</div>
        {page.members.length === 0 ? (
          <p className="muted small">{t.hallPage.noMembers}</p>
        ) : (
          <ul className="hall-list">
            {page.members.map(m => (
              <li key={m.playerTag}>
                <button
                  className={`hall-row hall-row-btn ${m.backgroundKey ? 'hall-row-bg' : ''}`}
                  style={m.backgroundKey ? { backgroundImage: `url(/bg/${m.backgroundKey}.webp)` } : undefined}
                  onClick={() => { haptic('light'); onOpenPlayer(m.playerTag) }}
                >
                  {m.backgroundKey && <span className="hall-row-veil" />}
                  <span className="hall-rank">#{m.rank}</span>
                  <span className="hall-name">
                    {m.name}
                    {m.badgeKey && m.badgeLevel > 0 && (
                      <span className={`hall-badge ${LEVEL_CLS[m.badgeLevel] ?? ''}`}>
                        {BADGE_ICONS[m.badgeKey] ?? '🏅'}
                      </span>
                    )}
                    {m.isSponsor && <span className="hall-sponsor-tag">★</span>}
                  </span>
                  <span className="hall-sub muted small">{t.hallPage.hallRankShort} #{m.hallRank}</span>
                  <span className="hall-fame">{fmt(m.seasonFame)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {editing && (
        <DesignModal
          page={page}
          onClose={() => setEditing(false)}
          onSaved={() => { setEditing(false); load() }}
          t={t}
        />
      )}

      {writing && (
        <ClanMessageModal
          clanId={page.clanId}
          clanName={page.clanName}
          onClose={() => setWriting(false)}
          t={t}
        />
      )}
    </div>
  )
}

/** Страница игрока: только витрина — место, медали, значки, оформление. */
export function PlayerPageView({ playerTag, onOpenClan, onBack }: {
  playerTag: string
  onOpenClan: (clanId: number) => void
  onBack: () => void
}) {
  const { t } = useT()
  const [page, setPage] = useState<PlayerPage | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    setState('loading')
    api.getPlayerPage(playerTag)
      .then(p => { setPage(p); setState('ready') })
      .catch(() => setState('error'))
  }, [playerTag])

  if (state === 'loading') return <Loading onBack={onBack} t={t} />
  if (state === 'error' || !page) return <Failed text={t.hallPage.playerNotFound} onBack={onBack} t={t} />

  // Место — главное, что есть на этой странице, и до перестройки оно стояло
  // мелкой строкой под именем, ниже картинки. Медальон выносит его вправо от
  // имени: на страницу приходят посмотреть, какой человек по счёту, а не
  // перечитывать его ник.
  const medal = page.rank <= 3 ? `rank-medal-${page.rank}` : ''

  return (
    <div className={`fade-in page-sheet design-${page.designKey}`}>
      <BackBar onBack={onBack} t={t} />

      <header
        className={`page-hero ${page.backgroundKey ? 'page-hero-bg' : ''}`}
        style={page.backgroundKey ? { backgroundImage: `url(/bg/${page.backgroundKey}.webp)` } : undefined}
      >
        <span className="page-hero-veil" />
        <div className="page-hero-body page-hero-split">
          <div className="page-hero-left">
            {page.showcaseKey && page.showcaseLevel > 0 && (
              <span className={`page-hero-crest hall-badge ${LEVEL_CLS[page.showcaseLevel] ?? ''}`}>
                {BADGE_ICONS[page.showcaseKey] ?? '🏅'}
              </span>
            )}
            <h2 className="page-hero-name">
              {page.name}
              {page.isSponsor && <span className="hall-sponsor-tag">★</span>}
            </h2>
            <span className="page-hero-tag">{page.playerTag}</span>

            {page.clanId !== null ? (
              <button
                className="page-hero-clan"
                onClick={() => { haptic('light'); onOpenClan(page.clanId!) }}
              >🏰 {page.clanName}</button>
            ) : (
              <span className="page-hero-motto">🏰 {page.clanName}</span>
            )}
          </div>

          <div className={`rank-medal ${medal}`}>
            <span className="rank-medal-num">{page.rank}</span>
            <span className="rank-medal-of">{t.hall.outOf} {page.totalPlayers}</span>
          </div>
        </div>
      </header>

      <div className="page-stats">
        <Stat value={fmt(page.seasonFame)} label={t.hallPage.medals} />
        <Stat value={String(page.weeksPlayed)} label={t.hallPage.weeks} />
        <Stat value={fmt(page.bestWeekFame)} label={t.hallPage.bestWeek} />
      </div>

      {page.weeks.length > 1 && (
        <section className="card">
          <div className="card-title">{t.hallPage.weeksChart}</div>
          <WeekBars weeks={page.weeks} t={t} />
        </section>
      )}

      <section className="card">
        <div className="card-title">{t.hallPage.badges}</div>
        {page.badges.length === 0 ? (
          <p className="muted small">{t.hallPage.noBadges}</p>
        ) : (
          <div className="page-badges">
            {page.badges.map(b => (
              <div key={b.key} className={`page-badge ${LEVEL_CLS[b.level] ?? ''}`}>
                <span className="page-badge-icon">{BADGE_ICONS[b.key] ?? '🏅'}</span>
                <span className="page-badge-lvl">{'★'.repeat(b.level)}</span>
                <span className="page-badge-val muted small">{fmt(b.value)}</span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

/**
 * Столбики по неделям.
 *
 * Рисуем своими классами, а не общими чартовыми: те несут наведение и выбор, а
 * здесь график читают, но не трогают — одна лишняя тень, и столбик начинает
 * выглядеть нажимаемым.
 */
function WeekBars({ weeks, t }: { weeks: PageWeek[]; t: Translations }) {
  const max = Math.max(...weeks.map(w => w.fame), 1)
  return (
    <div className="week-bars">
      {weeks.map(w => (
        <div key={w.sectionIndex} className="week-bar-col">
          <span className="week-bar-val">{fmt(w.fame)}</span>
          <div className="week-bar-track">
            <div className="week-bar-fill" style={{ height: `${Math.round((w.fame / max) * 100)}%` }} />
          </div>
          <span className="week-bar-label muted small">{t.hallPage.weekShort}{w.sectionIndex + 1}</span>
        </div>
      ))}
    </div>
  )
}

/** Выбор оформления и девиза. Открыт главе и спонсору своего клана. */
function DesignModal({ page, onClose, onSaved, t }: {
  page: ClanPage; onClose: () => void; onSaved: () => void; t: Translations
}) {
  const [design, setDesign] = useState<ClanDesignKey>(page.designKey)
  const [motto, setMotto] = useState(page.motto ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.setClanPage(page.clanId, { designKey: design, motto })
      hapticNotify('success')
      onSaved()
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError && e.code === 'design_needs_sponsor'
        ? t.hallPage.designLocked
        : t.hallPage.saveError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="card modal-card" onClick={e => e.stopPropagation()}>
        <div className="card-title">{t.hallPage.designTitle}</div>
        <p className="muted small">{t.hallPage.designHint}</p>

        <div className="design-grid">
          {page.availableDesigns.map(k => (
            <button
              key={k}
              className={`design-tile design-${k} ${design === k ? 'design-tile-on' : ''}`}
              disabled={busy}
              onClick={() => { haptic('light'); setDesign(k) }}
            >
              <span className="design-tile-name">{t.hallPage.designs[k]}</span>
            </button>
          ))}
        </div>
        {page.sponsorCount === 0 && <p className="muted small">{t.hallPage.designLocked}</p>}

        <p className="adm-block-title">{t.hallPage.motto}</p>
        <textarea
          className="recruit-note"
          value={motto}
          onChange={e => setMotto(e.target.value)}
          placeholder={t.hallPage.mottoPlaceholder}
          maxLength={120}
          rows={2}
        />

        {error && <p className="form-error small">{error}</p>}

        <div className="recruit-actions">
          <button className="btn" disabled={busy} onClick={save}>
            {busy ? t.hallPage.saving : t.hallPage.save}
          </button>
          <button className="btn-mini" disabled={busy} onClick={onClose}>{t.hall.close}</button>
        </div>
      </div>
    </div>
  )
}

/**
 * Окно «написать клану».
 *
 * Своё, а не то, что на Аллее: там оно получало готовую строку списка, здесь есть
 * только страница. Держать одно на двоих значило бы тянуть в него весь HallClan
 * ради двух полей.
 */
function ClanMessageModal({ clanId, clanName, onClose, t }: {
  clanId: number; clanName: string; onClose: () => void; t: Translations
}) {
  const [text, setText] = useState('')
  const [kind, setKind] = useState<'message' | 'challenge'>('message')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  const send = async () => {
    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.sendClanMessage(clanId, text.trim(), kind)
      hapticNotify('success')
      setSent(true)
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError ? messageError(e.code, t) : t.hall.msgError)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="card modal-card" onClick={e => e.stopPropagation()}>
        <div className="card-title">{t.hall.msgTitle} {clanName}</div>

        {sent ? (
          <>
            <p className="muted small">{t.hall.msgSent}</p>
            <button className="btn" onClick={onClose}>{t.hall.close}</button>
          </>
        ) : (
          <>
            <div className="hall-switch">
              <button
                className={`chart-opt ${kind === 'message' ? 'chart-opt-on' : ''}`}
                onClick={() => { haptic('light'); setKind('message') }}
              >{t.hall.msgKindMessage}</button>
              <button
                className={`chart-opt ${kind === 'challenge' ? 'chart-opt-on' : ''}`}
                onClick={() => { haptic('light'); setKind('challenge') }}
              >{t.hall.msgKindChallenge}</button>
            </div>

            <textarea
              className="recruit-note"
              value={text}
              onChange={e => setText(e.target.value)}
              placeholder={kind === 'challenge' ? t.hall.msgChallengePlaceholder : t.hall.msgPlaceholder}
              maxLength={300}
              rows={4}
            />
            <p className="muted small">{t.hall.msgLimit}</p>

            {error && <p className="form-error small">{error}</p>}

            <div className="recruit-actions">
              <button className="btn" disabled={busy || text.trim().length < 3} onClick={send}>
                {busy ? t.hall.msgSending : t.hall.msgSend}
              </button>
              <button className="btn-mini" disabled={busy} onClick={onClose}>{t.hall.close}</button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}

function Stat({ value, label }: { value: string; label: string }) {
  return (
    <div className="page-stat">
      <span className="page-stat-val">{value}</span>
      <span className="page-stat-label muted small">{label}</span>
    </div>
  )
}

function BackBar({ onBack, t }: { onBack: () => void; t: Translations }) {
  return (
    <button className="page-back" onClick={() => { haptic('light'); onBack() }}>
      ← {t.hallPage.back}
    </button>
  )
}

function Loading({ onBack, t }: { onBack: () => void; t: Translations }) {
  return (
    <div className="page-sheet">
      <BackBar onBack={onBack} t={t} />
      <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
    </div>
  )
}

function Failed({ text, onBack, t }: { text: string; onBack: () => void; t: Translations }) {
  return (
    <div className="page-sheet">
      <BackBar onBack={onBack} t={t} />
      <p className="center muted" style={{ marginTop: 16 }}>{text}</p>
    </div>
  )
}

function messageError(code: string, t: Translations): string {
  switch (code) {
    case 'too_soon': return t.hall.errTooSoon
    case 'daily_limit': return t.hall.errDailyLimit
    case 'target_opted_out': return t.hall.errOptedOut
    case 'target_has_no_chat': return t.hall.errNoChat
    case 'not_allowed': return t.hall.errNotAllowed
    case 'needs_sponsor': return t.hall.errNeedsSponsor
    default: return t.hall.msgError
  }
}
