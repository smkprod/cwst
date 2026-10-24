/** Смайлик карты на арене тренажёра: юнит, здание, снаряд заклинания. */
export const EMOJI: Record<string, string> = {
  'Knight': '🤺',
  'Hog Rider': '🐗',
  'Skeletons': '💀',
  'Goblins': '👺',
  'Giant': '🗿',
  'Mega Knight': '🦾',
  'Mini P.E.K.K.A': '🤖',
  'Musketeer': '💂',
  'Balloon': '🎈',
  'Miner': '⛏️',
  'Golem': '🪨',
  'Golemite': '🪨',
  'Cannon': '💣',
  'X-Bow': '🏹',
  'Fireball': '☄️',
  'The Log': '🪵',
  'Goblin Barrel': '🛢️',
}

export const emojiOf = (card: string) => EMOJI[card] ?? '⚔️'
