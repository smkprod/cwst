import { useState } from 'react'
import { useT } from '../lib/i18n'
import { haptic, shareToTelegram, botStartLink, BOT_USERNAME } from '../lib/telegram'
import { LangSwitcher } from './LangSwitcher'
import { PlayerSearchView } from './PlayerSearchView'
import { TopPlayersTeaser } from './TopPlayersTeaser'

type Tab = 'home' | 'search'

export function ClanlessView() {
  const { t } = useT()
  const [tab, setTab] = useState<Tab>('home')

  const switchTab = (next: Tab) => {
    haptic('light')
    setTab(next)
  }

  return (
    <>
      <main className="with-tabbar fade-in">
        <div className="lang-switcher-bar">
          <LangSwitcher />
        </div>

        {tab === 'home' && (
          <div>
            {/* Психология: не «у тебя ничего нет», а миссия с готовыми шагами и
                одной кнопкой, которая делает всё за пользователя (шеринг в чат). */}
            <div className="center" style={{ minHeight: 'auto', paddingBottom: 12 }}>
              <p style={{ fontSize: 40, margin: 0 }}>🏰</p>
              <p style={{ margin: '10px 0 2px' }}><strong>{t.clanless.heroTitle}</strong></p>
              <p className="muted small" style={{ maxWidth: 280, textAlign: 'center', margin: 0 }}>
                {t.clanless.heroSub}
              </p>
            </div>

            <section className="card">
              <div className="card-title">{t.clanless.stepsTitle}</div>
              <ol className="setup-steps">
                <li>{t.clanless.step1}</li>
                <li>{t.clanless.step2}</li>
                <li>{t.clanless.step3}</li>
              </ol>
              {BOT_USERNAME && (
                <button
                  className="btn btn-nudge"
                  style={{ width: '100%', marginTop: 10 }}
                  onClick={() => {
                    haptic('medium')
                    shareToTelegram(t.clanless.shareText, botStartLink())
                  }}
                >
                  {t.clanless.shareBtn}
                </button>
              )}
            </section>

            <TopPlayersTeaser />

          </div>
        )}

        {tab === 'search' && (
          <div className="fade-in">
            <PlayerSearchView />
          </div>
        )}

      </main>

      <nav className="tabbar" role="tablist">
        <button
          role="tab"
          aria-selected={tab === 'home'}
          className={`tab ${tab === 'home' ? 'tab-active' : ''}`}
          onClick={() => switchTab('home')}
        >
          <span className="tab-icon">🏰</span>
          <span className="tab-label">{t.clanless.title}</span>
        </button>
        <button
          role="tab"
          aria-selected={tab === 'search'}
          className={`tab ${tab === 'search' ? 'tab-active' : ''}`}
          onClick={() => switchTab('search')}
        >
          <span className="tab-icon">🔍</span>
          <span className="tab-label">{t.tabs.search}</span>
        </button>
      </nav>
    </>
  )
}
