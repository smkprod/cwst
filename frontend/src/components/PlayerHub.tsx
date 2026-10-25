import { useEffect, useState, type ReactNode } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { markTrainerSeen } from '../lib/mode'
import { Icon, type IconName } from './ui/Icon'
import { DeckBuilderView } from './DeckBuilderView'
import { WorldTopView } from './WorldTopView'
import { TrainerView } from './Trainer'
import { TipsLibrary } from './TipSheet'
import { TrainerPlanCard } from './TrainerPlanCard'
import { OPEN_TRAINER } from '../lib/trainer'
import { ChallengeView } from './ChallengeView'
import { DuelView } from './DuelView'
import { TournamentView } from './TournamentView'

/**
 * Разделы режима игрока. Раньше всё это лежало в «Ещё» одной строкой среди
 * настроек - и тренажёр с подбором колод, то, ради чего берут Плюс, находили
 * только те, кто туда случайно зашёл.
 */

export type DecksSection = 'pick' | 'meta'
export type PlaySection = 'challenge' | 'duel' | 'tournament'
export type TrainerSection = 'tasks' | 'tips'

function useStored<T extends string>(key: string, initial: T, allowed: readonly T[]): [T, (v: T) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const v = localStorage.getItem(key) as T | null
      return v && allowed.includes(v) ? v : initial
    } catch { return initial }
  })
  const set = (v: T) => {
    setValue(v)
    try { localStorage.setItem(key, v) } catch { /* без памяти - не страшно */ }
  }
  return [value, set]
}

function HubTabs<T extends string>({ items, value, onChange }: {
  items: { id: T; icon: IconName; label: string }[]
  value: T
  onChange: (v: T) => void
}) {
  return (
    <div className="hub-tabs">
      {items.map(x => (
        <button key={x.id} className={`hub-tab ${value === x.id ? 'on' : ''}`}
          onClick={() => { haptic('light'); onChange(x.id) }}>
          <Icon name={x.icon} size={14} /> {x.label}
        </button>
      ))}
    </div>
  )
}

/** Колоды: подбор из своих карт и мета топа - две половины одного вопроса «чем играть». */
export function DecksHub({ initial }: { initial?: DecksSection }) {
  const { t } = useT()
  const [section, setSection] = useStored<DecksSection>('cwst_hub_decks', initial ?? 'pick', ['pick', 'meta'])
  useEffect(() => { if (initial) setSection(initial) }, [initial]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className="fade-in">
      <HubTabs value={section} onChange={setSection} items={[
        { id: 'pick', icon: 'cards', label: t.hub.decksPick },
        { id: 'meta', icon: 'flame', label: t.hub.decksMeta },
      ]} />
      {section === 'pick' ? <DeckBuilderView /> : <WorldTopView />}
    </div>
  )
}

/** Тренажёр: задачи и библиотека фишек - отрабатывать и подсматривать. */
export function TrainerHub({ top }: { top?: ReactNode }) {
  const { t } = useT()
  const [section, setSection] = useStored<TrainerSection>('cwst_hub_trainer', 'tasks', ['tasks', 'tips'])
  // Открыли задачу из «тренер подобрал» или из профиля, уже стоя в тренажёре: он
  // читает задачу при запуске, поэтому запускаем его заново
  const [runKey, setRunKey] = useState(0)
  useEffect(() => { markTrainerSeen() }, [])
  useEffect(() => {
    const go = () => { setSection('tasks'); setRunKey(k => k + 1); window.scrollTo(0, 0) }
    window.addEventListener(OPEN_TRAINER, go)
    return () => window.removeEventListener(OPEN_TRAINER, go)
  }, []) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className="fade-in">
      <HubTabs value={section} onChange={setSection} items={[
        { id: 'tasks', icon: 'target', label: t.hub.trainerTasks },
        { id: 'tips', icon: 'book', label: t.hub.trainerTips },
      ]} />
      {section === 'tasks' ? <>{top}<TrainerPlanCard /><TrainerView key={runKey} /></> : <TipsLibrary />}
    </div>
  )
}

/** Соревнования: челлендж, дуэли и турниры - всё, где играешь против людей за место. */
export function PlayHub({ initial }: { initial?: PlaySection }) {
  const { t } = useT()
  const [section, setSection] = useStored<PlaySection>('cwst_hub_play', initial ?? 'challenge', ['challenge', 'duel', 'tournament'])
  useEffect(() => { if (initial) setSection(initial) }, [initial]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className="fade-in">
      <HubTabs value={section} onChange={setSection} items={[
        { id: 'challenge', icon: 'ticket', label: t.hub.playChallenge },
        { id: 'duel', icon: 'swords', label: t.hub.playDuel },
        { id: 'tournament', icon: 'trophy', label: t.hub.playTournament },
      ]} />
      {section === 'challenge' && <ChallengeView />}
      {section === 'duel' && <DuelView />}
      {section === 'tournament' && <TournamentView />}
    </div>
  )
}
