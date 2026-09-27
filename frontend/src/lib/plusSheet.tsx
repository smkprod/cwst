import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { PlusSheet } from '../components/PlusSheet'
import { haptic } from './telegram'

/**
 * Окно «Clanify Плюс», открываемое из любого места: из замков в разборе, из «Ещё»,
 * по кнопке бота. Одно на приложение, как и карточка игрока.
 */
const OpenPlusContext = createContext<() => void>(() => {})

export function usePlusSheet() {
  return useContext(OpenPlusContext)
}

/**
 * Событие «Плюс изменился» — после покупки. Экраны, которые показывают замки,
 * слушают его и перечитывают себя: иначе человек, только что заплативший,
 * видел бы замок до перезапуска приложения.
 */
export const PLUS_CHANGED = 'clanify:plus-changed'

export function PlusSheetProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false)

  const show = useCallback(() => {
    haptic('light')
    setOpen(true)
  }, [])
  const value = useMemo(() => show, [show])

  return (
    <OpenPlusContext.Provider value={value}>
      {children}
      {open && <PlusSheet onClose={() => setOpen(false)} />}
    </OpenPlusContext.Provider>
  )
}
