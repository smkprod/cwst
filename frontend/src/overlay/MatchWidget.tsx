import type { Translations } from '../lib/i18n'
import type { Tournament } from '../types'
import { Icon } from '../components/ui/Icon'
import { boLabel, champion, currentMatch, isLive, roundName, sideName, winsNeeded } from './data'
import { OvChip } from './TourHead'

/**
 * «Текущий матч» — плашка внизу экрана, 900×220: кто играет, счёт серии, раунд и
 * формат. То, что зритель, зашедший посреди стрима, должен понять за секунду.
 */
export function MatchWidget({ data, t }: { data: Tournament; t: Translations }) {
  const m = currentMatch(data)
  const champ = champion(data)
  const maxRound = Math.max(0, ...data.matches.map(x => x.round))
  const mode = data.gameMode ? t.duel.modes[data.gameMode] ?? data.gameMode : null

  if (!m) {
    return (
      <div className={`ov-panel ov-match ov-match-idle ${champ ? 'ov-match-champ' : ''}`}>
        <div className="ov-match-top">
          <span className="ov-match-tour"><Icon name="trophy" size={18} /> {data.name}</span>
          {mode && <OvChip icon="swords" tone="violet">{mode}</OvChip>}
        </div>
        {champ ? (
          <div className="ov-match-banner">
            <span className="ov-match-crown"><Icon name="crown" size={44} /></span>
            <span className="ov-match-banner-text">
              <span className="ov-match-banner-label">{t.overlay.champion}</span>
              <span className="ov-match-banner-name">{sideName(champ)}</span>
            </span>
          </div>
        ) : (
          <div className="ov-match-banner ov-match-wait">
            <span className="ov-match-hg"><Icon name="hourglass" size={34} /></span>
            <span className="ov-match-banner-name">{t.overlay.waiting}</span>
          </div>
        )}
      </div>
    )
  }

  const live = isLive(m)
  const wins = winsNeeded(data, m)
  return (
    <div className={`ov-panel ov-match ${live ? 'ov-match-live' : ''}`}>
      <div className="ov-match-top">
        <span className="ov-match-tour"><Icon name="trophy" size={18} /> {data.name}</span>
        <span className="ov-match-chips">
          {live
            ? <OvChip tone="live"><span className="ov-live-dot" />{t.overlay.live}</OvChip>
            : <OvChip icon="clock" tone="blue">{t.overlay.next}</OvChip>}
          <OvChip tone="gold">{roundName(m.round, maxRound, t)}</OvChip>
          <OvChip tone="gray">{boLabel(wins)}</OvChip>
          {mode && <OvChip icon="swords" tone="violet">{mode}</OvChip>}
        </span>
      </div>

      <div className="ov-match-main">
        <div className="ov-match-side ov-match-a">
          <span className="ov-match-seed">{m.participantA?.seed}</span>
          <span className="ov-match-who">
            <span className="ov-match-name">{sideName(m.participantA)}</span>
            {/* Сколько побед до конца серии — точками, как в спортивных трансляциях */}
            <Pips n={wins} filled={m.scoreA} />
          </span>
        </div>
        <div className="ov-match-score">
          <span key={`a${m.scoreA}`} className={`ov-match-num ov-pop ${m.scoreA > m.scoreB ? 'ov-match-lead' : ''}`}>{m.scoreA}</span>
          <span className="ov-match-colon">:</span>
          <span key={`b${m.scoreB}`} className={`ov-match-num ov-pop ${m.scoreB > m.scoreA ? 'ov-match-lead' : ''}`}>{m.scoreB}</span>
        </div>
        <div className="ov-match-side ov-match-b">
          <span className="ov-match-who">
            <span className="ov-match-name">{sideName(m.participantB)}</span>
            <Pips n={wins} filled={m.scoreB} />
          </span>
          <span className="ov-match-seed">{m.participantB?.seed}</span>
        </div>
      </div>
    </div>
  )
}

function Pips({ n, filled }: { n: number; filled: number }) {
  return (
    <span className="ov-pips">
      {Array.from({ length: n }, (_, i) => <span key={i} className={`ov-pip ${i < filled ? 'ov-pip-on' : ''}`} />)}
    </span>
  )
}
