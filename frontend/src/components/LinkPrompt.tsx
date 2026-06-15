import { useT } from '../lib/i18n'

export function LinkPrompt() {
  const { t } = useT()
  return (
    <div className="center">
      <h2>{t.link.title}</h2>
      <p className="muted">{t.link.desc}</p>
      <code className="cmd">/link #ТВОЙ_ТЕГ</code>
      <p className="muted">{t.link.hint}</p>
    </div>
  )
}
