import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiTarget = process.env.MD_PIPELINE_API_TARGET ?? 'http://localhost:5100'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3100,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})
