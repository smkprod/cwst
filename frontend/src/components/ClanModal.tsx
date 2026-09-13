import { useEffect, useState } from 'react'
import type { ClanOverview } from '../types'
import { api } from '../lib/api'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { ClanProfileCard } from './ClanProfileCard'

interface Props {
  /** Тег клана — единственное, что нужно знать вызывающему. */
  tag: string
  /** Имя из списка: показываем сразу, пока грузится профиль. */
  name?: string
  onClose: () => void
}

/**
 * Страница чужого клана: профиль и журнал войн.
 *
 * Раньше журнал соперника открывался только из гонки недели и показывал одни
 * прошлые войны. Но человек, ткнувший в клан, хочет понять, что это за клан:
 * сколько народу, какой порог входа, чем живёт. Поэтому здесь сначала профиль,
 * а войны под ним.
 *
 * Журнал войн грузит сам ClanProfileCard — тот же компонент, что показывается при
 * поиске клана. Собственная загрузка журнала здесь дала бы второй заголовок
 * «Прошлые войны» и второй запрос за теми же данными.
 */
export function ClanModal({ tag, name, onClose }: Props) {
  const [overview, setOverview] = useState<ClanOverview | null>(null)
  const [failed, setFailed] = useState(false)
  const { t } = useT()

  useEffect(() => {
    const prev = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = prev }
  }, [])

  useEffect(() => {
    let alive = true
    setOverview(null)
    setFailed(false)

    api.getClanOverview(tag)
      .then(o => { if (alive) setOverview(o) })
      .catch(() => { if (alive) setFailed(true) })

    return () => { alive = false }
  }, [tag])

  const close = () => {
    haptic('light')
    onClose()
  }

  return (
    <div className="modal-backdrop" onClick={close}>
      <div className="modal-sheet fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="modal-grip" />

        <div className="modal-head">
          {/* Пока профиль грузится — название в шапке единственное, что есть.
              Когда пришёл, шапка его не повторяет: карточка ниже показывает и имя,
              и тег, и два одинаковых заголовка подряд выглядят как сбой. */}
          <div className="modal-title-wrap">
            {overview === null && (
              <>
                <h3 className="modal-name">{name ?? tag}</h3>
                <span className="muted small">{tag}</span>
              </>
            )}
          </div>
          <button className="modal-close" onClick={close} aria-label={t.warlog.close}>✕</button>
        </div>

        {failed && <p className="muted small">{t.warlog.error}</p>}
        {!failed && overview === null && <p className="muted small">{t.warlog.loading}</p>}
        {overview !== null && <ClanProfileCard clan={overview} />}
      </div>
    </div>
  )
}
