import { useT } from '../lib/i18n'

/**
 * Что умеет бот. Все функции открыты всем: тарифа больше нет, платная модель —
 * спонсорство, а оно про видимость, а не про доступ к функциям.
 */
export function AboutCard() {
  const { t } = useT()

  return (
    <section className="card about-card">
      <div className="card-title">{t.about.title}</div>

      <p className="muted small" style={{ margin: '8px 0 12px' }}>{t.about.intro}</p>

      <ul className="about-list">
        {t.about.features.map(f => (
          <li key={f.title} className="about-item">
            <span className="about-icon">{f.icon}</span>
            <div className="about-text">
              <span className="about-title">{f.title}</span>
              <span className="muted small">{f.desc}</span>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
