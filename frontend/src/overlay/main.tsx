import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { translationsFor } from '../lib/i18n'
import { Overlay } from './Overlay'
import './overlay.css'

/**
 * Вход виджетов OBS. Отдельная страница, а не маршрут мини-аппа: здесь нет ни
 * Telegram, ни входа, ни провайдеров приложения — только ссылка с ключом.
 */
const params = new URLSearchParams(window.location.search)
const lang = params.get('lang')
const t = translationsFor(lang)
document.documentElement.lang = lang === 'uk' || lang === 'en' ? lang : 'ru'

// В OBS фон обязан быть прозрачным. Открытая в обычном браузере (кнопка «Открыть»
// в Студии), та же страница на белом выглядела бы сломанной — там подкладываем
// тёмную «шахматку», как в редакторах: видно и виджет, и то, что фон прозрачный.
if (!('obsstudio' in window)) document.documentElement.classList.add('ov-preview')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Overlay widget={params.get('w')} k={params.get('k')} c={params.get('c')} t={t} />
  </StrictMode>,
)
