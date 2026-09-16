import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // The SPA talks to /api on its own origin in production, so the dev server proxies
    // instead of using a different base URL. That keeps the refresh cookie first-party.
    proxy: {
      '/api': {
        target: process.env.VITE_API_TARGET ?? 'http://localhost:5080',
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        // The player pulls in the markdown renderer and the admin panel is rarely visited;
        // splitting them keeps the landing bundle small (Lighthouse target in T-05).
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
        },
      },
    },
  },
});
