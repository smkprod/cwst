import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
  build: {
    rollupOptions: {
      // Две страницы: мини-апп и виджеты для OBS. Виджетам нужен свой вход — без
      // Telegram SDK и стилей приложения; сервер раздаёт dist как есть, так что
      // /overlay.html в проде работает без отдельной настройки.
      input: {
        main: fileURLToPath(new URL('./index.html', import.meta.url)),
        overlay: fileURLToPath(new URL('./overlay.html', import.meta.url)),
      },
    },
  },
})
