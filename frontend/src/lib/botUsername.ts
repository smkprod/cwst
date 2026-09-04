import { useEffect, useState } from 'react'
import { api } from './api'
import { getBotUsername, setBotUsername } from './telegram'

/**
 * Юзернейм бота для кнопок «пригласить» и «попросить подключить клан».
 *
 * Раньше он существовал только как переменная сборки, и если её забывали задать в CI,
 * компоненты молча возвращали null — кнопок в интерфейсе просто не было, без единой
 * ошибки в логах. Теперь значение из сборки лишь стартовое: когда его нет, спрашиваем
 * сервер, который своего бота знает всегда.
 *
 * Запрос уходит один на всё приложение: результат кладётся в общий модуль, а
 * подписчики получают его, когда он придёт.
 */

let request: Promise<void> | null = null
const subscribers = new Set<(value: string) => void>()

function load(): Promise<void> {
  request ??= api.getAppConfig()
    .then(cfg => {
      const name = (cfg.botUsername ?? '').trim()
      if (!name) return
      setBotUsername(name)
      subscribers.forEach(notify => notify(name))
    })
    .catch(() => {
      // Не дозвонились — пусть следующая попытка будет возможна
      request = null
    })
  return request
}

/**
 * Запустить загрузку, не дожидаясь компонента, которому юзернейм нужен.
 *
 * Раньше запрос уходил только из «пригласить» и «нет клана». На остальных экранах
 * юзернейм оставался пустым, и ссылка «поделиться» превращалась в голый t.me —
 * без бота, без payload, без смысла. Зовём один раз на старте приложения.
 */
export function ensureBotUsername(): Promise<void> {
  return getBotUsername() ? Promise.resolve() : load()
}

/** Юзернейм бота; пустая строка, пока он неизвестен. */
export function useBotUsername(): string {
  const [value, setValue] = useState(getBotUsername)

  useEffect(() => {
    if (getBotUsername()) return   // уже знаем — спрашивать незачем

    subscribers.add(setValue)
    void load()
    return () => { subscribers.delete(setValue) }
  }, [])

  return value
}
