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
      '/api': 'http://localhost:5180',
    },
  },
})
