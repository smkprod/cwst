/** 12345 -> "12 345" */
export function fmt(n: number): string {
  return n.toLocaleString('ru-RU')
}

/** Сокращённый формат: 12345 -> "12.3к" */
export function fmtShort(n: number): string {
  if (Math.abs(n) >= 1000) return `${(n / 1000).toFixed(1).replace('.0', '')}к`
  return String(n)
}
