import { useEffect, useState } from 'react'
import { useT } from '../lib/i18n'

/**
 * Заставка на время загрузки.
 *
 * Фон намеренно тёмный в любой теме, а не по палитре Telegram: словесная часть
 * логотипа светло-серебристая и на светлом фоне просто пропала бы. Экран загрузки,
 * который темнее остального приложения, читается нормально — так устроены заставки в играх.
 *
 * Никакой искусственной задержки здесь нет: заставка живёт ровно столько, сколько
 * идёт запрос. Растягивать её ради красоты значило бы замедлять приложение.
 */
export function SplashScreen() {
  const { t } = useT()
  const [step, setStep] = useState(0)

  // Фразы сменяются, пока идёт загрузка. Обычно человек увидит только первую —
  // поэтому первой стоит та, что лучше всего объясняет, чего он ждёт.
  useEffect(() => {
    const id = window.setInterval(() => setStep(n => n + 1), 1400)
    return () => window.clearInterval(id)
  }, [])

  const phrases = t.splash.phrases

  return (
    <div className="splash" role="status" aria-label={t.loading}>
      <div className="splash-glow" />
      <img className="splash-logo" src="/clanify-logo.png" alt="Clanify" />
      <div className="splash-track">
        <div className="splash-fill" />
      </div>
      {/* key перезапускает анимацию появления на каждой новой фразе */}
      <p className="splash-phrase" key={step}>{phrases[step % phrases.length]}</p>
    </div>
  )
}
