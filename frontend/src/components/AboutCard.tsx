import type { Plan } from '../types'

interface Feature {
  icon: string
  title: string
  desc: string
  pro: boolean
}

const FEATURES: Feature[] = [
  { icon: '⚔️', title: 'Статус войны', desc: 'Кто сыграл 4/4, кто не доиграл, сколько времени до конца военного дня.', pro: false },
  { icon: '🏁', title: 'Гонка кланов', desc: 'Места и очки всех кланов недели, прогноз финала. Тап по клану — его прошлые войны.', pro: false },
  { icon: '🏆', title: 'Рейтинг недели и топ бота', desc: 'Кто больше всех набил за текущую неделю + глобальный топ игроков всех кланов.', pro: false },
  { icon: '🔮', title: 'Прогноз клана', desc: 'Сколько медалей наберёте за сегодня и к концу недели, с диапазоном и точностью.', pro: false },
  { icon: '📅', title: 'Прошлые войны и сезон', desc: 'Рейтинг игроков за каждую войну (W1, W2…) и суммарный зачёт за весь сезон.', pro: false },
  { icon: '📜', title: 'История по неделям', desc: 'Графики и динамика клана и лично твоя за прошлые недели войн.', pro: false },
  { icon: '👉', title: '«Пнуть» не сыгравших', desc: 'Разослать напоминание всем сразу одной кнопкой (Free — до 20 человек).', pro: false },
  { icon: '⏰', title: 'Автонапоминания', desc: 'Личное сообщение каждому не доигравшему за N часов до конца дня.', pro: true },
  { icon: '📊', title: 'Pro-аналитика', desc: 'Шанс победы в гонке, здоровье клана и архетип каждого игрока (Тащер, Надёжный…).', pro: true },
]

/** Гид по приложению: что умеет бот и чем отличается Pro от Free. */
export function AboutCard({ plan }: { plan: Plan }) {
  const isPro = plan === 'pro'
  return (
    <section className="card about-card">
      <div className="card-title-row">
        <div className="card-title">ℹ️ Как это работает</div>
        <span className={isPro ? 'pro-chip' : 'free-chip'}>{isPro ? 'PRO' : 'FREE'}</span>
      </div>

      <p className="muted small" style={{ margin: '0 0 12px' }}>
        Бот следит за Клановой Войной: 4 военных дня, у каждого игрока 4 колоды в день.
        Все цифры берутся из официального Clash Royale API, прогнозы — по истории прошлых войн.
      </p>

      <ul className="about-list">
        {FEATURES.map(f => (
          <li key={f.title} className={`about-item ${f.pro && !isPro ? 'about-locked' : ''}`}>
            <span className="about-icon">{f.icon}</span>
            <div className="about-text">
              <span className="about-title">
                {f.title}
                {f.pro && <span className="about-tag">{isPro ? '✓ Pro' : '🔒 Pro'}</span>}
              </span>
              <span className="muted small">{f.desc}</span>
            </div>
          </li>
        ))}
      </ul>

      {!isPro && (
        <p className="muted small about-upsell">
          🔒 Пункты с меткой Pro открываются на тарифе Pro. За подключением — к владельцу бота.
        </p>
      )}
    </section>
  )
}
