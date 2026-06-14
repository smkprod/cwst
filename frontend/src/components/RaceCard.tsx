import { useState } from 'react'
import type { ClanStatus, RaceClan } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { ClanWarLogModal } from './ClanWarLogModal'

interface Props {
  race: RaceClan[]
  periodType: ClanStatus['periodType']
}

/** Ситуация в гонке: все кланы недели, как standings на cwstats/RoyaleAPI. */
export function RaceCard({ race, periodType }: Props) {
  const [selected, setSelected] = useState<RaceClan | null>(null)

  if (!race || race.length === 0) return null

  const maxFame = Math.max(...race.map(c => Math.max(c.fame, 1)))
  const isWarDay = periodType === 'warDay' || periodType === 'colosseum'

  const open = (c: RaceClan) => {
    haptic('light')
    setSelected(c)
  }

  return (
    <section className="card race-card">
      <div className="card-title-row">
        <div className="card-title">⛵ Гонка недели</div>
        {periodType === 'training' && <span className="muted small">медали с четверга</span>}
        {periodType === 'colosseum' && <span className="trend-chip trend-onpace">Колизей</span>}
      </div>

      <ul className="race-list">
        {race.map(c => {
          // Мета-строка под прогресс-баром: трофеи · колоды · avg · лодка
          const meta = [
            c.warTrophies > 0 ? `🏆 ${fmt(c.warTrophies)}` : null,
            isWarDay ? `🃏 ${c.decksUsedToday}/${c.maxDecksToday}` : null,
            isWarDay && c.decksUsedToday > 0 ? `⚡ ${c.avgFamePerAttack.toFixed(1)}` : null,
            isWarDay && c.boatPoints > 0 ? `⛵ ${fmt(c.boatPoints)}` : null,
          ].filter(Boolean).join(' · ')

          return (
            <li key={c.tag}>
              <button className={`race-row ${c.isOurClan ? 'race-ours' : ''}`} onClick={() => open(c)}>
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
                  {meta && <span className="race-meta muted small">{meta}</span>}
                </div>

                <div className="race-numbers">
                  {isWarDay ? (
                    <>
                      <span className="race-fame race-fame-today" title="Медали за сегодня">
                        {fmt(c.todayFame)}
                      </span>
                      <span className="race-projected" title="Всего за неделю">
                        ∑ {fmt(c.fame)}
                      </span>
                      {!c.isFinished && c.projectedFame > 0 && (
                        <span className="race-projected" title="Прогноз к концу дня (avg × 200)">
                          → {fmt(c.projectedFame)}
                        </span>
                      )}
                    </>
                  ) : (
                    <span className="race-fame">{fmt(c.fame)}</span>
                  )}
                </div>
              </button>
            </li>
          )
        })}
      </ul>

      {selected && <ClanWarLogModal clan={selected} onClose={() => setSelected(null)} />}
    </section>
  )
}
