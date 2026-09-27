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
import { usePlusSheet } from '../lib/plusSheet'

export type MoreSection = 'recruit' | 'about' | 'worldTop'
type Section = MoreSection

interface Props {
  /** Админ группы или лидер клана — только им есть что настраивать в уведомлениях. */
  canManage: boolean
  /** Глава или соруководитель видит биржу кандидатов. */
  isLeader: boolean
  onOpenNotifications: () => void
  /** Сразу открыть раздел — по ссылке из бота («/meta» ведёт в мировой топ). */
  initialSection?: MoreSection | null
}

/**
 * «Ещё» — всё, что нужно изредка: настройки, справка, турниры, биржа.
 *
 * Раньше это было размазано: язык отдельной строкой над контентом, уведомления
 * за шестерёнкой в шапке войны, турниры и биржа — вкладками в баре наравне с
 * ежедневным экраном. В итоге бар доходил до семи пунктов, а найти настройки
 * можно было только случайно.
 */
export function MoreView({ canManage, isLeader, onOpenNotifications, initialSection = null }: Props) {
  const { t } = useT()
  const [section, setSection] = useState<Section | null>(initialSection)
  const openPlus = usePlusSheet()

  const open = (next: Section) => { haptic('light'); setSection(next) }
  const back = () => { haptic('light'); setSection(null) }

  if (section) {
    return (
      <div className="fade-in">
        <button className="btn-mini more-back" onClick={back}>← {t.more.back}</button>
        {section === 'recruit' && (isLeader ? <RecruitBoard /> : <RecruitToggle />)}
        {section === 'about' && <AboutCard />}
        {section === 'worldTop' && <WorldTopView />}
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

        <button className="more-row" onClick={openPlus}>
          <span className="more-row-icon">💎</span>
          <span className="more-row-text">
            <span className="more-row-title">{t.more.plus}</span>
            <span className="muted small">{t.more.plusHint}</span>
          </span>
          <span className="more-row-arrow">›</span>
        </button>

        <button className="more-row" onClick={() => open('worldTop')}>
          <span className="more-row-icon">🌍</span>
          <span className="more-row-text">
            <span className="more-row-title">{t.more.worldTop}</span>
            <span className="muted small">{t.more.worldTopHint}</span>
          </span>
          <span className="more-row-arrow">›</span>
        </button>

        <button className="more-row" onClick={() => open('recruit')}>
          <span className="more-row-icon">👥</span>
          <span className="more-row-text">
            <span className="more-row-title">{isLeader ? t.more.recruitBoard : t.more.recruitMe}</span>
            <span className="muted small">{isLeader ? t.more.recruitBoardHint : t.more.recruitMeHint}</span>
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
