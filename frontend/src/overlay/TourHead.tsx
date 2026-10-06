import type { ReactNode } from 'react'
import type { Translations } from '../lib/i18n'
import type { Tournament } from '../types'
import { Icon, type IconName } from '../components/ui/Icon'
import { boLabel } from './data'

/** Плашка-«чип» виджета: та же форма, что Chip в приложении, но крупнее — под эфир. */
export function OvChip({ icon, tone = 'gray', children }: { icon?: IconName; tone?: string; children: ReactNode }) {
  return <span className={`ov-chip ov-chip-${tone}`}>{icon && <Icon name={icon} size={15} />}{children}</span>
}

export function statusChip(d: Tournament, t: Translations) {
  switch (d.status) {
    case 'registrationOpen': return <OvChip icon="users" tone="green">{t.tournament.statusOpen}</OvChip>
    case 'bracketReady': return <OvChip icon="bracket" tone="blue">{t.tournament.statusBracketReady}</OvChip>
    case 'inProgress': return <OvChip tone="live"><span className="ov-live-dot" />{t.overlay.live}</OvChip>
    case 'completed': return <OvChip icon="crown" tone="gold">{t.tournament.statusCompleted}</OvChip>
    default: return <OvChip tone="gray">{t.tournament.statusCancelled}</OvChip>
  }
}

/** Шапка турнирного виджета: название и условия — режим, формат, число участников. */
export function TourHead({ data, t, compact = false }: { data: Tournament; t: Translations; compact?: boolean }) {
  return (
    <div className={`ov-head ${compact ? 'ov-head-compact' : ''}`}>
      <span className="ov-head-tile"><Icon name="trophy" size={compact ? 22 : 28} /></span>
      <div className="ov-head-text">
        <div className="ov-head-title">{data.name}</div>
        <div className="ov-head-chips">
          {statusChip(data, t)}
          {data.gameMode && <OvChip icon="swords" tone="violet">{t.duel.modes[data.gameMode] ?? data.gameMode}</OvChip>}
          <OvChip tone="gray">{boLabel(data.bestOf)}</OvChip>
          {!compact && <OvChip icon="users" tone="gray">{data.participants.length}/{data.maxParticipants}</OvChip>}
        </div>
      </div>
      {!compact && data.prizeInfo && (
        <div className="ov-head-prize"><Icon name="gift" size={18} /> <span>{data.prizeInfo}</span></div>
      )}
    </div>
  )
}
