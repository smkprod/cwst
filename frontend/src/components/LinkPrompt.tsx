import { useEffect, useState } from 'react'
import { api, ApiError } from '../lib/api'
import { haptic, hapticNotify, startParam } from '../lib/telegram'
import type { LinkCandidates } from '../types'
import { useT } from '../lib/i18n'
import { LangSwitcher } from './LangSwitcher'
import { BotTourCard } from './BotTourCard'
import { SectionHead } from './ui/Section'

/**
 * Первый экран человека, который ещё не привязал себя.
 *
 * Раньше здесь была нарисована команда /link #ТВОЙ_ТЕГ и всё: чтобы привязаться,
 * надо было закрыть приложение, найти чат с ботом и набрать её руками. Половина
 * на этом и заканчивала. Теперь поле и кнопка прямо тут, приложение закрывать не надо.
 *
 * Под формой короткий экскурс: человек, который ещё не привязался, не видит ни одного
 * рабочего экрана и иначе не узнает, ради чего вообще вводить тег.
 */
/** Пришёл по кнопке «Привязать аккаунт» из чата клана: startapp=link_ТЕГ_КЛАНА. */
const linkClanTag: string | null = (() => {
  const m = /^link_([A-Z0-9]{3,15})$/i.exec(startParam)
  return m ? m[1].toUpperCase() : null
})()

export function LinkPrompt({ onLinked }: { onLinked?: () => void }) {
  const { t } = useT()
  const [tag, setTag] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Список непривязанных игроков клана: выбрать себя проще, чем вспомнить тег
  const [candidates, setCandidates] = useState<LinkCandidates | null>(null)
  const [showList, setShowList] = useState(true)

  useEffect(() => {
    if (!linkClanTag) return
    api.getLinkCandidates(linkClanTag).then(setCandidates).catch(() => { /* тогда просто поле для тега */ })
  }, [])

  const submit = async (picked?: string) => {
    const clean = (picked ?? tag).trim()
    if (clean.length < 3) return

    haptic('medium')
    setBusy(true)
    setError(null)
    try {
      await api.linkMe(clean)
      hapticNotify('success')
      // Перезагружаем целиком: привязка меняет всё, что показывает приложение,
      // и точечно обновлять тут нечего.
      if (onLinked) onLinked()
      else window.location.reload()
    } catch (e) {
      hapticNotify('error')
      setError(e instanceof ApiError && e.code === 'player_not_found'
        ? t.link.notFound
        : t.link.error)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fade-in">
      <div className="lang-switcher-bar">
        <LangSwitcher />
      </div>

      {candidates && showList && (
        <section className="card link-card">
          <SectionHead icon="users" tone="violet" title={t.engage.pickTitle} />
          <p className="muted small link-desc">{t.engage.pickHint.replace('{clan}', candidates.clanName)}</p>
          {candidates.members.length === 0
            ? <p className="muted small">{t.engage.pickEmpty}</p>
            : (
              <div className="link-pick-list">
                {candidates.members.map(m => (
                  <button key={m.tag} className="link-pick" disabled={busy} onClick={() => { haptic('light'); submit(m.tag) }}>
                    <span className="link-pick-name">{m.name}</span>
                    <span className="muted small">{m.tag} · 🏆 {m.trophies}</span>
                  </button>
                ))}
              </div>
            )}
          {error && <p className="form-error small">{error}</p>}
          {candidates.members.length > 0 && (
            <button className="btn-mini" style={{ marginTop: 8 }} onClick={() => { haptic('light'); setShowList(false) }}>
              {t.engage.pickOther}
            </button>
          )}
        </section>
      )}

      <section className="card link-card">
        <SectionHead icon="link" tone="violet" title={t.link.title} info={t.link.hint} />
        <p className="muted small link-desc">{t.link.descApp}</p>

        <input
          className="search-input link-input"
          value={tag}
          onChange={e => setTag(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') submit() }}
          placeholder="#ABC123"
          autoCapitalize="characters"
          autoCorrect="off"
          spellCheck={false}
          maxLength={16}
        />

        {error && <p className="form-error small">{error}</p>}

        <button className="btn link-btn" disabled={busy || tag.trim().length < 3} onClick={() => submit()}>
          {busy ? t.link.linking : t.link.submit}
        </button>
      </section>

      <BotTourCard />
    </div>
  )
}
