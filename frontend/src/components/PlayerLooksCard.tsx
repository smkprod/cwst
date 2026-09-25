import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { Achievement, AppConfig, BackgroundKey } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { BADGE_ICONS } from '../lib/sponsorMarks'

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
      <div className="card-title">{t.looks.title}</div>

      <p className="adm-block-title">{t.looks.badgeTitle}</p>
      {badges.length === 0 ? (
        <p className="muted small">{t.looks.noBadges}</p>
      ) : (
        <>
          <p className="muted small">{t.looks.badgeHint}</p>
          <div className="looks-badges">
            {badges.map(b => (
              <button
                key={b.key}
                className={`looks-badge ${showcase === b.key ? 'looks-badge-on' : ''}`}
                disabled={busy}
                onClick={() => pickBadge(showcase === b.key ? null : b.key)}
                aria-label={b.key}
              >
                <span className="looks-badge-icon">{BADGE_ICONS[b.key] ?? '🏅'}</span>
                <span className="looks-badge-lvl muted small">{'★'.repeat(b.level)}</span>
              </button>
            ))}
          </div>
        </>
      )}

      <p className="adm-block-title">{t.looks.bgTitle}</p>
      {isSponsor && config ? (
        <>
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
          {config?.sponsorContact && (
            <a
              className="btn hall-sponsor-btn"
              href={`https://t.me/${config.sponsorContact}`}
              target="_blank"
              rel="noreferrer"
              onClick={() => haptic('medium')}
            >
              {t.hall.becomeSponsor}
            </a>
          )}
        </>
      )}
    </section>
  )
}
