import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'
import { haptic, openTelegramLink } from '../lib/telegram'
import { NEWS_CHANNEL, VICE_RELEASE_UTC, hideViceBanner, viceActive, viceBannerHidden } from '../lib/vice'
import { Icon } from './ui/Icon'

/** Тема на весь экран, пока идёт неделя релиза. */
export function useViceTheme() {
  useEffect(() => {
    if (!viceActive()) return
    document.documentElement.classList.add('vice')
    return () => document.documentElement.classList.remove('vice')
  }, [])
}

/**
 * Карточка к выходу GTA VI: закат над пальмами, отсчёт до релиза и кнопка в
 * новостной канал. Крестиком прячется насовсем.
 */
export function ViceBanner() {
  const { t } = useT()
  const s = t.vice
  const [hidden, setHidden] = useState(viceBannerHidden)
  const [now, setNow] = useState(Date.now())

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 30_000)
    return () => clearInterval(id)
  }, [])

  if (hidden || !viceActive(now)) return null
  const left = VICE_RELEASE_UTC - now
  const out = left <= 0
  const h = Math.floor(left / 3_600_000)
  const m = Math.floor((left % 3_600_000) / 60_000)

  return (
    <section className="vice-card fade-in">
      <ViceScene />
      <button className="vice-close" aria-label="×" onClick={() => { haptic('light'); hideViceBanner(); setHidden(true) }}>
        <Icon name="x" size={16} />
      </button>
      <div className="vice-body">
        <span className="vice-kicker">{out ? s.kickerOut : s.kicker}</span>
        <h2 className="vice-title">GTA <b>VI</b></h2>
        <p className="vice-sub">{out ? s.outText : s.soonText.replace('{h}', String(h)).replace('{m}', String(m).padStart(2, '0'))}</p>
        <button className="vice-cta" onClick={() => { haptic('medium'); openTelegramLink(NEWS_CHANNEL) }}>
          <Icon name="megaphone" size={16} /> {s.cta}
        </button>
        <span className="vice-fine">{s.fine}</span>
      </div>
    </section>
  )
}

/** Закат в духе Вайс-Сити: полосатое солнце, сетка до горизонта и пальмы - всё нарисовано, без чужих картинок. */
function ViceScene() {
  return (
    <svg className="vice-scene" viewBox="0 0 360 200" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      <defs>
        <linearGradient id="viceSky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#1b0938" />
          <stop offset="0.55" stopColor="#7a1f6e" />
          <stop offset="0.8" stopColor="#ff6a3d" />
          <stop offset="1" stopColor="#ffb347" />
        </linearGradient>
        <linearGradient id="viceSun" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffe66d" />
          <stop offset="1" stopColor="#ff3d8b" />
        </linearGradient>
        <clipPath id="viceSunCut">
          <rect x="0" y="0" width="360" height="104" />
          <rect x="0" y="108" width="360" height="5" />
          <rect x="0" y="117" width="360" height="4" />
          <rect x="0" y="125" width="360" height="3" />
        </clipPath>
      </defs>
      <rect width="360" height="200" fill="url(#viceSky)" />
      <circle cx="250" cy="128" r="56" fill="url(#viceSun)" clipPath="url(#viceSunCut)" />
      <rect y="132" width="360" height="68" fill="#140a2a" />
      <g stroke="#ff3d8b" strokeOpacity="0.55" strokeWidth="1">
        {[0, 1, 2, 3, 4].map(i => <line key={`h${i}`} x1="0" x2="360" y1={136 + i * i * 3.2} y2={136 + i * i * 3.2} />)}
        {[-6, -4, -2, 0, 2, 4, 6, 8, 10].map(i => <line key={`v${i}`} x1={180 + i * 14} y1="132" x2={180 + i * 60} y2="200" />)}
      </g>
      <Palm x={46} scale={1} />
      <Palm x={98} scale={0.72} />
      <Palm x={330} scale={0.85} />
    </svg>
  )
}

function Palm({ x, scale }: { x: number; scale: number }) {
  return (
    <g transform={`translate(${x} 134) scale(${scale})`} fill="#12061f">
      <path d="M-3 0 C -2 -40, 4 -70, 10 -96 L 14 -95 C 9 -70, 4 -40, 4 0 Z" />
      <path d="M12 -96 C -10 -104, -30 -96, -44 -80 C -26 -90, -8 -92, 10 -90 Z" />
      <path d="M12 -96 C 30 -108, 52 -104, 64 -88 C 46 -96, 30 -96, 14 -91 Z" />
      <path d="M12 -97 C 0 -118, -20 -124, -34 -118 C -16 -114, 0 -108, 10 -94 Z" />
      <path d="M13 -97 C 24 -118, 44 -124, 58 -116 C 40 -114, 26 -108, 15 -94 Z" />
      <path d="M12 -95 C 4 -82, -6 -64, -10 -50 C -2 -64, 6 -80, 14 -92 Z" />
    </g>
  )
}
