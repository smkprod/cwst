import type { Plan } from '../types'
import { useT } from '../lib/i18n'

export function AboutCard({ plan }: { plan: Plan }) {
  const { t } = useT()
  const isPro = plan === 'pro'

  return (
    <section className="card about-card">
      <div className="card-title-row">
        <div className="card-title">{t.about.title}</div>
        <span className={isPro ? 'pro-chip' : 'free-chip'}>{isPro ? 'PRO' : 'FREE'}</span>
      </div>

      <p className="muted small" style={{ margin: '0 0 12px' }}>{t.about.intro}</p>

      <ul className="about-list">
        {t.about.features.map(f => (
          <li key={f.title} className={`about-item ${f.pro && !isPro ? 'about-locked' : ''}`}>
            <span className="about-icon">{f.icon}</span>
            <div className="about-text">
              <span className="about-title">
                {f.title}
                {f.pro && <span className="about-tag">{isPro ? '✓ Pro' : '🔒 Pro'}</span>}
              </span>
              <span className="muted small">{f.desc}</span>
            </div>
          </li>
        ))}
      </ul>

      {!isPro && (
        <p className="muted small about-upsell">{t.about.upsell}</p>
      )}
    </section>
  )
}
