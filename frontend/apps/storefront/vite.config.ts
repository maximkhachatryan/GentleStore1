import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@gentlestore/shared': path.resolve(import.meta.dirname, '../../packages/shared/src'),
    },
  },
  server: {
    port: 5174,
  },
})
