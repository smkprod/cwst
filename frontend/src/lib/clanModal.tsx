import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { ClanModal } from '../components/ClanModal'
import { haptic } from './telegram'

/**
 * Открытие страницы чужого клана из любого места приложения.
 *
 * Кланы перечисляются в полудюжине мест: гонка недели, топ страны, мировой рейтинг,
 * журнал войн, разведка. Держать в каждом своё состояние модалки и прокидывать его
 * через промежуточные компоненты — это шесть одинаковых кусков кода, которые
 * разойдутся при первой же правке. Модалка одна на приложение, открывается вызовом.
 */
const OpenClanContext = createContext<(tag: string, name?: string) => void>(() => {})

/** Открыть страницу клана по тегу. Пустой тег игнорируется — кликать не по чему. */
export function useOpenClan() {
  return useContext(OpenClanContext)
}

export function ClanModalProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<{ tag: string; name?: string } | null>(null)

  const open = useCallback((tag: string, name?: string) => {
    // Старые ответы сервера могут прийти без тега: молча ничего не делаем,
    // это лучше, чем открыть пустую страницу и показать ошибку.
    if (!tag) return
    haptic('light')
    setTarget({ tag, name })
  }, [])

  // Значение не пересоздаём: иначе каждый рендер провайдера перерисовывал бы
  // всех подписчиков, а это половина экранов приложения.
  const value = useMemo(() => open, [open])

  return (
    <OpenClanContext.Provider value={value}>
      {children}
      {target !== null && (
        <ClanModal tag={target.tag} name={target.name} onClose={() => setTarget(null)} />
      )}
    </OpenClanContext.Provider>
  )
}
