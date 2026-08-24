import type { ClanStatus, PlayerStatus, WarLogWeek } from '../types'

export interface WeekKing {
  playerTag: string
  name: string
  fame: number
  /** Война доиграна — первое место окончательное, а не «пока что». */
  final: boolean
}

/**
 * Кто первый по медалям за неделю.
 *
 * Хитрость в том, откуда брать данные. Пока идут военные дни, ответ прямо в составе:
 * участники несут медали текущей гонки. Но как только неделя доигрывается, CR открывает
 * новую гонку, и медали у всех обнуляются — если смотреть только на состав, король
 * исчезает ровно в тот момент, когда его надо короновать. Поэтому в тренировочные дни
 * победителя берём из журнала завершённых войн.
 *
 * Возвращает null, когда короля назвать не из чего: медалей ещё никто не набрал,
 * а завершённых войн в журнале нет.
 */
export function weekKing(
  players: PlayerStatus[],
  warLog: WarLogWeek[] | undefined,
  periodType: ClanStatus['periodType'],
): WeekKing | null {
  // Тренировочные дни идут ПЕРЕД военными, так что это всегда «прошлая война доиграна»
  const final = periodType === 'training'

  const top = players.reduce<PlayerStatus | null>(
    (best, p) => (best === null || p.fame > best.fame ? p : best), null)
  if (top !== null && top.fame > 0) {
    return { playerTag: top.playerTag, name: top.name, fame: top.fame, final }
  }

  if (!final) return null

  // Журнал отсортирован от свежих недель к старым, игроки внутри — по медалям
  const last = warLog?.[0]?.standings.find(s => s.isOurClan)?.players?.[0]
  return last && last.fame > 0
    ? { playerTag: last.playerTag, name: last.name, fame: last.fame, final: true }
    : null
}
