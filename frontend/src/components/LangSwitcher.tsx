import { useT, type Lang } from '../lib/i18n'

const FLAGS: Record<Lang, string> = { ru: '🇷🇺', uk: '🇺🇦', en: '🇬🇧' }
const LANGS: Lang[] = ['ru', 'uk', 'en']

export function LangSwitcher() {
  const { lang, setLang } = useT()
  return (
    <div className="lang-switcher">
      {LANGS.map(l => (
        <button
          key={l}
          className={`lang-btn ${lang === l ? 'lang-btn-active' : ''}`}
          onClick={() => setLang(l)}
          aria-label={l.toUpperCase()}
          aria-pressed={lang === l}
        >
          {FLAGS[l]}
        </button>
      ))}
    </div>
  )
}
