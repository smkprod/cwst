import { useT, type Lang } from '../lib/i18n'

const LABELS: Record<Lang, string> = { ru: 'RU', uk: 'UA', en: 'EN' }
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
          aria-pressed={lang === l}
        >
          {LABELS[l]}
        </button>
      ))}
    </div>
  )
}
