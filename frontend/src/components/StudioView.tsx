import { useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from '../lib/api'
import type { Studio, StudioTournament, TournamentStatus } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'
import { PERSONAL_WIDGETS, TOURNAMENT_WIDGETS } from '../lib/overlayLinks'
import { TournamentForm } from './TournamentForm'
import { TournamentDetail } from './TournamentDetail'
import { ObsSteps, WidgetLinks } from './WidgetLinks'
import { StudioChallenges } from './StudioChallenges'
import { Icon, type IconName } from './ui/Icon'
import { Chip, IconTile, SectionHead, type Tone } from './ui/Section'
import { InfoButton } from './ui/Info'

type View = { kind: 'home' } | { kind: 'create' } | { kind: 'detail'; id: number }

type LoadState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'denied' }
  | { kind: 'ready'; data: Studio }

/**
 * Иконки и пометка «скоро» у идей — здесь, а не в переводах: это свойства самой
 * идеи, а не текста, и в трёх языках они обязаны совпадать. Порядок — как в t.studio.ideas.
 */
const IDEAS: { icon: IconName; tone: Tone; soon: boolean }[] = [
  { icon: 'crown', tone: 'gold', soon: true },
  { icon: 'flame', tone: 'orange', soon: true },
  { icon: 'sparkles', tone: 'violet', soon: false },
  { icon: 'clock', tone: 'blue', soon: true },
  { icon: 'users', tone: 'green', soon: false },
]

/**
 * «Студия» блогера: свои челленджи, турниры и виджеты для OBS в одном месте.
 * Вкладкой в баре её больше нет — открывается из «Ещё» и из меню панели.
 *
 * Турниры здесь те же, что во вкладке «Турнир», — форма и карточка переиспользуются
 * целиком. Своё у Студии только то, что нужно на трансляции: ссылки на виджеты,
 * личный ключ и подсказки, что вообще можно устроить со зрителями.
 */
export function StudioView() {
  const { t } = useT()
  const [view, setView] = useState<View>({ kind: 'home' })
  const [state, setState] = useState<LoadState>({ kind: 'loading' })

  const load = () => {
    api.getStudio()
      .then(data => setState({ kind: 'ready', data }))
      .catch(e => setState(e instanceof ApiError && e.status === 403 ? { kind: 'denied' } : { kind: 'error' }))
  }

  // Перечитываем при каждом возврате на главный экран: в карточке турнира могли
  // построить сетку или отменить его, и статусы в списке должны это отразить.
  useEffect(() => {
    if (view.kind === 'home') load()
  }, [view.kind])

  const go = (v: View) => { haptic('light'); setView(v) }
  const home = () => go({ kind: 'home' })

  if (view.kind === 'create') {
    return (
      <div className="fade-in">
        <button className="btn-back" onClick={home}><Icon name="chevronLeft" size={15} /> {t.tournament.back}</button>
        <TournamentForm mode="create" onSaved={tr => go({ kind: 'detail', id: tr.id })} onCancel={home} />
      </div>
    )
  }

  if (view.kind === 'detail') {
    return (
      <div className="fade-in">
        <TournamentDetail tournamentId={view.id} onBack={home} onCancelled={home} />
      </div>
    )
  }

  return (
    <div className="fade-in st-page">
      <section className="st-hero">
        <div className="st-hero-top">
          <IconTile name="rocket" tone="orange" size={48} />
          <div className="st-hero-text">
            <h1 className="st-hero-title">{t.studio.title}</h1>
            <p className="st-hero-sub">{t.studio.subtitle}</p>
          </div>
        </div>
        <button className="ui-btn ui-btn-warm st-hero-btn" onClick={() => go({ kind: 'create' })}>
          <Icon name="plus" size={17} /> {t.studio.createBtn}
        </button>
      </section>

      {state.kind === 'loading' && <div className="center"><div className="spinner" /></div>}
      {state.kind === 'error' && (
        <div className="center">
          <p className="muted">{t.studio.error}</p>
          <button className="btn" onClick={() => { setState({ kind: 'loading' }); load() }}>
            <Icon name="refresh" size={16} /> {t.retry}
          </button>
        </div>
      )}
      {state.kind === 'denied' && <p className="center muted">{t.studio.notCreator}</p>}

      {state.kind === 'ready' && (
        <StudioHome
          data={state.data}
          onOpen={id => go({ kind: 'detail', id })}
          onRotated={data => setState({ kind: 'ready', data })}
          t={t}
        />
      )}
    </div>
  )
}

function StudioHome({ data, onOpen, onRotated, t }: {
  data: Studio
  onOpen: (id: number) => void
  /** Свежая Студия после смены ключа или правки челленджа. */
  onRotated: (s: Studio) => void
  t: Translations
}) {
  // Отменённые турниры в выборе виджетов не нужны: показывать их в эфире нечего.
  const live = useMemo(() => data.tournaments.filter(x => x.tournament.status !== 'cancelled'), [data])
  const [picked, setPicked] = useState<number | null>(() => defaultPick(live))
  const widgetsRef = useRef<HTMLDivElement>(null)

  // Выбранный турнир мог пропасть из списка (отменили) — тогда берём следующий подходящий
  const current = live.find(x => x.tournament.id === picked) ?? live.find(x => x.tournament.id === defaultPick(live)) ?? null

  const showWidgets = (id: number) => {
    haptic('light')
    setPicked(id)
    widgetsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <>
      {/* Челленджи первыми: их устраивают на каждый стрим, турнир — от случая к случаю */}
      <StudioChallenges items={data.challenges ?? []} onChanged={onRotated} t={t} />

      <section className="card st-card">
        <SectionHead icon="trophy" tone="gold" title={t.studio.myTitle} aside={data.tournaments.length || undefined} />
        {data.tournaments.length === 0 && <p className="muted small st-empty">{t.studio.myEmpty}</p>}
        <div className="ui-list">
          {data.tournaments.map(x => (
            <TournamentRow key={x.tournament.id} item={x} t={t}
              onOpen={() => onOpen(x.tournament.id)}
              onWidgets={x.tournament.status === 'cancelled' ? undefined : () => showWidgets(x.tournament.id)} />
          ))}
        </div>
      </section>

      <section className="card st-card" ref={widgetsRef}>
        <SectionHead icon="image" tone="orange" title={t.studio.widgetsTitle}
          infoTitle={t.studio.obsInfoTitle} info={<ObsSteps steps={t.studio.obsSteps} />} />

        <div className="st-sub">
          <span className="st-sub-title"><Icon name="trophy" size={14} /> {t.studio.tournamentWidgets}</span>
        </div>
        {current ? (
          <>
            {live.length > 1 && (
              <select className="rating-select tournament-select st-pick" value={current.tournament.id}
                onChange={e => { haptic('light'); setPicked(Number(e.target.value)) }}>
                {live.map(x => <option key={x.tournament.id} value={x.tournament.id}>{x.tournament.name}</option>)}
              </select>
            )}
            {live.length === 1 && <p className="st-pick-one">{current.tournament.name}</p>}
            <WidgetLinks widgets={TOURNAMENT_WIDGETS} overlayKey={current.overlayKey} />
          </>
        ) : (
          <p className="muted small st-empty">{t.studio.noTournamentWidgets}</p>
        )}

        <div className="st-sub">
          <span className="st-sub-title"><Icon name="globe" size={14} /> {t.studio.globalWidgets}</span>
        </div>
        <WidgetLinks widgets={PERSONAL_WIDGETS} overlayKey={data.overlayKey} />

        <KeyRow overlayKey={data.overlayKey} onRotated={onRotated} t={t} />
      </section>

      <Ideas t={t} />
    </>
  )
}

/** Какой турнир показать в виджетах сразу: идущий, потом открытый, потом любой. */
function defaultPick(list: StudioTournament[]): number | null {
  const by = (s: TournamentStatus[]) => list.find(x => s.includes(x.tournament.status))
  return (by(['inProgress']) ?? by(['bracketReady', 'registrationOpen']) ?? list[0])?.tournament.id ?? null
}

function statusLook(s: TournamentStatus, t: Translations): { label: string; tone: Tone; icon: IconName } {
  switch (s) {
    case 'registrationOpen': return { label: t.tournament.statusOpen, tone: 'green', icon: 'users' }
    case 'bracketReady': return { label: t.tournament.statusBracketReady, tone: 'blue', icon: 'bracket' }
    case 'inProgress': return { label: t.tournament.statusInProgress, tone: 'orange', icon: 'dot' }
    case 'completed': return { label: t.tournament.statusCompleted, tone: 'gold', icon: 'crown' }
    default: return { label: t.tournament.statusCancelled, tone: 'gray', icon: 'xCircle' }
  }
}

function TournamentRow({ item, onOpen, onWidgets, t }: {
  item: StudioTournament
  onOpen: () => void
  onWidgets?: () => void
  t: Translations
}) {
  const tr = item.tournament
  const st = statusLook(tr.status, t)
  return (
    <div className={`ui-row st-trow ${tr.status === 'cancelled' ? 'st-trow-off' : ''}`}>
      <button className="st-trow-main" onClick={onOpen}>
        <span className="st-trow-name">{tr.name}</span>
        <span className="st-trow-chips">
          <Chip icon={st.icon} tone={st.tone}>{st.label}</Chip>
          {tr.gameMode && <Chip icon="swords" tone="violet">{t.duel.modes[tr.gameMode] ?? tr.gameMode}</Chip>}
          {tr.mode === 'duo' && <Chip tone="gray">2×2</Chip>}
          <Chip icon="user" tone="gray">
            {t.studio.participants.replace('{n}', String(tr.participantCount)).replace('{max}', String(tr.maxParticipants))}
          </Chip>
        </span>
      </button>
      {onWidgets && (
        <button className="st-trow-obs" onClick={onWidgets} aria-label={t.studio.widgetsTitle}>
          <Icon name="image" size={15} /> {t.studio.widgetsBtn}
        </button>
      )}
      <button className="st-trow-chev" onClick={onOpen} aria-label={tr.name}>
        <Icon name="chevronRight" size={18} />
      </button>
    </div>
  )
}

/**
 * Личный ключ: показываем только края — целиком он не нужен глазам, а на записи
 * экрана во время стрима полный ключ дал бы зрителям чужие виджеты.
 */
function KeyRow({ overlayKey, onRotated, t }: { overlayKey: string; onRotated: (s: Studio) => void; t: Translations }) {
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  const rotate = async () => {
    // Смена ключа ломает ссылки, уже вставленные в OBS, — поэтому второй тап
    if (!confirm) { haptic('medium'); setConfirm(true); return }
    haptic('medium')
    setBusy(true)
    try {
      const fresh = await api.rotateStudioKey()
      hapticNotify('success')
      setDone(true)
      onRotated(fresh)
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  return (
    <div className="st-key">
      <div className="st-key-top">
        <Icon name="key" size={16} className="st-key-ic" />
        <span className="st-key-label">{t.studio.keyTitle}</span>
        <InfoButton size={18} title={t.studio.keyTitle}>{t.studio.keyHint}</InfoButton>
        <code className="st-key-val">{overlayKey.slice(0, 4)}••••{overlayKey.slice(-4)}</code>
      </div>
      {done && <p className="small st-key-done"><Icon name="checkCircle" size={14} /> {t.studio.rotated}</p>}
      <div className="st-key-actions">
        <button className={`btn-mini ${confirm ? 'btn-mini-danger' : ''}`} disabled={busy} onClick={rotate}>
          <Icon name={confirm ? 'alert' : 'refresh'} size={14} /> {confirm ? t.studio.rotateConfirm : t.studio.rotate}
        </button>
        {confirm && <button className="btn-mini" disabled={busy} onClick={() => setConfirm(false)}>{t.tournament.cancelForm}</button>}
      </div>
    </div>
  )
}

/** Идеи для стрима — свёрнуты: это вдохновение, а не то, что нужно каждый заход. */
function Ideas({ t }: { t: Translations }) {
  const [open, setOpen] = useState(false)
  return (
    <section className="card st-card">
      <button className="st-ideas-head" onClick={() => { haptic('light'); setOpen(o => !o) }} aria-expanded={open}>
        <IconTile name="sparkles" tone="violet" />
        <span className="ui-head-title">{t.studio.ideasTitle}</span>
        <Icon name={open ? 'chevronUp' : 'chevronDown'} size={18} className="st-ideas-chev" />
      </button>
      {open && (
        <div className="st-ideas">
          {t.studio.ideas.map((idea, i) => {
            const look = IDEAS[i] ?? IDEAS[0]
            return (
              <div key={i} className="st-idea">
                <IconTile name={look.icon} tone={look.tone} size={34} />
                <div className="st-idea-body">
                  <div className="st-idea-top">
                    <span className="st-idea-title">{idea.title}</span>
                    {look.soon
                      ? <Chip icon="hourglass" tone="gray">{t.studio.soon}</Chip>
                      : <Chip icon="check" tone="green">{t.studio.now}</Chip>}
                  </div>
                  <p className="muted small">{idea.text}</p>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}
