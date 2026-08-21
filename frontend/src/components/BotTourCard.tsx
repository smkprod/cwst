import { useT } from '../lib/i18n'

/**
 * Короткий экскурс: человек без подключённого клана не видит основной экран войны,
 * поэтому иначе он просто не узнает, ради чего просить главу подключить бота.
 */
export function BotTourCard() {
  const { t } = useT()

  const items = [
    { icon: '⚔️', title: t.clanless.tour.warTitle, text: t.clanless.tour.warText },
    { icon: '🔮', title: t.clanless.tour.forecastTitle, text: t.clanless.tour.forecastText },
    { icon: '🔔', title: t.clanless.tour.remindTitle, text: t.clanless.tour.remindText },
    { icon: '🏆', title: t.clanless.tour.ratingTitle, text: t.clanless.tour.ratingText },
    { icon: '🃏', title: t.clanless.tour.decksTitle, text: t.clanless.tour.decksText },
    { icon: '🥇', title: t.clanless.tour.tournamentTitle, text: t.clanless.tour.tournamentText },
  ]

  return (
    <section className="card">
      <div className="card-title">{t.clanless.tour.title}</div>
      <ul className="tour-list">
        {items.map(i => (
          <li key={i.title} className="tour-row">
            <span className="tour-icon">{i.icon}</span>
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
