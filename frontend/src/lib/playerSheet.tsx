import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { PlayerStatus } from '../types'
import { PlayerSheetModal, type SheetTab } from '../components/PlayerSheet'
import { haptic } from './telegram'

/**
 * Открытие единой карточки игрока из любого места приложения.
 *
 * Раньше у поиска, КВ, мирового топа и Аллеи были свои карточки, и один и тот же
 * человек выглядел в них по-разному. Теперь карточка одна и открывается вызовом.
 */
export interface SheetOptions {
  /** Строка КВ, если карточку открыли из списка клана: она уже есть, дозапрашивать незачем. */
  warRow?: PlayerStatus
  isMe?: boolean
  /** Лидер или админ группы — показываем приглашение в бота. */
  canManage?: boolean
  tab?: SheetTab
}

type Open = (tag: string, options?: SheetOptions) => void

const OpenSheetContext = createContext<Open>(() => {})

export function usePlayerSheet() {
  return useContext(OpenSheetContext)
}

export function PlayerSheetProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<{ tag: string; options: SheetOptions } | null>(null)

  const open = useCallback<Open>((tag, options = {}) => {
    if (!tag) return
    haptic('light')
    setTarget({ tag, options })
  }, [])

  const value = useMemo(() => open, [open])

  return (
    <OpenSheetContext.Provider value={value}>
      {children}
      {target !== null && (
        <PlayerSheetModal
          key={target.tag}
          tag={target.tag}
          {...target.options}
          onOpenPlayer={(tag) => open(tag)}
          onClose={() => setTarget(null)}
        />
      )}
    </OpenSheetContext.Provider>
  )
}
