import type { ServiceIdentity } from '../types'

/**
 * Кому какие служебные вкладки. Сервер всё равно проверяет права сам — здесь
 * только про то, чтобы не показывать вкладку, которая ответит отказом.
 */

/** «Студия»: блогеру по праву, владельцу — всегда (у него есть все права). */
export function canUseStudio(me: ServiceIdentity): boolean {
  return me.role === 'owner' || me.permissions.includes('Creator')
}

/**
 * Панель владельца. Блогер формально тоже «модератор» — право хранится в той же
 * таблице, — но кроме Студии ему там делать нечего: панель из одной сводки кланов
 * только путала бы, зачем она ему.
 */
export function canUseOwnerPanel(me: ServiceIdentity): boolean {
  if (me.role === 'none') return false
  if (me.role === 'owner') return true
  return me.permissions.some(p => p !== 'Creator')
}
