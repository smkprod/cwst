import { useT } from '../lib/i18n'
import { RecruitToggle } from './RecruitToggle'

export function ClanlessView() {
  const { t } = useT()
  return (
    <div className="fade-in">
      <div className="center" style={{ paddingBottom: 24 }}>
        <p style={{ fontSize: 40, margin: 0 }}>🏰</p>
        <p><strong>{t.clanless.title}</strong></p>
        <p className="muted small" style={{ maxWidth: 280, textAlign: 'center' }}>
          {t.clanless.hint}
        </p>
      </div>
      <p className="muted small" style={{ textAlign: 'center', marginBottom: 12 }}>
        {t.clanless.recruitHint}
      </p>
      <RecruitToggle />
    </div>
  )
}
