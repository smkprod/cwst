import { useId } from 'react'

/**
 * Цветные «игровые» иконки для профиля: кубок, мечи, корона, карты, огонь, мишень,
 * график, свиток. Свой рисунок, не арт игры: залитые формы с градиентом, бликом и
 * тёмным контуром - чтобы читались на тёмных панелях так же, как значки в игре.
 */
export type GameIconName = 'trophy' | 'swords' | 'crown' | 'cards' | 'flame' | 'target' | 'chart' | 'scroll' | 'drop' | 'lock' | 'gem' | 'play'

export function GameIcon({ name, size = 28 }: { name: GameIconName; size?: number }) {
  const id = useId().replace(/:/g, '')
  const g = (n: string) => `${n}${id}`
  const stroke = '#1a1030'
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" className="game-icon">
      <defs>
        <linearGradient id={g('gold')} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#fff1b0" /><stop offset="0.5" stopColor="#ffc83d" /><stop offset="1" stopColor="#d9840f" /></linearGradient>
        <linearGradient id={g('steel')} x1="0" y1="0" x2="1" y2="1"><stop offset="0" stopColor="#ffffff" /><stop offset="1" stopColor="#8fa3c8" /></linearGradient>
        <linearGradient id={g('blue')} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#9cc6ff" /><stop offset="1" stopColor="#2c5fd6" /></linearGradient>
        <linearGradient id={g('red')} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#ffa0a8" /><stop offset="1" stopColor="#d61f36" /></linearGradient>
        <linearGradient id={g('fire')} x1="0" y1="1" x2="0" y2="0"><stop offset="0" stopColor="#ff3d1f" /><stop offset="0.6" stopColor="#ff9c3a" /><stop offset="1" stopColor="#ffe08a" /></linearGradient>
        <linearGradient id={g('violet')} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#e3d2ff" /><stop offset="1" stopColor="#7c4dff" /></linearGradient>
        <linearGradient id={g('green')} x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#b5f5cf" /><stop offset="1" stopColor="#1f9c58" /></linearGradient>
      </defs>
      {name === 'trophy' && (
        <g stroke={stroke} strokeWidth="1.4" strokeLinejoin="round">
          <path d="M8 5h16v6a8 8 0 0 1-16 0V5Z" fill={`url(#${g('gold')})`} />
          <path d="M8 7H4v2a5 5 0 0 0 5 5M24 7h4v2a5 5 0 0 1-5 5" fill="none" stroke="#d9840f" strokeWidth="2.2" />
          <path d="M13 18h6l1 5h-8l1-5Z" fill={`url(#${g('gold')})`} />
          <rect x="9" y="23" width="14" height="4" rx="1.5" fill="#8a5a1a" />
          <path d="M11 7.5v3.5" stroke="#fff8d8" strokeWidth="2" strokeLinecap="round" />
        </g>
      )}
      {name === 'swords' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <path d="M5 4l13 13-2 2L3 6V4h2Z" fill={`url(#${g('steel')})`} />
          <path d="M27 4 14 17l2 2L29 6V4h-2Z" fill={`url(#${g('steel')})`} />
          <path d="M8 19l5 5-2 2-5-5 2-2ZM24 19l-5 5 2 2 5-5-2-2Z" fill={`url(#${g('gold')})`} />
          <circle cx="7.5" cy="26.5" r="2" fill="#8a5a1a" /><circle cx="24.5" cy="26.5" r="2" fill="#8a5a1a" />
        </g>
      )}
      {name === 'crown' && (
        <g stroke={stroke} strokeWidth="1.4" strokeLinejoin="round">
          <path d="M3 10l7 6 6-10 6 10 7-6-3 15H6L3 10Z" fill={`url(#${g('gold')})`} />
          <rect x="6" y="24" width="20" height="4" rx="1.5" fill="#d9840f" />
          <circle cx="16" cy="6" r="2" fill="#ff5a6e" /><circle cx="3" cy="10" r="1.6" fill="#fff1b0" /><circle cx="29" cy="10" r="1.6" fill="#fff1b0" />
        </g>
      )}
      {name === 'cards' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <rect x="4" y="7" width="13" height="18" rx="2.5" transform="rotate(-14 10 16)" fill={`url(#${g('red')})`} />
          <rect x="10" y="5" width="13" height="18" rx="2.5" fill={`url(#${g('blue')})`} />
          <rect x="15" y="8" width="13" height="18" rx="2.5" transform="rotate(12 21 17)" fill={`url(#${g('violet')})`} />
          <path d="M21 13l1.6 3.2 3.4.4-2.5 2.3.7 3.3-3.2-1.7-3.2 1.7.7-3.3-2.5-2.3 3.4-.4L21 13Z" fill="#fff1b0" stroke="none" transform="rotate(12 21 17)" />
        </g>
      )}
      {name === 'flame' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <path d="M16 3c2 5 8 7 8 15a8 8 0 0 1-16 0c0-4 2-6 4-8 0 3 1 5 3 5-1-4 0-8 1-12Z" fill={`url(#${g('fire')})`} />
          <path d="M16 15c1 3 4 4 4 7a4 4 0 0 1-8 0c0-2 1-3 2-4 0 1 1 2 2 2 0-2-.5-3 0-5Z" fill="#fff1b0" stroke="none" />
        </g>
      )}
      {name === 'target' && (
        <g stroke={stroke} strokeWidth="1.3">
          <circle cx="16" cy="16" r="12" fill={`url(#${g('red')})`} />
          <circle cx="16" cy="16" r="8" fill="#fff" />
          <circle cx="16" cy="16" r="4.5" fill={`url(#${g('red')})`} />
          <path d="M16 16 27 5M24 4h4v4" fill="none" stroke="#ffc83d" strokeWidth="2.2" strokeLinecap="round" />
        </g>
      )}
      {name === 'chart' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <rect x="4" y="16" width="6" height="12" rx="1.5" fill={`url(#${g('blue')})`} />
          <rect x="13" y="10" width="6" height="18" rx="1.5" fill={`url(#${g('violet')})`} />
          <rect x="22" y="4" width="6" height="24" rx="1.5" fill={`url(#${g('green')})`} />
        </g>
      )}
      {name === 'scroll' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <rect x="6" y="5" width="20" height="22" rx="3" fill="#f3e2b8" />
          <path d="M10 11h12M10 16h12M10 21h8" stroke="#8a5a1a" strokeWidth="2" strokeLinecap="round" />
          <rect x="4" y="3" width="24" height="5" rx="2.5" fill={`url(#${g('gold')})`} />
        </g>
      )}
      {name === 'drop' && (
        <path d="M16 3c4 6 9 11 9 17a9 9 0 0 1-18 0c0-6 5-11 9-17Z" fill={`url(#${g('violet')})`} stroke={stroke} strokeWidth="1.3" />
      )}
      {name === 'lock' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <path d="M10 14v-4a6 6 0 0 1 12 0v4" fill="none" stroke="#c7d2ec" strokeWidth="3" />
          <rect x="6" y="14" width="20" height="14" rx="3" fill={`url(#${g('violet')})`} />
          <circle cx="16" cy="21" r="2.2" fill="#1a1030" stroke="none" />
        </g>
      )}
      {name === 'gem' && (
        <g stroke={stroke} strokeWidth="1.3" strokeLinejoin="round">
          <path d="M8 5h16l5 7-13 16L3 12l5-7Z" fill={`url(#${g('violet')})`} />
          <path d="M3 12h26M16 28 11 12l5-7 5 7-5 16" fill="none" stroke="rgba(255,255,255,0.7)" strokeWidth="1" />
        </g>
      )}
      {name === 'play' && (
        <g><circle cx="16" cy="16" r="13" fill={`url(#${g('green')})`} stroke={stroke} strokeWidth="1.3" /><path d="M13 10l9 6-9 6V10Z" fill="#fff" /></g>
      )}
    </svg>
  )
}
