import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    proxy: {
      // One API Service entry point; WMS.API routes to the owning business service.
      '/api': { target: 'http://localhost:5062', changeOrigin: true },
    },
  },
});
