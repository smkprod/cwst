import { useEffect, useState } from 'react'

export const REDUCED = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches

/** Число, которое докручивается до значения: цифра, выросшая на глазах, читается как «посчитано». */
export function useCountUp(target: number, run: boolean, ms = 900) {
  const [v, setV] = useState(REDUCED ? target : 0)
  useEffect(() => {
    if (REDUCED || !run) { setV(REDUCED ? target : 0); return }
    let raf = 0
    const start = performance.now()
    const tick = (now: number) => {
      const p = Math.min(1, (now - start) / ms)
      setV(target * (1 - Math.pow(1 - p, 3)))
      if (p < 1) raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [target, run, ms])
  return v
}

/** Анимация запускается, когда блок появился на экране, а не при открытии листа. */
export function useInView<T extends Element>() {
  const [el, setEl] = useState<T | null>(null)
  const [seen, setSeen] = useState(false)
  useEffect(() => {
    if (!el || seen) return
    if (typeof IntersectionObserver === 'undefined') { setSeen(true); return }
    const io = new IntersectionObserver(es => { if (es.some(e => e.isIntersecting)) setSeen(true) }, { threshold: 0.25 })
    io.observe(el)
    return () => io.disconnect()
  }, [el, seen])
  return [setEl, seen] as const
}
