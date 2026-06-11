import type { ClanStatus, RaceClan } from '../types'
import { fmt } from '../lib/format'

interface Props {
  race: RaceClan[]
  periodType: ClanStatus['periodType']
}

/** Ситуация в гонке: все кланы недели, как standings на cwstats/RoyaleAPI. */
export function RaceCard({ race, periodType }: Props) {
  if (!race || race.length === 0) return null

  const maxFame = Math.max(...race.map(c => Math.max(c.fame, 1)))

  return (
    <section className="card race-card">
      <div className="card-title-row">
        <div className="card-title">⛵ Гонка недели</div>
        {periodType === 'training' && <span className="muted small">тренировка — слава с четверга</span>}
        {periodType === 'colosseum' && <span className="trend-chip trend-onpace">Колизей</span>}
      </div>

      <ul className="race-list">
        {race.map(c => (
          <li key={c.tag} className={`race-row ${c.isOurClan ? 'race-ours' : ''}`}>
            <span className={`race-pos ${c.position === 1 ? 'race-pos-gold' : ''}`}>{c.position}</span>

            <div className="race-info">
              <span className="race-name">
                {c.name}
                {c.isOurClan && <span className="me-badge">мы</span>}
                {c.isFinished && ' 🏁'}
              </span>
              <div className="race-bar-track">
                <div
                  className={`race-bar-fill ${c.isOurClan ? 'race-bar-ours' : ''}`}
                  style={{ width: `${Math.max(3, Math.round((c.fame / maxFame) * 100))}%` }}
                />
              </div>
              {periodType !== 'training' && (
                <span className="muted small">
                  🃏 {c.decksUsedToday}/{c.maxDecksToday} сегодня · ⚡ {Math.round(c.avgFamePerAttack)}
                </span>
              )}
            </div>

            <div className="race-numbers">
              <span className="race-fame">{fmt(c.fame)}</span>
              {!c.isFinished && periodType !== 'training' && (
                <span className="race-projected" title="Прогноз к концу недели">
                  🔮 {fmt(c.projectedFame)}
                </span>
              )}
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
