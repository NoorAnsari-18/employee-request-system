import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Build straight into the API's wwwroot so one ASP.NET Core service serves both.
// In dev, /api and /health are proxied to the API (dotnet run → http://localhost:5293).
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/EmployeeRequests.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5293',
      '/health': 'http://localhost:5293',
    },
  },
})
