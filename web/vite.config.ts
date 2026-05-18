import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/hubs": {
        target: "http://localhost:5096",
        ws: true,
      },
      "/v1": {
        target: "http://localhost:5096",
      },
    },
  },
})
