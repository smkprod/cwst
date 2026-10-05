import { useT } from '../lib/i18n'
import type { IconName } from './ui/Icon'
import { IconTile, SectionHead } from './ui/Section'

/**
 * Короткий экскурс: человек без подключённого клана не видит основной экран войны,
 * поэтому иначе он просто не узнает, ради чего просить главу подключить бота.
 */
export function BotTourCard() {
  const { t } = useT()

  const items = [
    { icon: 'swords' as IconName, title: t.clanless.tour.warTitle, text: t.clanless.tour.warText },
    { icon: 'radar' as IconName, title: t.clanless.tour.forecastTitle, text: t.clanless.tour.forecastText },
    { icon: 'bell' as IconName, title: t.clanless.tour.remindTitle, text: t.clanless.tour.remindText },
    { icon: 'trophy' as IconName, title: t.clanless.tour.ratingTitle, text: t.clanless.tour.ratingText },
    { icon: 'cards' as IconName, title: t.clanless.tour.decksTitle, text: t.clanless.tour.decksText },
    { icon: 'medal' as IconName, title: t.clanless.tour.tournamentTitle, text: t.clanless.tour.tournamentText },
  ]

  return (
    <section className="card">
      <SectionHead icon="bot" tone="violet" title={t.clanless.tour.title} />
      <ul className="tour-list">
        {items.map(i => (
          <li key={i.title} className="tour-row">
            <IconTile name={i.icon} tone="gray" size={30} />
            <div className="tour-text">
              <span className="tour-title">{i.title}</span>
              <span className="muted small">{i.text}</span>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
