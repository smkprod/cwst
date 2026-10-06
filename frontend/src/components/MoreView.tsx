import { useState } from 'react'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'
import { AboutCard } from './AboutCard'
import { CommunityCard } from './CommunityCard'
import { InviteCard } from './InviteCard'
import { RecruitToggle } from './RecruitToggle'
import { RecruitBoard } from './RecruitBoard'
import { WorldTopView } from './WorldTopView'
import { ChallengeView } from './ChallengeView'
import { DuelView } from './DuelView'
import { TournamentView } from './TournamentView'
import { HallOfFame } from './HallOfFame'
import { PlayerSearchView } from './PlayerSearchView'
import { StudioView } from './StudioView'
import type { AppConfig } from '../types'
import { usePlusSheet } from '../lib/plusSheet'
import { Icon, type IconName } from './ui/Icon'
import { IconTile, SectionHead, type Tone } from './ui/Section'

export type MoreSection = 'recruit' | 'about' | 'worldTop' | 'challenge' | 'duel' | 'tournament' | 'hall' | 'search' | 'studio'
type Section = MoreSection

interface Props {
  /** Админ группы или лидер клана — только им есть что настраивать в уведомлениях. */
  canManage: boolean
  /** Глава или соруководитель видит биржу кандидатов. */
  isLeader: boolean
  onOpenNotifications: () => void
  /** Сразу открыть раздел — по ссылке из бота («/meta» ведёт в мировой топ). */
  initialSection?: MoreSection | null
  /** Аллее нужен конфиг (фоны спонсора) и перечитка после покупки. Нет — пункта нет. */
  config?: AppConfig | null
  onConfigChanged?: () => void
  /** Право блогера (lib/serviceAccess: canUseStudio) — «Студия» живёт здесь, а не в баре. */
  showStudio?: boolean
}

/**
 * «Ещё» — всё, что нужно изредка: настройки, справка, турниры, биржа.
 *
 * Раньше это было размазано: язык отдельной строкой над контентом, уведомления
 * за шестерёнкой в шапке войны, турниры и биржа — вкладками в баре наравне с
 * ежедневным экраном. В итоге бар доходил до семи пунктов, а найти настройки
 * можно было только случайно.
 *
 * «Разделы» — полный список страниц приложения, а не только то, чего нет в баре:
 * состав бара выбирает владелец, и страница, которую он туда не поставил, иначе
 * становилась недостижимой. Открываются они здесь же, с кнопкой «Назад», — даже
 * если такая вкладка в баре есть: так «Ещё» ведёт себя одинаково при любом наборе.
 */
export function MoreView({ canManage, isLeader, onOpenNotifications, initialSection = null, config = null, onConfigChanged, showStudio = false }: Props) {
  const { t } = useT()
  const [section, setSection] = useState<Section | null>(initialSection)
  const openPlus = usePlusSheet()

  // Подстраница открывается с начала, а не с той высоты, до которой пролистали список
  const open = (next: Section) => { haptic('light'); setSection(next); window.scrollTo(0, 0) }
  const back = () => { haptic('light'); setSection(null); window.scrollTo(0, 0) }

  if (section) {
    return (
      <div className="fade-in">
        <button className="btn-mini more-back" onClick={back}><Icon name="chevronLeft" size={15} /> {t.more.back}</button>
        {section === 'recruit' && (isLeader ? <RecruitBoard /> : <RecruitToggle />)}
        {section === 'about' && <AboutCard />}
        {section === 'worldTop' && <WorldTopView />}
        {section === 'challenge' && <ChallengeView />}
        {section === 'duel' && <DuelView />}
        {section === 'tournament' && <TournamentView />}
        {section === 'hall' && onConfigChanged && <HallOfFame config={config} onConfigChanged={onConfigChanged} />}
        {section === 'search' && <PlayerSearchView />}
        {section === 'studio' && showStudio && <StudioView />}
      </div>
    )
  }

  return (
    <div className="fade-in">
      <section className="card">
        <SectionHead icon="menu" tone="violet" title={t.more.sectionsTitle} />

        {/* Студия первой: из бара её убрали, а блогеру она нужнее остального списка */}
        {showStudio && (
          <MoreRow icon="rocket" tone="orange" title={t.more.studio} hint={t.more.studioHint} onClick={() => open('studio')} />
        )}

        <MoreRow icon="ticket" tone="green" title={t.more.challenge} hint={t.more.challengeHint} onClick={() => open('challenge')} />

        <MoreRow icon="swords" tone="red" title={t.more.duel} hint={t.more.duelHint} onClick={() => open('duel')} />

        <MoreRow icon="trophy" tone="gold" title={t.more.tournaments} hint={t.more.tournamentsHint} onClick={() => open('tournament')} />

        {onConfigChanged && (
          <MoreRow icon="columns" tone="gold" title={t.more.hall} hint={t.more.hallHint} onClick={() => open('hall')} />
        )}

        <MoreRow icon="search" tone="blue" title={t.more.search} hint={t.more.searchHint} onClick={() => open('search')} />

        <MoreRow icon="globe" tone="blue" title={t.more.worldTop} hint={t.more.worldTopHint} onClick={() => open('worldTop')} />

        <MoreRow icon="gem" tone="violet" title={t.more.plus} hint={t.more.plusHint} onClick={() => openPlus()} />

        <MoreRow icon="users" tone="green" title={isLeader ? t.more.recruitBoard : t.more.recruitMe} hint={isLeader ? t.more.recruitBoardHint : t.more.recruitMeHint} onClick={() => open('recruit')} />

        <MoreRow icon="info" tone="gray" title={t.more.about} hint={t.more.aboutHint} onClick={() => open('about')} />
      </section>

      <div style={{ height: 10 }} />
      <InviteCard />
      <div style={{ height: 10 }} />
      <CommunityCard />

      {/* Настройки — в самом низу: их трогают раз, а список разделов нужен каждый заход */}
      <section className="card" style={{ marginTop: 10 }}>
        <SectionHead icon="sliders" tone="gray" title={t.more.settingsTitle} />

        <div className="more-setting">
          <span className="more-setting-label mx-more-label"><IconTile name="language" tone="blue" size={30} /> {t.more.language}</span>
          <LangSwitcher />
        </div>

        {canManage && (
          <MoreRow icon="bell" tone="orange" title={t.more.notifications} hint={t.more.notificationsHint} onClick={() => { haptic('light'); onOpenNotifications() }} />
        )}
      </section>
    </div>
  )
}

/** Пункт меню «Ещё»: плитка с иконкой, название с подписью и шеврон. */
function MoreRow({ icon, tone, title, hint, onClick }: {
  icon: IconName; tone: Tone; title: string; hint: string; onClick: () => void
}) {
  return (
    <button className="more-row" onClick={onClick}>
      <IconTile name={icon} tone={tone} size={34} />
      <span className="more-row-text">
        <span className="more-row-title">{title}</span>
        <span className="muted small">{hint}</span>
      </span>
      <span className="more-row-arrow"><Icon name="chevronRight" size={18} /></span>
    </button>
  )
}
