import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { Showcase, type ShowcaseTab } from '../components/Showcase'
import { haptic } from './telegram'

/**
 * Витрина Плюса и спонсорства - одна на приложение и открывается отовсюду: «Ещё»,
 * окно покупки («Как это выглядит»), ссылка startapp=showcase. Сама страница рисуется
 * в ShowcaseHost - внутри провайдера окна покупки, чтобы её кнопки «Подключить» могли
 * его открыть, а окно покупки - открыть витрину.
 */
interface Ctx { tab: ShowcaseTab | null; open: (tab?: ShowcaseTab) => void; close: () => void }

const ShowcaseContext = createContext<Ctx>({ tab: null, open: () => {}, close: () => {} })

export function ShowcaseProvider({ children }: { children: ReactNode }) {
  const [tab, setTab] = useState<ShowcaseTab | null>(null)
  const open = useCallback((next: ShowcaseTab = 'plus') => { haptic('light'); setTab(next) }, [])
  const close = useCallback(() => setTab(null), [])
  const value = useMemo(() => ({ tab, open, close }), [tab, open, close])
  return <ShowcaseContext.Provider value={value}>{children}</ShowcaseContext.Provider>
}

export function useShowcase() {
  return useContext(ShowcaseContext).open
}

export function ShowcaseHost() {
  const { tab, close } = useContext(ShowcaseContext)
  return tab ? <Showcase initialTab={tab} onClose={close} /> : null
}
