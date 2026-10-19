import { api } from './api'

/**
 * «Увидел предложение» - шаг воронки покупки. Раз за запуск на товар и место: сервер и
 * так хранит одну строку на человека в день, а лишние запросы при каждом открытии окна
 * ни к чему.
 */
const sent = new Set<string>()

export function markOfferSeen(product: 'plus' | 'sponsor', place: 'sheet' | 'showcase' | 'tab') {
  const key = `${product}:${place}`
  if (sent.has(key)) return
  sent.add(key)
  api.trackOffer(product, place).catch(() => { sent.delete(key) })
}
