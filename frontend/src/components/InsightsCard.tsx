import type { ClanInsights, Plan, PlayerStatus, RaceClan, WarDayLog, WarLogWeek } from '../types'
import { fmt } from '../lib/format'
import { useT } from '../lib/i18n'

interface Props {
  insights: ClanInsights | null
  plan: Plan
  players: PlayerStatus[]
  dayLogs: WarDayLog[]
  warLog: WarLogWeek[]
  race: RaceClan[]
  periodType: 'training' | 'warDay' | 'colosseum'
  periodIndex: number   // 0..6 (нормализованный день недели гонки)
  hoursLeft: number     // часов до конца текущего дня
}

/**
 * «Аналитика недели» (Pro): темп против прошлой недели, медали по дням из
 * официального лога и герои недели.
 *
 * Отсюда убран «шанс победы». Он выглядел главной цифрой карточки, но был
 * бесполезен: проценты скакали сами по себе от чужих атак, а сделать с ними
 * было нечего. Разговор про конкретных людей теперь ведёт «Дисциплина клана».
 */
export function InsightsCard({ insights, plan, players, dayLogs, warLog, race, periodType, periodIndex, hoursLeft }: Props) {
  const { t } = useT()

  if (plan !== 'pro') {
    return (
      <section className="card forecast-locked">
        <div className="card-title-row">
          <div className="card-title">{t.insights.title}</div>
          <span className="pro-chip">PRO</span>
        </div>
        <p className="muted small">{t.insights.lockedNote}</p>
      </section>
    )
  }

  if (!insights) return null

  // Официальный по-дневный лог: только военные дни (3..6 → Д1..Д4).
  // CR может отдавать в логе дни ПРОШЛЫХ недель — предпочитаем текущую (weekOffset 0);
  // если её дней ещё нет, показываем последнюю доступную неделю с честной подписью.
  const warDayEntries = dayLogs.filter(d => d.dayIndex >= 3)
  const currentWeekDays = warDayEntries.filter(d => (d.weekOffset ?? 0) === 0)
  const shownIsCurrentWeek = currentWeekDays.length > 0
  const warDays = (shownIsCurrentWeek
    ? currentWeekDays
    : warDayEntries.filter(d => d.weekOffset === Math.min(...warDayEntries.map(x => x.weekOffset ?? 0)))
  ).slice(-4)
  const maxDayPoints = Math.max(1, ...warDays.map(d => d.pointsEarned))

  // Сравнение с прошлой неделей — БЕЗ «графика на сегодня»: сравниваем ПРОГНОЗ финиша
  // с итогом прошлой недели (то же число «→ прогноз», что и в таблице гонки). Это
  // честно и вперёд-смотряще: не «отстаёте», пока неделя не доиграна, а «финишируете
  // выше/ниже/вровень». Ту же цифру «сейчас» берём из гонки (медали всего клана).
  const lastWeek = warLog.length > 0 ? warLog[0] : null
  const lastOurs = lastWeek?.standings.find(s => s.isOurClan) ?? null
  const oursRace = race.find(c => c.isOurClan)
  const currentFame = oursRace?.fame ?? players.reduce((sum, p) => sum + p.fame, 0)
  const lastFame = lastOurs?.fame ?? 0
  const dayNumber = Math.min(4, Math.max(1, periodIndex - 2))          // 1..4
  const elapsed = Math.min(4, Math.max(0.25, dayNumber - 1 + Math.min(1, Math.max(0, (24 - hoursLeft) / 24))))

  // Прогноз финиша недели: колизей — из гонки (avg × колоды, копится всю неделю);
  // обычная война — экстраполяция текущего темпа на 4 дня (пока идёт неделя).
  const projectedFinal = periodType === 'colosseum'
    ? Math.max(oursRace?.projectedFame ?? 0, currentFame)
    : (elapsed >= 0.5 ? Math.round(currentFame / elapsed * 4) : 0)
  const finalDelta = projectedFinal - lastFame
  const outcome: 'above' | 'below' | 'even' | null =
    lastFame <= 0 || projectedFinal <= 0 ? null
      : Math.abs(finalDelta) <= lastFame * 0.03 ? 'even'
        : finalDelta > 0 ? 'above' : 'below'
  // Полоса: насколько уже приблизились к итогу прошлой недели
  const fillPct = lastFame > 0 ? Math.min(100, Math.round((currentFame / lastFame) * 100)) : 0

  const heroes = [...players].sort((a, b) => b.fame - a.fame).slice(0, 3).filter(p => p.fame > 0)
  const heroMedals = ['🥇', '🥈', '🥉']

  return (
    <section className="card insights-card">
      <div className="card-title-row">
        <div className="card-title">{t.insights.title}</div>
        <span className="pro-chip">PRO</span>
      </div>

      {/* На виду: прогноз финиша против прошлой недели (вперёд-смотрящее сравнение) */}
      {lastOurs && lastFame > 0 && (
        <div className="pace-block">
          <span className="insights-sub">{t.insights.vsLastWeek}</span>

          <div className="pace-track">
            <div
              className={`pace-fill ${outcome === 'below' ? 'pace-fill-bad' : 'pace-fill-good'}`}
              style={{ width: `${Math.max(2, fillPct)}%` }}
            />
          </div>
          <div className="pace-row">
            <span className="muted small">{t.insights.lastLabel} {fmt(lastFame)} 🏅 · {lastOurs.rank}{t.insights.placeSuffix}</span>
            <span className="small"><b>{fmt(currentFame)}</b> 🏅 {t.insights.nowLabel}</span>
          </div>

          {outcome && (
            <span className={`pace-chip ${outcome === 'above' ? 'win-good' : outcome === 'below' ? 'win-bad' : 'win-mid'}`}>
              🔮 {t.insights.projLabel} {fmt(projectedFinal)} 🏅 —{' '}
              {outcome === 'above' && <>{t.insights.finishAbove} {fmt(finalDelta)}</>}
              {outcome === 'below' && <>{t.insights.finishBelow} {fmt(-finalDelta)}</>}
              {outcome === 'even' && <>{t.insights.finishEven}</>}
            </span>
          )}
        </div>
      )}

      {/* Детали (медали по дням + герои недели) — под тап, чтобы не перегружать экран */}
      {(warDays.length > 0 || heroes.length > 0) && (
        <details className="insights-more">
          <summary>{t.insights.more}</summary>

          {warDays.length > 0 && (
            <div className="week-days-block">
              <span className="insights-sub">
                {shownIsCurrentWeek ? t.insights.dayByDay : t.insights.dayByDayLastWeek}
              </span>
              <div className="week-days">
                {warDays.map(d => (
                  <div key={d.dayIndex} className="week-day">
                    <div className="week-day-bar-track">
                      <div
                        className="week-day-bar"
                        style={{ height: `${Math.max(8, Math.round((d.pointsEarned / maxDayPoints) * 100))}%` }}
                      />
                    </div>
                    <span className="week-day-points small">{fmt(d.pointsEarned)}</span>
                    <span className="muted small">
                      {t.insights.dayShort}{d.dayIndex - 2} · {d.endOfDayRank}{t.insights.placeSuffix}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {heroes.length > 0 && (
            <div className="heroes-block">
              <span className="insights-sub">{t.insights.heroes}</span>
              {heroes.map((p, i) => (
                <div key={p.playerTag} className="hero-row">
                  <span>{heroMedals[i]}</span>
                  <span className="hero-name">{p.name}</span>
                  <span className="muted small">⚡ {p.avgFamePerAttack.toFixed(0)}</span>
                  <span className="hero-fame">{fmt(p.fame)} 🏅</span>
                </div>
              ))}
            </div>
          )}
        </details>
      )}
    </section>
  )
}
