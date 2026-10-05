/**
 * Убирает эмодзи из строки. Нужен там, где текст приходит готовым (с сервера,
 * из старых переводов), а интерфейс рисует иконки сам.
 */
const EMOJI = /[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}\u{2B00}-\u{2BFF}\u{2300}-\u{23FF}\u{3030}\u{303D}\u{3297}\u{3299}\u{FE0F}\u{200D}\u{20E3}\u{E0020}-\u{E007F}]/gu

export function noEmoji(s: string): string {
  // ★ оставляем: это знак звёзд Telegram в ценах, а не картинка
  const out = s.replace(EMOJI, ch => (ch === '★' ? ch : '')).replace(/\s{2,}/g, ' ').replace(/^[\s·•—-]+/, '').trim()
  return out.length > 0 ? out : s
}
