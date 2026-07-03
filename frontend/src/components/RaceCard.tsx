import { useState } from 'react'
import type { ClanStatus, RaceClan } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { ClanWarLogModal } from './ClanWarLogModal'

interface Props {
  race: RaceClan[]
  periodType: ClanStatus['periodType']
}

/**
 * Два отдельных режима отображения:
 *  — обычная война (warDay/training): проверенный формат, НЕ меняем;
 *  — колизей: очки копятся все 4 дня без сброса, лодок нет — свой заголовок,
 *    подписи словами и легенда, чтобы низ карточки читался без догадок.
 */
export function RaceCard({ race, periodType }: Props) {
  const [selected, setSelected] = useState<RaceClan | null>(null)
  const { t } = useT()

  if (!race || race.length === 0) return null

  const maxFame = Math.max(...race.map(c => Math.max(c.fame, 1)))
  const isColosseum = periodType === 'colosseum'
  const isWarDay = periodType === 'warDay' || isColosseum

  const open = (c: RaceClan) => {
    haptic('light')
    setSelected(c)
  }

  return (
    <section className="card race-card">
      <div className="card-title-row">
        <div className="card-title">{isColosseum ? t.race.titleColosseum : t.race.title}</div>
        {periodType === 'training' && <span className="muted small">{t.race.trainingNote}</span>}
      </div>
      {isColosseum && <p className="muted small race-colosseum-note">{t.race.colosseumNote}</p>}

      <ul className="race-list">
        {race.map(c => {
          // Колизей: накопленные за неделю колоды и средние медали, с подписями словами.
          // Обычная война: компактный формат «сегодня/лимит», как было.
          const meta = isColosseum
            ? [
                c.warTrophies > 0 ? `🏆 ${fmt(c.warTrophies)}` : null,
                c.decksUsed > 0 ? `🃏 ${fmt(c.decksUsed)} ${t.race.decksWeekLabel}` : null,
                c.decksUsed > 0 ? `⚡ ${c.avgFamePerAttack.toFixed(1)} ${t.race.avgLabel}` : null,
              ].filter(Boolean).join(' · ')
            : [
                c.warTrophies > 0 ? `🏆 ${fmt(c.warTrophies)}` : null,
                isWarDay ? `🃏 ${c.decksUsedToday}/${c.maxDecksToday}` : null,
                isWarDay && c.decksUsedToday > 0 ? `⚡ ${c.avgFamePerAttack.toFixed(1)}` : null,
              ].filter(Boolean).join(' · ')

          return (
            <li key={c.tag}>
              <button className={`race-row ${c.isOurClan ? 'race-ours' : ''}`} onClick={() => open(c)}>
                <span className={`race-pos ${c.position === 1 ? 'race-pos-gold' : ''}`}>{c.position}</span>

                <div className="race-info">
                  <span className="race-name">
                    {c.name}
                    {c.isOurClan && <span className="me-badge">{t.race.ours}</span>}
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
                  {/* Колизей: лодок нет — clan.fame из API это те же накопленные медали
                      (дублирует ∑), а periodPoints CR не заполняет. Показываем одну
                      цифру медалей недели и прогноз к финалу. */}
                  {isColosseum ? (
                    <>
                      <span className="race-fame race-fame-today" title={t.race.totalTitle}>
                        {fmt(c.fame)} 🏅
                      </span>
                      {!c.isFinished && c.projectedFame > 0 && (
                        <span className="race-projected" title={t.race.projTitle}>
                          → {fmt(c.projectedFame)}
                        </span>
                      )}
                    </>
                  ) : isWarDay ? (
                    <>
                      <span className="race-fame race-fame-today" title={t.race.todayTitle}>
                        {fmt(c.todayFame)} 🏅
                      </span>
                      {c.boatPoints > 0 && (
                        <span className="race-projected" title={t.race.boatTitle}>
                          ⛵ {fmt(c.boatPoints)}
                        </span>
                      )}
                      <span className="race-projected" title={t.race.totalTitle}>
                        ∑ {fmt(c.fame)}
                      </span>
                      {!c.isFinished && c.projectedFame > 0 && (
                        <span className="race-projected" title={t.race.projTitle}>
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

      {isColosseum && <p className="muted small race-colosseum-legend">{t.race.colosseumLegend}</p>}

      {selected && <ClanWarLogModal clan={selected} onClose={() => setSelected(null)} />}
    </section>
  )
}
