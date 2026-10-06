import { useEffect, useState, type ReactNode } from 'react'
import type { Translations } from '../lib/i18n'
import type { Challenge, OverlayLeague, Tournament } from '../types'
import { Icon } from '../components/ui/Icon'
import { usePoll } from './data'
import { BracketWidget } from './BracketWidget'
import { MatchWidget } from './MatchWidget'
import { ListWidget } from './ListWidget'
import { LeagueWidget } from './LeagueWidget'
import { ChallengeWidget } from './ChallengeWidget'

/**
 * Размер, под который свёрстан каждый виджет. Совпадает с рекомендацией в Студии;
 * если в OBS задали другой — сцена масштабируется целиком, с сохранением пропорций.
 */
const SIZES = {
  bracket: [1280, 720],
  match: [900, 220],
  list: [520, 720],
  league: [520, 720],
  challenge: [520, 720],
} as const

type Widget = keyof typeof SIZES

const isWidget = (w: string | null): w is Widget => w !== null && w in SIZES

export function Overlay({ widget, k, t }: { widget: string | null; k: string | null; t: Translations }) {
  // Битую ссылку показываем той же плашкой, что и неизвестный ключ: блогеру в обоих
  // случаях нужно одно — заново скопировать ссылку из Студии.
  if (!isWidget(widget) || !k || !/^[a-z0-9]{8,64}$/i.test(k)) return <Missing t={t} />
  return <Live widget={widget} k={k} t={t} />
}

function Live({ widget, k, t }: { widget: Widget; k: string; t: Translations }) {
  const scope = widget === 'league' || widget === 'challenge' ? 's' : 't'
  const path = scope === 't'
    ? `/api/overlay/t/${encodeURIComponent(k)}`
    : `/api/overlay/s/${encodeURIComponent(k)}/${widget}${widget === 'league' ? '?limit=10' : ''}`
  const poll = usePoll<unknown>(path)

  // Плашка «не найден» — вне масштабируемой сцены: это служебное сообщение для
  // блогера в углу источника, а не часть оформления эфира.
  if (poll.kind === 'missing') return <Missing t={t} />
  // Пока первый ответ не пришёл — пусто, а не крутилка: в эфире лучше ничего,
  // чем мигающий индикатор поверх игры.
  if (poll.kind === 'loading') return null

  const [w, h] = SIZES[widget]
  return (
    <Stage w={w} h={h}>
      {widget === 'bracket' && <BracketWidget data={poll.data as Tournament} t={t} />}
      {widget === 'match' && <MatchWidget data={poll.data as Tournament} t={t} />}
      {widget === 'list' && <ListWidget data={poll.data as Tournament} t={t} />}
      {widget === 'league' && <LeagueWidget data={poll.data as OverlayLeague} t={t} />}
      {widget === 'challenge' && <ChallengeWidget data={poll.data as Challenge} t={t} />}
    </Stage>
  )
}

/**
 * Сцена фиксированного размера, вписанная в окно. Верстаем в пикселях под
 * рекомендованный размер, а под фактический — масштабируем: так шрифт в эфире
 * одинаково читается и в 720p, и в 1440p, и никакая строка не переносится иначе.
 */
function Stage({ w, h, children }: { w: number; h: number; children: ReactNode }) {
  const [scale, setScale] = useState(() => fitScale(w, h))
  useEffect(() => {
    const onResize = () => setScale(fitScale(w, h))
    onResize()
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [w, h])
  return (
    <div className="ov-stage" style={{ width: w, height: h, transform: `translate(-50%, -50%) scale(${scale})` }}>
      {children}
    </div>
  )
}

const fitScale = (w: number, h: number) => Math.min(window.innerWidth / w, window.innerHeight / h) || 1

function Missing({ t }: { t: Translations }) {
  return (
    <div className="ov-missing">
      <Icon name="link" size={18} /> {t.overlay.notFound}
    </div>
  )
}
