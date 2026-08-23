import { useState } from 'react'
import type { Plan } from '../types'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'
import { AboutCard } from './AboutCard'
import { CommunityCard } from './CommunityCard'
import { InviteCard } from './InviteCard'
import { RecruitToggle } from './RecruitToggle'
import { RecruitBoard } from './RecruitBoard'
import { TournamentView } from './TournamentView'

type Section = 'tournaments' | 'recruit' | 'about'

interface Props {
  plan: Plan
  /** Админ группы или лидер клана — только им есть что настраивать в уведомлениях. */
  canManage: boolean
  /** Лидер клана на Pro видит биржу кандидатов. */
  isProLeader: boolean
  onOpenNotifications: () => void
}

/**
 * «Ещё» — всё, что нужно изредка: настройки, справка, турниры, биржа.
 *
 * Раньше это было размазано: язык отдельной строкой над контентом, уведомления
 * за шестерёнкой в шапке войны, турниры и биржа — вкладками в баре наравне с
 * ежедневным экраном. В итоге бар доходил до семи пунктов, а найти настройки
 * можно было только случайно.
 */
export function MoreView({ plan, canManage, isProLeader, onOpenNotifications }: Props) {
  const { t } = useT()
  const [section, setSection] = useState<Section | null>(null)

  const open = (next: Section) => { haptic('light'); setSection(next) }
  const back = () => { haptic('light'); setSection(null) }

  if (section) {
    return (
      <div className="fade-in">
        <button className="btn-mini more-back" onClick={back}>← {t.more.back}</button>
        {section === 'tournaments' && <TournamentView />}
        {section === 'recruit' && (isProLeader ? <RecruitBoard /> : <RecruitToggle />)}
        {section === 'about' && <AboutCard plan={plan} />}
      </div>
    )
  }

  return (
    <div className="fade-in">
      <section className="card">
        <div className="card-title">{t.more.settingsTitle}</div>

        <div className="more-setting">
          <span className="more-setting-label">🌐 {t.more.language}</span>
          <LangSwitcher />
        </div>

        {canManage && (
          <button className="more-row" onClick={() => { haptic('light'); onOpenNotifications() }}>
            <span className="more-row-icon">🔔</span>
            <span className="more-row-text">
              <span className="more-row-title">{t.more.notifications}</span>
              <span className="muted small">{t.more.notificationsHint}</span>
            </span>
            <span className="more-row-arrow">›</span>
          </button>
        )}
      </section>

      <section className="card" style={{ marginTop: 10 }}>
        <div className="card-title">{t.more.sectionsTitle}</div>

        <button className="more-row" onClick={() => open('tournaments')}>
          <span className="more-row-icon">🥇</span>
          <span className="more-row-text">
            <span className="more-row-title">{t.more.tournaments}</span>
            <span className="muted small">{t.more.tournamentsHint}</span>
          </span>
          <span className="more-row-arrow">›</span>
        </button>

        <button className="more-row" onClick={() => open('recruit')}>
          <span className="more-row-icon">👥</span>
          <span className="more-row-text">
            <span className="more-row-title">{isProLeader ? t.more.recruitBoard : t.more.recruitMe}</span>
            <span className="muted small">{isProLeader ? t.more.recruitBoardHint : t.more.recruitMeHint}</span>
          </span>
          <span className="more-row-arrow">›</span>
        </button>

        <button className="more-row" onClick={() => open('about')}>
          <span className="more-row-icon">ℹ️</span>
          <span className="more-row-text">
            <span className="more-row-title">{t.more.about}</span>
            <span className="muted small">{t.more.aboutHint}</span>
          </span>
          <span className="more-row-arrow">›</span>
        </button>
      </section>

      <div style={{ height: 10 }} />
      <InviteCard />
      <div style={{ height: 10 }} />
      <CommunityCard />
    </div>
  )
}
