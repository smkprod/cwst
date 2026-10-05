import type { SVGProps } from 'react'
import { Icon, type IconName } from '../ui/Icon'

/**
 * Иконки лиги дуэлей — тонкие обёртки над общим набором ui/Icon, чтобы лига
 * выглядела так же, как остальное приложение. Имена оставлены прежними: на них
 * завязаны экран лиги и карточка игрока.
 */
type IconProps = Omit<SVGProps<SVGSVGElement>, 'name'> & { size?: number }

const make = (name: IconName) => {
  const DuelIcon = ({ size = 20, ...rest }: IconProps) => <Icon {...rest} name={name} size={size} />
  return DuelIcon
}

export const IconSwords = make('swords')
export const IconTrophy = make('trophy')
export const IconCrown = make('crown')
export const IconFlame = make('flame')
export const IconTarget = make('target')
export const IconTrendUp = make('trendUp')
export const IconX = make('x')
export const IconCheck = make('check')
export const IconUsers = make('users')
export const IconLink = make('link')
export const IconClipboard = make('clipboard')
export const IconBolt = make('bolt')
export const IconShield = make('shieldCheck')
export const IconClock = make('clock')
export const IconChevron = make('chevronRight')
export const IconSend = make('send')
