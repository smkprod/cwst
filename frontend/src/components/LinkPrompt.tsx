import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'

export function LinkPrompt() {
  const { t } = useT()
  return (
    <div className="center">
      <div className="lang-switcher-bar">
        <LangSwitcher />
      </div>
      <h2>{t.link.title}</h2>
      <p className="muted">{t.link.desc}</p>
      <code className="cmd">/link #ТВОЙ_ТЕГ</code>
      <p className="muted">{t.link.hint}</p>
    </div>
  )
}
