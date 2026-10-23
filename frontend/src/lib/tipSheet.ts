/** Окно фишки живёт рядом с App и открывается событием - из профиля, библиотеки, отчёта о бое. */
export const OPEN_TIP = 'cwst:open-tip'

export function openTip(id: string) {
  window.dispatchEvent(new CustomEvent(OPEN_TIP, { detail: id }))
}
