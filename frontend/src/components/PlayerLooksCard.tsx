import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { Achievement, AppConfig, BackgroundKey } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { BadgeIcon } from '../lib/sponsorMarks'
import { Icon } from './ui/Icon'
import { InfoButton } from './ui/Info'
import { IconTile } from './ui/Section'
import { SponsorBuyButton } from './SponsorBuyButton'

/**
 * Оформление себя: значок напоказ и фоны спонсора.
 *
 * Живёт на вкладке «Я», а не только на Аллее, по двум причинам. Значок может
 * выставить любой, и искать эту возможность на витрине чужих достижений
 * неоткуда. А у спонсора выбор фона до этого открывался только со строки
 * «Твоё место», которой нет, пока он не попал в сезонный зачёт, — то есть
 * купивший спонсорство новичок не нашёл бы, где выбрать фон, вообще.
 */
export function PlayerLooksCard() {
  const { t } = useT()
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [badges, setBadges] = useState<Achievement[]>([])
  const [showcase, setShowcase] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Свёрнуто по умолчанию: развёрнутый выбор — это девять плиток фонов, и он
  // отжимал бы статистику на экран вниз при каждом открытии вкладки. Заголовок
  // сверху виден всегда, а искать оформление приходят один раз — после покупки.
  const [open, setOpen] = useState(false)

  const loadConfig = useCallback(() => {
    api.getAppConfig().then(setConfig).catch(() => { /* останемся без выбора фона */ })
  }, [])

  useEffect(() => {
    loadConfig()
    api.getMyAchievements()
      .then(a => {
        setBadges(a.badges.filter(b => b.level > 0))
        setShowcase(a.showcaseKey ?? null)
      })
      .catch(() => { /* наград ещё нет — покажем подсказку */ })
  }, [loadConfig])

  const pickBadge = async (key: string | null) => {
    haptic('light')
    setBusy(true)
    try {
      const r = await api.setShowcaseBadge(key)
      setShowcase(r.key)
      hapticNotify('success')
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  const pickBackground = async (key: BackgroundKey | null, scope: 'player' | 'clan') => {
    haptic('light')
    setBusy(true)
    try {
      await api.setMyBackground(key, scope)
      hapticNotify('success')
      loadConfig()
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  const isSponsor = config?.isSponsor ?? false

  return (
    <section className="card">
      <button className="looks-head" onClick={() => { haptic('light'); setOpen(o => !o) }}>
        <span className="pl-head-left"><IconTile name="palette" tone="violet" /><span className="ui-head-title">{t.looks.title}</span></span>
        <span className="looks-head-right">
          {/* Что выбрано сейчас — видно, не разворачивая */}
          {showcase && <span className="looks-head-badge"><BadgeIcon badge={showcase} size={18} /></span>}
          {isSponsor && config?.myBackground && (
            <span
              className="looks-head-bg"
              style={{ backgroundImage: `url(/bg/${config.myBackground}.webp)` }}
            />
          )}
          <span className="looks-head-chev"><Icon name={open ? 'chevronDown' : 'chevronRight'} size={18} /></span>
        </span>
      </button>

      {!open ? null : (
      <>
      <p className="adm-block-title pl-subhead">
        {t.looks.badgeTitle}
        {badges.length > 0 && <InfoButton title={t.looks.badgeTitle}>{t.looks.badgeHint}</InfoButton>}
      </p>
      {badges.length === 0 ? (
        <p className="muted small">{t.looks.noBadges}</p>
      ) : (
        <>
          <div className="looks-badges">
            {badges.map(b => (
              <button
                key={b.key}
                className={`looks-badge ${showcase === b.key ? 'looks-badge-on' : ''}`}
                disabled={busy}
                onClick={() => pickBadge(showcase === b.key ? null : b.key)}
                aria-label={b.key}
              >
                <span className="looks-badge-icon"><BadgeIcon badge={b.key} size={22} /></span>
                <span className="looks-badge-lvl muted small pl-stars">
                  {Array.from({ length: b.level }, (_, i) => <Icon key={i} name="star" size={10} fill="currentColor" />)}
                </span>
              </button>
            ))}
          </div>
        </>
      )}

      <p className="adm-block-title">{t.looks.bgTitle}</p>
      {isSponsor && config ? (
        <>
          {/* Срок раньше не показывался нигде: сервер его отдавал, а спонсорство
              кончалось молча, и продлевать было не с чего. */}
          {config.sponsorUntil && (
            <div className="looks-until">
              <span className="looks-until-text">
                {t.sponsor.until.replace('{date}', new Date(config.sponsorUntil).toLocaleDateString())}
              </span>
              <SponsorBuyButton config={config} onBought={loadConfig} className="btn-mini" />
            </div>
          )}
          <p className="muted small">{t.looks.bgMine}</p>
          <div className="bg-grid">
            {config.playerBackgrounds.map(k => (
              <button
                key={k}
                className={`bg-tile ${config.myBackground === k ? 'bg-tile-on' : ''}`}
                style={{ backgroundImage: `url(/bg/${k}.webp)` }}
                disabled={busy}
                onClick={() => pickBackground(config.myBackground === k ? null : k, 'player')}
                aria-label={k}
              />
            ))}
          </div>

          <p className="muted small">{t.looks.bgClan}</p>
          <div className="bg-grid">
            {config.clanBackgrounds.map(k => (
              <button
                key={k}
                className={`bg-tile bg-tile-tall ${config.myClanBackground === k ? 'bg-tile-on' : ''}`}
                style={{ backgroundImage: `url(/bg/${k}.webp)` }}
                disabled={busy}
                onClick={() => pickBackground(config.myClanBackground === k ? null : k, 'clan')}
                aria-label={k}
              />
            ))}
          </div>
        </>
      ) : (
        <>
          <p className="muted small">{t.looks.notSponsor}</p>
          {config && <SponsorBuyButton config={config} onBought={loadConfig} />}
        </>
      )}
      </>
      )}
    </section>
  )
}
