import { useRef } from 'react'
import type { Translations } from '../lib/i18n'
import type { Tournament, TournamentParticipant } from '../types'
import { Icon } from '../components/ui/Icon'
import { sideName, useFlip } from './data'
import { TourHead } from './TourHead'

/**
 * Участники турнира, 520×720. Пока идёт регистрация — кто записался; во время
 * турнира живые сверху, выбывшие уезжают вниз; после финала — пьедестал.
 */
export function ListWidget({ data, t }: { data: Tournament; t: Translations }) {
  const done = data.status === 'completed'
  const people = data.participants.filter(p => p.status !== 'withdrawn')

  const sorted = done
    ? [...people].sort((a, b) => (a.finalPlacement ?? 999) - (b.finalPlacement ?? 999) || a.seed - b.seed)
    : [...people].sort((a, b) => Number(a.status === 'eliminated') - Number(b.status === 'eliminated') || a.seed - b.seed || a.id - b.id)

  const podium = done ? [2, 1, 3].map(place => sorted.find(p => p.finalPlacement === place) ?? null) : []
  const rest = done ? sorted.filter(p => !p.finalPlacement || p.finalPlacement > 3) : sorted

  const listRef = useRef<HTMLDivElement>(null)
  useFlip(listRef, rest.map(p => `${p.id}:${p.status}`).join(','))

  const sub = done ? t.overlay.standings
    : data.status === 'registrationOpen' ? `${t.overlay.registration} · ${people.length}/${data.maxParticipants}`
    : `${t.overlay.inProgress} · ${people.filter(p => p.status === 'active').length}/${people.length}`

  // Плотность под число строк: одна колонка до 11, две до 22, дальше три — иначе
  // список в 64 человека просто не влезет в источник.
  const dense = rest.length > 22 ? 'ov-list-3' : rest.length > 11 ? 'ov-list-2' : 'ov-list-1'

  return (
    <div className="ov-panel ov-list">
      <TourHead data={data} t={t} compact />
      <div className="ov-list-sub"><Icon name={done ? 'medal' : 'users'} size={16} /> {sub}</div>

      {done && (
        <div className="ov-podium">
          {podium.map((p, i) => {
            const place = [2, 1, 3][i]
            return (
              <div key={place} className={`ov-pod ov-pod-${place}`}>
                {place === 1 && <span className="ov-pod-crown"><Icon name="crown" size={30} /></span>}
                <span className="ov-pod-name">{p ? sideName(p) : '—'}</span>
                <span className="ov-pod-block"><span className="ov-pod-num">{place}</span></span>
              </div>
            )
          })}
        </div>
      )}

      {people.length === 0
        ? <div className="ov-list-empty"><Icon name="hourglass" size={26} /> {t.overlay.noParticipants}</div>
        : (
          <div className={`ov-list-rows ${dense}`} ref={listRef}>
            {rest.map((p, i) => <Row key={p.id} p={p} n={done ? p.finalPlacement ?? i + 4 : i + 1} out={!done && p.status === 'eliminated'} />)}
          </div>
        )}
    </div>
  )
}

/** out — выбыл по ходу турнира. В итогах выбыли все, кроме чемпиона, — там не гасим. */
function Row({ p, n, out }: { p: TournamentParticipant; n: number; out: boolean }) {
  return (
    <div className={`ov-lrow ${out ? 'ov-lrow-out' : ''}`} data-flip={p.id}>
      <span className="ov-lrow-n">{n}</span>
      <span className="ov-lrow-text">
        <span className="ov-lrow-name">{sideName(p)}</span>
        {p.partnerPlayerName && <span className="ov-lrow-roster">{p.playerName} + {p.partnerPlayerName}</span>}
      </span>
      {out && <Icon name="x" size={16} className="ov-lrow-x" />}
    </div>
  )
}
