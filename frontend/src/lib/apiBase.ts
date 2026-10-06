/**
 * Откуда ходить в API.
 *
 * В проде фронт раздаёт тот же сервер, поэтому адрес пустой — запросы идут на свой
 * домен. В разработке Vite крутится отдельно, а API — на localhost:5000.
 *
 * Отдельный модуль, а не константа внутри api.ts: виджетам OBS нужен тот же адрес,
 * но api.ts тянет за собой Telegram и заголовки входа, которых в OBS нет.
 */
export const API_BASE: string = import.meta.env.DEV
  ? (import.meta.env.VITE_API_URL ?? 'http://localhost:5000')
  : ''
