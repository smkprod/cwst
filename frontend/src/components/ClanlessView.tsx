import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { ClanOverview, PlayerProfile } from '../types'
import { useT } from '../lib/i18n'
import { haptic, shareToTelegram, botStartLink, BOT_USERNAME } from '../lib/telegram'
import { LangSwitcher } from './LangSwitcher'
import { RecruitToggle } from './RecruitToggle'
import { PlayerSearchView } from './PlayerSearchView'
import { TournamentView } from './TournamentView'
import { TopPlayersTeaser } from './TopPlayersTeaser'
import { PlayerProfileCard } from './PlayerProfileCard'
import { DecksButton } from './DecksButton'
import { RegionTopCard } from './RegionTopCard'
import { BotTourCard } from './BotTourCard'

type Tab = 'profile' | 'clan' | 'search' | 'tournament'

/**
 * Экран игрока, чей клан не подключён к боту. Раньше здесь была одна инструкция и
 * тупик: делать нечего, смотреть нечего. Теперь это личный кабинет — своя статистика,
 * разбор коллекции, подбор колод и рейтинг кланов региона, — а просьба подключить клан
 * стоит рядом с тем, что человек за это получит.
 */
export function ClanlessView() {
  const { t } = useT()
  const [tab, setTab] = useState<Tab>('profile')

  const [profile, setProfile] = useState<PlayerProfile | null>(null)
  const [overview, setOverview] = useState<ClanOverview | null>(null)
  const [profileState, setProfileState] = useState<'loading' | 'ready' | 'error'>('loading')

  useEffect(() => {
    let alive = true

    ;(async () => {
      try {
        const me = await api.getMe()
        const p = await api.getPlayerProfile(me.playerTag)
        if (!alive) return
        setProfile(p)
        setProfileState('ready')

        // Клан игрока в самой игре — отдельный запрос, и его провал не должен
        // уносить с собой уже загруженный профиль
        if (p.clanTag) {
          try {
            const o = await api.getClanOverview(p.clanTag)
            if (alive) setOverview(o)
          } catch { /* рейтинг недоступен — покажем экран без него */ }
        }
      } catch {
        if (alive) setProfileState('error')
      }
    })()

    return () => { alive = false }
  }, [])

  const switchTab = (next: Tab) => {
    haptic('light')
    setTab(next)
  }

  const shareInstructions = () => {
    haptic('medium')
    shareToTelegram(t.clanless.shareText, botStartLink())
  }

  // Пока профиль не загрузился — не даём вкладкам, которые без него бессмысленны
  const hasProfile = profileState === 'ready' && profile !== null

  const tabs: { id: Tab; icon: string; label: string }[] = [
    { id: 'profile', icon: '👤', label: t.clanless.tabProfile },
    { id: 'clan', icon: '🏰', label: t.clanless.tabClan },
    { id: 'search', icon: '🔍', label: t.tabs.search },
    { id: 'tournament', icon: '🥇', label: t.tabs.tournament },
  ]

  return (
    <>
      <main className="with-tabbar fade-in">
        <div className="lang-switcher-bar">
          <LangSwitcher />
        </div>

        {tab === 'profile' && (
          <div className="fade-in">
            <ConnectBanner profile={profile} overview={overview} onShare={shareInstructions} />

            {profileState === 'loading' && (
              <div className="center" style={{ marginTop: 24 }}><div className="spinner" /></div>
            )}
            {profileState === 'error' && (
              <p className="center muted small" style={{ marginTop: 16 }}>{t.clanless.profileError}</p>
            )}
            {hasProfile && (
              <>
                <DecksButton playerTag={profile.playerTag} />
                <PlayerProfileCard profile={profile} />
              </>
            )}
          </div>
        )}

        {tab === 'clan' && (
          <div className="fade-in">
            <ConnectBanner profile={profile} overview={overview} onShare={shareInstructions} />

            <section className="card">
              <div className="card-title">{t.clanless.stepsTitle}</div>
              <ol className="setup-steps">
                <li>{t.clanless.step1}</li>
                <li>{t.clanless.step2}</li>
                <li>{t.clanless.step3}</li>
              </ol>
              {BOT_USERNAME && (
                <button className="btn btn-nudge" style={{ width: '100%', marginTop: 10 }} onClick={shareInstructions}>
                  {t.clanless.shareBtn}
                </button>
              )}
            </section>

            <BotTourCard />

            {overview && <RegionTopCard overview={overview} />}

            <TopPlayersTeaser />

            <p className="muted small" style={{ textAlign: 'center', margin: '16px 0 12px' }}>
              {t.clanless.orRecruit}
            </p>
            <RecruitToggle />
          </div>
        )}

        {tab === 'search' && <div className="fade-in"><PlayerSearchView /></div>}
        {tab === 'tournament' && <div className="fade-in"><TournamentView /></div>}
      </main>

      <nav className="tabbar" role="tablist">
        {tabs.map(x => (
          <button
            key={x.id}
            role="tab"
            aria-selected={tab === x.id}
            className={`tab ${tab === x.id ? 'tab-active' : ''}`}
            onClick={() => switchTab(x.id)}
          >
            <span className="tab-icon">{x.icon}</span>
            <span className="tab-label">{x.label}</span>
          </button>
        ))}
      </nav>
    </>
  )
}

/**
 * Главное сообщение экрана: клан существует, но бот его не видит. Пишем это словами
 * самого игрока — «твой клан», с названием, — и сразу даём кнопку, которая отправит
 * инструкцию главе за него.
 */
function ConnectBanner({ profile, overview, onShare }: {
  profile: PlayerProfile | null; overview: ClanOverview | null; onShare: () => void
}) {
  const { t } = useT()

  // Игрок вообще без клана в игре — просить его «подключить клан» бессмысленно.
  // Ему нужен клан, а не бот, поэтому и текст другой.
  if (profile && !profile.clanTag) {
    return (
      <section className="card connect-banner">
        <div className="connect-title">🏰 {t.clanless.noClanTitle}</div>
        <p className="muted small" style={{ margin: '4px 0 0' }}>{t.clanless.noClanText}</p>
      </section>
    )
  }

  // Клан уже подключён, а экран всё ещё этот — значит игрок пока не привязан к клану
  // внутри бота. Врать «подключи клан» в такой ситуации нельзя.
  if (overview?.connected) {
    return (
      <section className="card connect-banner connect-banner-ok">
        <div className="connect-title">✅ {t.clanless.connectedTitle}</div>
        <p className="muted small" style={{ margin: 0 }}>{t.clanless.connectedText}</p>
      </section>
    )
  }

  return (
    <section className="card connect-banner">
      <div className="connect-title">
        🏰 {overview?.clanName
          ? `${t.clanless.bannerPrefix} «${overview.clanName}» ${t.clanless.bannerSuffix}`
          : t.clanless.heroTitle}
      </div>
      <p className="muted small" style={{ margin: '4px 0 0' }}>{t.clanless.bannerAsk}</p>

      {overview && (
        <div className="connect-stats">
          <span>⚔️ {overview.warTrophies}</span>
          {overview.memberCount != null && <span>👥 {overview.memberCount}/50</span>}
          {overview.countryName && <span>🌍 {overview.countryName}</span>}
        </div>
      )}

      {BOT_USERNAME && (
        <button className="btn btn-nudge" style={{ width: '100%', marginTop: 10 }} onClick={onShare}>
          {t.clanless.askLeader}
        </button>
      )}
    </section>
  )
}
