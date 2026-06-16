import { useState } from 'react'
import { useT } from '../lib/i18n'
import { haptic } from '../lib/telegram'
import { LangSwitcher } from './LangSwitcher'
import { RecruitToggle } from './RecruitToggle'
import { PlayerSearchView } from './PlayerSearchView'

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
            <div className="center" style={{ paddingBottom: 24 }}>
              <p style={{ fontSize: 40, margin: 0 }}>🏰</p>
              <p><strong>{t.clanless.title}</strong></p>
              <p className="muted small" style={{ maxWidth: 280, textAlign: 'center' }}>
                {t.clanless.hint}
              </p>
            </div>
            <p className="muted small" style={{ textAlign: 'center', marginBottom: 12 }}>
              {t.clanless.recruitHint}
            </p>
            <RecruitToggle />
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
