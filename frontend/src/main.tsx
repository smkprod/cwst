import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { ChallengePromo } from './components/ChallengePromo'
import { HalloweenSplash } from './components/HalloweenSplash'
import { FeedbackSheet } from './components/FeedbackSheet'
import { TipSheet } from './components/TipSheet'
import { initTelegram } from './lib/telegram'
import { ensureBotUsername } from './lib/botUsername'
import { LangProvider } from './lib/i18n'
import { ClanModalProvider } from './lib/clanModal'
import { PlayerSheetProvider } from './lib/playerSheet'
import { PlusSheetProvider } from './lib/plusSheet'
import { ShowcaseHost, ShowcaseProvider } from './lib/showcase'
import './styles.css'
import './styles/ui-owner.css'
import './styles/ui-player.css'
import './styles/ui-clan.css'
import './styles/ui-battles.css'
import './styles/ui-misc.css'
import './styles/ui-studio.css'
import './styles/ui-showcase.css'
import './styles/ui-halloween.css'
import './styles/ui-feedback.css'
import './styles/ui-dashboard.css'
import './styles/ui-clanpro.css'

initTelegram()

// Юзернейм бота нужен любой кнопке «поделиться», а не только экрану приглашения.
// Не ждём ответа: ссылки собираются в момент нажатия, к тому времени он уже придёт.
void ensureBotUsername()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <LangProvider>
      <ShowcaseProvider>
        <PlusSheetProvider>
          <PlayerSheetProvider>
            <ClanModalProvider>
              <App />
              <ChallengePromo />
              <HalloweenSplash />
              <FeedbackSheet />
              <TipSheet />
              <ShowcaseHost />
            </ClanModalProvider>
          </PlayerSheetProvider>
        </PlusSheetProvider>
      </ShowcaseProvider>
    </LangProvider>
  </React.StrictMode>,
)

// Заставку из index.html убираем только после того, как React отрисовал свою:
// render() ставит работу в очередь, поэтому ждём два кадра — иначе между двумя
// заставками мигнёт пустой экран.
requestAnimationFrame(() =>
  requestAnimationFrame(() => document.getElementById('boot-splash')?.remove()))
