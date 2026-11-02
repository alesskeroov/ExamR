import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5190,
    strictPort: true,
    // Same-origin in dev: /api is forwarded to ASP.NET Core, so no CORS and cookies just work.
    proxy: {
      // Keep the browser's Host (localhost:5190) so OAuth redirect URIs and cookies match the UI origin.
      '/api': { target: 'http://localhost:5180', changeOrigin: false },
      // SignalR (battle mode): WebSocket upgrade must be proxied too.
      '/hubs': { target: 'http://localhost:5180', ws: true },
    },
  },
})
