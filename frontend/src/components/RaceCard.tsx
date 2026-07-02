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

export function RaceCard({ race, periodType }: Props) {
  const [selected, setSelected] = useState<RaceClan | null>(null)
  const { t } = useT()

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
        <div className="card-title">{t.race.title}</div>
        {periodType === 'training' && <span className="muted small">{t.race.trainingNote}</span>}
        {periodType === 'colosseum' && <span className="trend-chip trend-onpace">{t.period.colosseum}</span>}
      </div>

      <ul className="race-list">
        {race.map(c => {
          // Колизей: колоды/очки копятся всю неделю — показываем накопленные колоды и
          // среднее за неделю, без «сегодня/лимита» и без лодок. Обычная война — как было.
          const meta = [
            c.warTrophies > 0 ? `🏆 ${fmt(c.warTrophies)}` : null,
            c.isColosseum
              ? (c.decksUsed > 0 ? `🃏 ${fmt(c.decksUsed)}` : null)
              : (isWarDay ? `🃏 ${c.decksUsedToday}/${c.maxDecksToday}` : null),
            c.isColosseum
              ? (c.decksUsed > 0 ? `⚡ ${c.avgFamePerAttack.toFixed(1)}` : null)
              : (isWarDay && c.decksUsedToday > 0 ? `⚡ ${c.avgFamePerAttack.toFixed(1)}` : null),
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
                      цифру медалей вместо «паруса» и нулевого «за сегодня». */}
                  {periodType === 'colosseum' ? (
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

      {selected && <ClanWarLogModal clan={selected} onClose={() => setSelected(null)} />}
    </section>
  )
}
