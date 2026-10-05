import { useT } from '../lib/i18n'
import type { IconName } from './ui/Icon'
import { IconTile, SectionHead } from './ui/Section'

/** Значки функций в переводах остались эмодзи — рисуем вместо них иконки. */
const FEATURE_ICONS: Record<string, IconName> = {
  '\u{2694}': 'swords', '\u{1F3C1}': 'flag', '\u{1F3C6}': 'trophy', '\u{1F52E}': 'radar', '\u{1F4C5}': 'calendar', '\u{1F4DC}': 'history',
  '\u{1F449}': 'megaphone', '\u{23F0}': 'clock', '\u{1F4CA}': 'chart', '\u{1F50D}': 'search', '\u{1F9ED}': 'shieldCheck', '\u{1F305}': 'sun',
}
const featureIcon = (emoji: string): IconName => FEATURE_ICONS[emoji.replace(/\uFE0F/g, '')] ?? 'sparkles'

/**
 * Что умеет бот. Все функции открыты всем: тарифа больше нет, платная модель —
 * спонсорство, а оно про видимость, а не про доступ к функциям.
 */
export function AboutCard() {
  const { t } = useT()

  return (
    <section className="card about-card">
      <SectionHead icon="info" tone="violet" title={t.about.title.replace(/^\u2139\uFE0F?\s*/, '')} info={t.about.intro} />

      <ul className="about-list">
        {t.about.features.map(f => (
          <li key={f.title} className="about-item">
            <IconTile name={featureIcon(f.icon)} tone="gray" size={30} />
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
