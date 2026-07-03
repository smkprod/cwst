import type { ClanStatus } from '../types'
import { useT } from '../lib/i18n'

/**
 * Первое, что видит игрок в военный день, если сам ещё не доиграл: личный
 * счёт колод и сколько времени осталось. Персональная релевантность +
 * страх потери («не теряй медали») цепляют сильнее общей статистики.
 */
export function MyActionBanner({ status }: { status: ClanStatus }) {
  const { t } = useT()

  if (status.periodType === 'training' || !status.myPlayerTag) return null
  const me = status.players.find(p =>
    p.playerTag.toUpperCase() === status.myPlayerTag!.toUpperCase())
  if (!me || me.decksUsedToday >= 4) return null

  const endsAt = new Date(status.dayEndsAtUtc)
  const msLeft = endsAt.getTime() - Date.now()
  const hours = Math.max(0, Math.floor(msLeft / 3_600_000))
  const mins = Math.max(0, Math.floor((msLeft % 3_600_000) / 60_000))

  return (
    <section className="card my-action-banner">
      <div className="my-action-row">
        <span className="my-action-decks">
          🔥 {t.actionBanner.played} <b>{me.decksUsedToday}/4</b> {t.actionBanner.decks}
        </span>
        {msLeft > 0 && (
          <span className="my-action-time muted small">
            ⏳ {t.actionBanner.left} ~{hours > 0 ? `${hours} ${t.header.h} ` : ''}{mins > 0 || hours === 0 ? `${mins}′` : ''}
          </span>
        )}
      </div>
      <p className="muted small my-action-cta">{t.actionBanner.cta}</p>
    </section>
  )
}
