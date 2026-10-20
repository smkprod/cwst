/** Окно опроса «что добавить» живёт рядом с App и открывается событием - из карточки и по ссылке бота. */
export const OPEN_FEEDBACK = 'cwst:open-feedback'

export function openFeedback() {
  window.dispatchEvent(new Event(OPEN_FEEDBACK))
}

/** Карточку закрыли крестиком - больше не показываем (окно по ссылке бота всё равно откроется). */
export const FEEDBACK_DISMISSED = 'cwst_fb_card_off'
