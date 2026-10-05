import { useEffect, useState } from 'react'
import { Icon, type IconName } from './ui/Icon'
import { api } from '../lib/api'
import type { ClanOverview, PlayerProfile } from '../types'
import { useT } from '../lib/i18n'
import { haptic, shareToTelegram, botStartLink } from '../lib/telegram'
import { useBotUsername } from '../lib/botUsername'
import { RecruitToggle } from './RecruitToggle'
import { PlayerSearchView } from './PlayerSearchView'
import { TopPlayersTeaser } from './TopPlayersTeaser'
import { RegionTopCard } from './RegionTopCard'
import { BotTourCard } from './BotTourCard'
import { ChallengeView } from './ChallengeView'
import { DuelView } from './DuelView'
import { OPEN_CHALLENGE } from '../lib/promo'
import { MyStatsView, type BattlesView, type MeSection } from './MyStatsView'
import { WorldTopView } from './WorldTopView'
import { MoreView } from './MoreView'

export type SoloTab = 'me' | 'meta' | 'clan' | 'search' | 'more' | 'challenge' | 'duel'

/** Почему у игрока нет экрана войны: клан не подключён или у подключённого нет войны. */
export type SoloReason = 'noClan' | 'noWar'

/**
 * Приложение игрока без войны клана: клан не подключён, клана нет вовсе или у
 * подключённого сейчас нет войны.
 *
 * Раньше это был отдельный экран-тупик с профилем и просьбой подключить клан.
 * Теперь это то же приложение для игрока: разбор своих боёв первым, мета топа,
 * поиск, «Ещё» с языком и Плюсом. Клан — одна из вкладок, а не условие входа.
 */
export function ClanlessView({ reason = 'noClan', initialTab = 'me', meSection = 'battles', battlesView = 'history', openMatchId = null, showChallenge = false, showDuel = false }: {
  reason?: SoloReason
  initialTab?: SoloTab
  meSection?: MeSection
  battlesView?: BattlesView
  openMatchId?: number | null
  /** Владелец включил вкладку челленджа - она нужна и игрокам без клана: они и приходят с рекламы. */
  showChallenge?: boolean
  /** Вкладка лиги дуэлей - по тому же выбору владельца. */
  showDuel?: boolean
} = {}) {
  const { t } = useT()
  const botUsername = useBotUsername()
  const [tab, setTab] = useState<SoloTab>(initialTab)
  useEffect(() => {
    const go = () => setTab('challenge')
    window.addEventListener(OPEN_CHALLENGE, go)
    return () => window.removeEventListener(OPEN_CHALLENGE, go)
  }, [])

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

  const switchTab = (next: SoloTab) => {
    haptic('light')
    setTab(next)
  }

  const shareInstructions = () => {
    haptic('medium')
    shareToTelegram(t.clanless.shareText, botStartLink())
  }

  const tabs: { id: SoloTab; icon: IconName; label: string }[] = [
    { id: 'me', icon: 'user' as IconName, label: t.tabs.me },
    { id: 'meta', icon: 'flame' as IconName, label: t.worldTop.tabMeta },
    ...(showChallenge ? [{ id: 'challenge' as SoloTab, icon: 'ticket' as IconName, label: t.tabs.challenge }] : []),
    ...(showDuel ? [{ id: 'duel' as SoloTab, icon: 'swords' as IconName, label: t.tabs.duel }] : []),
    { id: 'clan', icon: 'castle' as IconName, label: t.clanless.tabClan },
    { id: 'search', icon: 'search' as IconName, label: t.tabs.search },
    { id: 'more', icon: 'gear' as IconName, label: t.tabs.more },
  ]

  return (
    <>
      <main className="with-tabbar fade-in">
        {tab === 'me' && (
          <div className="fade-in">
            <MyStatsView defaultSection={meSection} battlesView={battlesView} openMatchId={openMatchId} />
          </div>
        )}

        {tab === 'meta' && <div className="fade-in"><WorldTopView /></div>}
        {tab === 'challenge' && <ChallengeView />}
        {tab === 'duel' && <DuelView />}

        {tab === 'clan' && (
          <div className="fade-in">
            {reason === 'noWar' ? (
              <section className="card connect-banner connect-banner-ok">
                <div className="connect-title">⏳ {t.clanless.noWarTitle}</div>
                <p className="muted small" style={{ margin: 0 }}>{t.clanless.noWarText}</p>
              </section>
            ) : (
              <ConnectBanner profile={profile} overview={overview} onShare={shareInstructions} />
            )}
            {profileState === 'error' && (
              <p className="center muted small" style={{ marginTop: 8 }}>{t.clanless.profileError}</p>
            )}

            {reason === 'noClan' && <section className="card">
              <div className="card-title">{t.clanless.stepsTitle}</div>
              <ol className="setup-steps">
                <li>{t.clanless.step1}</li>
                <li>{t.clanless.step2}</li>
                <li>{t.clanless.step3}</li>
              </ol>
              {botUsername && (
                <button className="btn btn-nudge" style={{ width: '100%', marginTop: 10 }} onClick={shareInstructions}>
                  {t.clanless.shareBtn}
                </button>
              )}
            </section>}

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
        {tab === 'more' && (
          <MoreView canManage={false} isLeader={false} onOpenNotifications={() => { /* уведомления — у клана */ }} />
        )}
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
            <span className="tab-icon"><Icon name={x.icon} size={20} /></span>
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
  const botUsername = useBotUsername()

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

      {botUsername && (
        <button className="btn btn-nudge" style={{ width: '100%', marginTop: 10 }} onClick={onShare}>
          {t.clanless.askLeader}
        </button>
      )}
    </section>
  )
}
