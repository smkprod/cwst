import { useRef } from 'react'
import type { Translations } from '../lib/i18n'
import type { Challenge } from '../types'
import { Icon } from '../components/ui/Icon'
import { useFlip, useNow } from './data'

/** Таблица челленджа (общего или блогера), 520×720: приз, обратный отсчёт и топ-10 по билетам. */
export function ChallengeWidget({ data, t }: { data: Challenge; t: Translations }) {
  const now = useNow()
  const ev = data.event
  // Не «билеты» — очки: значок звезды, чтобы на стриме не обещать билеты за три короны
  const unit = (ev.rule ?? 'tickets') === 'tickets' ? 'ticket' : 'star'
  const rows = data.leaders.slice(0, 10)
  const ref = useRef<HTMLDivElement>(null)
  useFlip(ref, rows.map(r => `${r.tag}:${r.rank}`).join(','))

  // Статус пересчитываем по часам, а не ждём сервер: отсчёт, застывший на нуле
  // до следующего опроса, выглядит как зависший виджет.
  const start = Date.parse(ev.startUtc), end = Date.parse(ev.endUtc)
  const phase = now < start ? 'upcoming' : now < end ? 'live' : 'ended'

  return (
    <div className="ov-panel ov-board ov-ch">
      <div className="ov-board-head">
        <span className="ov-head-tile ov-tile-green"><Icon name="ticket" size={26} /></span>
        <div className="ov-head-text">
          <div className="ov-head-title">{ev.title || t.ch.title}</div>
          <div className="ov-board-meta"><Icon name="gift" size={15} /> {ev.prize || t.ch.defaultPrize}</div>
          {/* У челленджа блогера зрителю важно, чей он: в эфир может попасть и общий */}
          {ev.host && <div className="ov-board-meta ov-ch-host"><Icon name="user" size={15} /> {t.ch.host.replace('{name}', ev.host)}</div>}
        </div>
      </div>

      <div className={`ov-ch-timer ov-ch-${phase}`}>
        {phase === 'ended'
          ? <><Icon name="flag" size={18} /> {t.ch.ended}</>
          : <>
              {phase === 'live' ? <span className="ov-live-dot" /> : <Icon name="clock" size={18} />}
              <span className="ov-ch-timer-label">{phase === 'live' ? t.ch.endsIn : t.ch.startsIn}</span>
              <span className="ov-ch-timer-val">{countdown((phase === 'live' ? end : start) - now, t)}</span>
            </>}
      </div>

      {rows.length === 0
        ? <div className="ov-list-empty"><Icon name="ticket" size={26} /> {t.ch.empty}</div>
        : (
          <div className="ov-board-rows" ref={ref}>
            {rows.map(r => (
              <div key={r.tag} data-flip={r.tag} className={`ov-brow ov-brow-${r.rank <= 3 ? r.rank : 'n'}`}>
                <span className="ov-brow-rank">{r.rank}</span>
                <span className="ov-brow-text">
                  <span className="ov-brow-name">{r.name}</span>
                  <span className="ov-brow-sub">
                    {r.wins}–{r.losses}
                    {r.streak >= 2 && <span className="ov-brow-streak"><Icon name="flame" size={13} /> {r.streak}</span>}
                  </span>
                </span>
                <span className="ov-brow-stat">
                  <span key={r.tickets} className="ov-brow-big ov-brow-tickets ov-pop"><Icon name={unit} size={17} /> {r.tickets}</span>
                </span>
              </div>
            ))}
          </div>
        )}

      <div className="ov-board-foot">{t.ch.participants.replace('{n}', String(data.participants))}</div>
    </div>
  )
}

/** «1д 04:12:09» — дни словом, остальное часами: так отсчёт не прыгает по ширине. */
function countdown(ms: number, t: Translations): string {
  const s = Math.max(0, Math.floor(ms / 1000))
  const d = Math.floor(s / 86400)
  const pad = (n: number) => String(n).padStart(2, '0')
  const hms = `${pad(Math.floor(s % 86400 / 3600))}:${pad(Math.floor(s % 3600 / 60))}:${pad(s % 60)}`
  return d > 0 ? `${d}${t.ch.daysShort} ${hms}` : hms
}
