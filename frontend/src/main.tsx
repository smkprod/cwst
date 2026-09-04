import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { initTelegram } from './lib/telegram'
import { ensureBotUsername } from './lib/botUsername'
import { LangProvider } from './lib/i18n'
import './styles.css'

initTelegram()

// Юзернейм бота нужен любой кнопке «поделиться», а не только экрану приглашения.
// Не ждём ответа: ссылки собираются в момент нажатия, к тому времени он уже придёт.
void ensureBotUsername()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <LangProvider>
      <App />
    </LangProvider>
  </React.StrictMode>,
)

// Заставку из index.html убираем только после того, как React отрисовал свою:
// render() ставит работу в очередь, поэтому ждём два кадра — иначе между двумя
// заставками мигнёт пустой экран.
requestAnimationFrame(() =>
  requestAnimationFrame(() => document.getElementById('boot-splash')?.remove()))
