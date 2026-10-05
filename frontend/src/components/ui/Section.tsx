import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import { InfoButton } from './Info'
import { noEmoji } from '../../lib/text'

export type Tone = 'violet' | 'blue' | 'green' | 'gold' | 'red' | 'orange' | 'gray'

/** Иконка в цветной плитке — одна на всё приложение: заголовки, пункты меню, шаги. */
export function IconTile({ name, tone = 'violet', size = 32 }: { name: IconName; tone?: Tone; size?: number }) {
  return (
    <span className={`ui-tile ui-tone-${tone}`} style={{ width: size, height: size }}>
      <Icon name={name} size={Math.round(size * 0.56)} />
    </span>
  )
}

/**
 * Заголовок секции: плитка с иконкой, название, справа — мелочь (счётчик, ссылка)
 * и кнопка «i» с объяснением. Эмодзи из текста заголовка вычищаются сами:
 * переводы долго писались с ними, а иконка теперь стоит отдельно.
 */
export function SectionHead({ icon, title, tone, info, infoTitle, aside, className }: {
  icon: IconName
  title: ReactNode
  tone?: Tone
  /** Объяснение под кнопкой «i». Нет — кнопки нет. */
  info?: ReactNode
  infoTitle?: ReactNode
  aside?: ReactNode
  className?: string
}) {
  const shown = typeof title === 'string' ? noEmoji(title) : title
  return (
    <div className={`ui-head ${className ?? ''}`}>
      <IconTile name={icon} tone={tone} />
      <span className="ui-head-title">{shown}</span>
      {info && <InfoButton title={infoTitle ?? shown}>{info}</InfoButton>}
      {aside !== undefined && <span className="ui-head-aside">{aside}</span>}
    </div>
  )
}

/** Небольшая плашка: «Bo3», «+23», «LIVE». */
export function Chip({ icon, tone = 'gray', children }: { icon?: IconName; tone?: Tone; children: ReactNode }) {
  return (
    <span className={`ui-chip ui-chip-${tone}`}>
      {icon && <Icon name={icon} size={12} />}
      {children}
    </span>
  )
}
