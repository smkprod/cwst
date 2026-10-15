import { useEffect, useState } from 'react'
import { api } from './api'
import type { AppConfig } from '../types'

/**
 * Конфиг приложения один раз на запуск: карточкам вроде «подпишись на канал» нужны его
 * отдельные поля, а спрашивать сервер каждой карточке незачем.
 */
let request: Promise<AppConfig | null> | null = null

function load(): Promise<AppConfig | null> {
  request ??= api.getAppConfig().catch(() => {
    request = null   // не дозвонились - следующий компонент попробует снова
    return null
  })
  return request
}

/** Канал бота без @; пустая строка - канал не подключён или конфиг ещё не пришёл. */
export function useChannelUsername(): string {
  const [value, setValue] = useState('')
  useEffect(() => {
    let alive = true
    load().then(cfg => { if (alive) setValue((cfg?.channelUsername ?? '').replace(/^@/, '')) })
    return () => { alive = false }
  }, [])
  return value
}
