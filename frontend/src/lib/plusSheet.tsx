import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { PlusSheet, type PlusFocus } from '../components/PlusSheet'
import { haptic } from './telegram'

/**
 * Окно покупки — одно на приложение: Плюс и Спонсор в нём рядом, как одна линейка.
 * Открывается из замков разбора, «Стоп-тильта», «Ещё», Аллеи и по кнопке бота.
 */
const OpenPlusContext = createContext<(focus?: PlusFocus) => void>(() => {})

export function usePlusSheet() {
  return useContext(OpenPlusContext)
}

/**
 * Событие «Плюс или спонсорство изменились» — после покупки. Экраны с замками и
 * статусом слушают его и перечитывают себя: иначе только что заплативший видел бы
 * замок до перезапуска приложения.
 */
export const PLUS_CHANGED = 'clanify:plus-changed'

export function PlusSheetProvider({ children }: { children: ReactNode }) {
  const [focus, setFocus] = useState<PlusFocus | null>(null)

  const show = useCallback((next: PlusFocus = 'plus') => {
    haptic('light')
    setFocus(next)
  }, [])
  const value = useMemo(() => show, [show])

  return (
    <OpenPlusContext.Provider value={value}>
      {children}
      {focus && <PlusSheet focus={focus} onClose={() => setFocus(null)} />}
    </OpenPlusContext.Provider>
  )
}
