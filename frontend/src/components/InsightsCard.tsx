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
}

/**
 * «Аналитика недели» (Pro). Вместо прежнего «здоровья клана» (одна цифра из
 * данных за день) — живая история недели: шанс победы, медали по дням из
 * официального лога, темп против прошлой недели и герои недели.
 */
export function InsightsCard({ insights, plan, players, dayLogs, warLog, race, periodType }: Props) {
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

  // Официальный по-дневный лог: только военные дни (3..6 → Д1..Д4)
  const warDays = dayLogs.filter(d => d.dayIndex >= 3).slice(-4)
  const maxDayPoints = Math.max(1, ...warDays.map(d => d.pointsEarned))

  // Темп против прошлой недели: итог прошлой vs текущий прогресс с поправкой на день.
  // «Сейчас» берём из гонки (общие медали клана, включая ушедших) — та же цифра,
  // что и в таблице «Гонка/Колизей», чтобы карточки не противоречили друг другу.
  const lastWeek = warLog.length > 0 ? warLog[0] : null
  const lastOurs = lastWeek?.standings.find(s => s.isOurClan) ?? null
  const currentFame = race.find(c => c.isOurClan)?.fame
    ?? players.reduce((sum, p) => sum + p.fame, 0)
  const daysElapsed = Math.min(4, Math.max(1, warDays.length + (periodType !== 'training' ? 1 : 0)))
  const expectedByNow = lastOurs ? Math.round(lastOurs.fame * (daysElapsed / 4)) : 0
  const pace: 'ahead' | 'behind' | 'same' | null = lastOurs && expectedByNow > 0
    ? currentFame > expectedByNow * 1.1 ? 'ahead'
      : currentFame < expectedByNow * 0.9 ? 'behind'
        : 'same'
    : null

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
          <span className="insights-sub">{t.insights.dayByDay}</span>
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

      {lastOurs && (
        <div className="pace-block">
          <span className="insights-sub">{t.insights.vsLastWeek}</span>
          <div className="pace-row">
            <span className="muted small">
              {t.insights.lastWeekFinal} {fmt(lastOurs.fame)} 🏅 · {lastOurs.rank}{t.insights.placeSuffix}
            </span>
            <span className="small">
              {t.insights.currentNow} <b>{fmt(currentFame)}</b> 🏅
            </span>
          </div>
          {pace && (
            <span className={`pace-chip ${pace === 'ahead' ? 'win-good' : pace === 'behind' ? 'win-bad' : 'win-mid'}`}>
              {pace === 'ahead' ? '📈 ' + t.insights.ahead : pace === 'behind' ? '📉 ' + t.insights.behind : '➡️ ' + t.insights.same}
            </span>
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
