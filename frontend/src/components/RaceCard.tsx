import type { ClanStatus, RaceClan } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'
import { useOpenClan } from '../lib/clanModal'
import type { ReactNode } from 'react'
import { Icon, type IconName } from './ui/Icon'
import { SectionHead } from './ui/Section'

/** Кусочек строки «иконка + число». */
function Meta({ icon, children }: { icon: IconName; children: ReactNode }) {
  return <span className="cl-ic"><Icon name={icon} size={12} />{children}</span>
}

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
  const openClan = useOpenClan()
  const { t } = useT()

  if (!race || race.length === 0) return null

  const maxFame = Math.max(...race.map(c => Math.max(c.fame, 1)))
  const isColosseum = periodType === 'colosseum'
  const isWarDay = periodType === 'warDay' || isColosseum

  return (
    <section className="card race-card">
      <SectionHead
        icon={isColosseum ? 'columns' : 'flag'}
        tone={isColosseum ? 'violet' : 'orange'}
        title={isColosseum ? t.race.titleColosseum : t.race.title}
        info={isColosseum ? <>
          <p>{t.race.colosseumNote}</p>
          <p><Meta icon="medal">{t.race.colosseumLegend}</Meta></p>
        </> : isWarDay ? (
          <ul>
            <li><Meta icon="medal">{t.race.todayTitle}</Meta></li>
            <li><Meta icon="anchor">{t.race.boatTitle}</Meta></li>
            <li>∑ {t.race.totalTitle}</li>
            <li>→ {t.race.projTitle}</li>
          </ul>
        ) : undefined}
        aside={periodType === 'training' ? t.race.trainingNote : undefined}
      />

      <ul className="race-list">
        {race.map(c => {
          // Колизей: накопленные за неделю колоды и средние медали, с подписями словами.
          // Обычная война: компактный формат «сегодня/лимит», как было.
          const parts: ReactNode[] = isColosseum
            ? [
                c.warTrophies > 0 ? <Meta key="tr" icon="trophy">{fmt(c.warTrophies)}</Meta> : null,
                c.decksUsed > 0 ? <Meta key="dk" icon="cards">{fmt(c.decksUsed)} {t.race.decksWeekLabel}</Meta> : null,
                c.decksUsed > 0 ? <Meta key="av" icon="bolt">{c.avgFamePerAttack.toFixed(1)} {t.race.avgLabel}</Meta> : null,
              ].filter(Boolean)
            : [
                c.warTrophies > 0 ? <Meta key="tr" icon="trophy">{fmt(c.warTrophies)}</Meta> : null,
                isWarDay ? <Meta key="dk" icon="cards">{c.decksUsedToday}/{c.maxDecksToday}</Meta> : null,
                isWarDay && c.decksUsedToday > 0 ? <Meta key="av" icon="bolt">{c.avgFamePerAttack.toFixed(1)}</Meta> : null,
              ].filter(Boolean)
          const meta = parts.length > 0
            ? parts.map((p, i) => <span key={i} className="cl-ic">{i > 0 && <span className="cl-sep">·</span>}{p}</span>)
            : null

          return (
            <li key={c.tag}>
              <button className={`race-row ${c.isOurClan ? 'race-ours' : ''}`} onClick={() => openClan(c.tag, c.name)}>
                <span className={`race-pos ${c.position === 1 ? 'race-pos-gold' : ''}`}>{c.position}</span>

                <div className="race-info">
                  <span className="race-name">
                    {c.name}
                    {c.isOurClan && <span className="me-badge">{t.race.ours}</span>}
                    {c.isFinished && <Icon name="flag" size={13} className="cl-ok" style={{ marginLeft: 4 }} />}
                  </span>
                  <div className="race-bar-track">
                    <div
                      className={`race-bar-fill ${c.isOurClan ? 'race-bar-ours' : ''}`}
                      style={{ width: `${Math.max(3, Math.round((c.fame / maxFame) * 100))}%` }}
                    />
                  </div>
                  {meta && <span className="race-meta muted small cl-meta">{meta}</span>}
                </div>

                <div className="race-numbers">
                  {/* Колизей: лодок нет — clan.fame из API это те же накопленные медали
                      (дублирует ∑), а periodPoints CR не заполняет. Показываем одну
                      цифру медалей недели и прогноз к финалу. */}
                  {isColosseum ? (
                    <>
                      <span className="race-fame race-fame-today" title={t.race.totalTitle}>
                        <span className="cl-ic">{fmt(c.fame)} <Icon name="medal" size={13} className="cl-fame" /></span>
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
                        <span className="cl-ic">{fmt(c.todayFame)} <Icon name="medal" size={13} className="cl-fame" /></span>
                      </span>
                      {c.boatPoints > 0 && (
                        <span className="race-projected" title={t.race.boatTitle}>
                          <span className="cl-ic"><Icon name="anchor" size={11} />{fmt(c.boatPoints)}</span>
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

    </section>
  )
}
