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
 * «Аналитика недели» (Pro). Вместо прежнего «здоровья клана» (одна цифра из
 * данных за день) — живая история недели: шанс победы, медали по дням из
 * официального лога, темп против прошлой недели и герои недели.
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

  const { winChance, winChanceIfSlackersOut, topRivalName } = insights
  const drop = winChance !== null && winChanceIfSlackersOut !== null
    ? winChance - winChanceIfSlackersOut
    : 0

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

  // Темп против прошлой недели. «Сейчас» — из гонки (общие медали клана, включая ушедших),
  // та же цифра, что и в таблице «Гонка/Колизей». Прошедшее время считаем от номера дня и
  // часов до конца (НЕ от лога — он у CR ненадёжен), поэтому сравнение честное:
  // день 3 из 4 → ждём ~68% от итога прошлой недели, а не четверть.
  const lastWeek = warLog.length > 0 ? warLog[0] : null
  const lastOurs = lastWeek?.standings.find(s => s.isOurClan) ?? null
  const currentFame = race.find(c => c.isOurClan)?.fame
    ?? players.reduce((sum, p) => sum + p.fame, 0)
  const dayNumber = Math.min(4, Math.max(1, periodIndex - 2))           // 1..4
  const dayFraction = Math.min(1, Math.max(0, (24 - hoursLeft) / 24))   // сколько сегодняшнего дня прошло
  const elapsed = Math.min(4, dayNumber - 1 + dayFraction)              // военных дней прошло (дробно)
  const lastFame = lastOurs?.fame ?? 0
  const expectedByNow = lastFame > 0 ? Math.round(lastFame * (elapsed / 4)) : 0
  const delta = currentFame - expectedByNow
  const pace: 'ahead' | 'behind' | 'same' | null = lastFame > 0 && expectedByNow > 0
    ? Math.abs(delta) <= expectedByNow * 0.05 ? 'same'
      : delta > 0 ? 'ahead' : 'behind'
    : null
  // Цель: сколько медалей в день нужно до конца недели, чтобы побить прошлую
  const daysRemaining = Math.max(0, 4 - elapsed)
  const alreadyBeaten = lastFame > 0 && currentFame >= lastFame
  const needPerDay = !alreadyBeaten && lastFame > 0 && daysRemaining > 0.1
    ? Math.ceil((lastFame - currentFame) / daysRemaining)
    : null
  const currentPerDay = elapsed > 0.1 ? Math.round(currentFame / elapsed) : null
  // Прогресс-бар: заливка = сейчас/прошлая, метка — где должны быть по графику
  const fillPct = lastFame > 0 ? Math.min(100, Math.round((currentFame / lastFame) * 100)) : 0
  const tickPct = lastFame > 0 ? Math.min(100, Math.round((expectedByNow / lastFame) * 100)) : 0

  const heroes = [...players].sort((a, b) => b.fame - a.fame).slice(0, 3).filter(p => p.fame > 0)
  const heroMedals = ['🥇', '🥈', '🥉']

  return (
    <section className="card insights-card">
      <div className="card-title-row">
        <div className="card-title">{t.insights.title}</div>
        <span className="pro-chip">PRO</span>
      </div>

      {winChance !== null && (
        <div className="win-block">
          <div className="win-row">
            <span className={`win-value ${winChance >= 60 ? 'win-good' : winChance >= 40 ? 'win-mid' : 'win-bad'}`}>
              {winChance}%
            </span>
            <div className="win-info">
              <span className="win-caption">{t.insights.winCaption}</span>
              {topRivalName && <span className="muted small">{t.insights.topRival} {topRivalName}</span>}
            </div>
          </div>
          <div className="win-track">
            <div
              className={`win-fill ${winChance >= 60 ? 'fill-good' : winChance >= 40 ? 'fill-mid' : 'fill-bad'}`}
              style={{ width: `${winChance}%` }}
            />
          </div>
          {drop >= 5 && winChanceIfSlackersOut !== null && (
            <p className="win-warning">
              ⚠️ {t.insights.slackersWarn1}<strong>{winChanceIfSlackersOut}%</strong>{t.insights.slackersWarn2}
            </p>
          )}
        </div>
      )}

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

      {lastOurs && lastFame > 0 && (
        <div className="pace-block">
          <span className="insights-sub">{t.insights.vsLastWeek}</span>

          {/* Прогресс к итогу прошлой недели; метка — где нужно быть прямо сейчас */}
          <div className="pace-track">
            <div
              className={`pace-fill ${pace === 'behind' ? 'pace-fill-bad' : 'pace-fill-good'}`}
              style={{ width: `${Math.max(2, fillPct)}%` }}
            />
            <div className="pace-tick" style={{ left: `${tickPct}%` }} />
          </div>
          <div className="pace-row">
            <span className="small">
              <b>{fmt(currentFame)}</b> 🏅 <span className="muted">/ {fmt(lastFame)}</span>
            </span>
            <span className="muted small">
              {t.insights.scheduleLabel} {fmt(expectedByNow)} · {t.insights.dayLabel} {dayNumber}/4
            </span>
          </div>

          {pace && (
            <span className={`pace-chip ${pace === 'ahead' ? 'win-good' : pace === 'behind' ? 'win-bad' : 'win-mid'}`}>
              {pace === 'ahead' && <>📈 {t.insights.aheadBy} <b>{fmt(delta)}</b> 🏅</>}
              {pace === 'behind' && <>📉 {t.insights.behindBy} <b>{fmt(-delta)}</b> 🏅</>}
              {pace === 'same' && <>➡️ {t.insights.onSchedule}</>}
            </span>
          )}

          {/* Практичная цель: сколько нужно в день, чтобы побить прошлую неделю */}
          {alreadyBeaten ? (
            <p className="pace-goal small">🎉 {t.insights.beaten}</p>
          ) : needPerDay !== null && (
            <p className="pace-goal small">
              🎯 {t.insights.goal1} <b>{fmt(needPerDay)}</b> 🏅{t.insights.goal2}
              {currentPerDay !== null && <span className="muted"> · {t.insights.paceNow} ~{fmt(currentPerDay)}</span>}
            </p>
          )}
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
    </section>
  )
}
