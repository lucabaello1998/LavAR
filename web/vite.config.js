import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// En desarrollo el panel corre en 5173 y la API en 8123: el proxy evita CORS y
// hace que las rutas relativas (/api/...) funcionen igual que en producción,
// donde los dos salen del mismo contenedor.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://127.0.0.1:8123',
    },
  },
  build: {
    // El build se copia a src/Lavar.Api/wwwroot para servir todo junto.
    outDir: 'dist',
    emptyOutDir: true,
  },
});
