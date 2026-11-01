import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    proxy: {
      '/api/inbounds': { target: 'http://localhost:5159', changeOrigin: true },
      '/api/inventory': { target: 'http://localhost:5159', changeOrigin: true },
      '/api/receiving-orders': { target: 'http://localhost:5136', changeOrigin: true },
      '/api/products': { target: 'http://localhost:5254', changeOrigin: true },
      '/api/admin': { target: 'http://localhost:5062', changeOrigin: true },
      '/api/purchase-orders': { target: 'http://localhost:5136', changeOrigin: true },
      '/api/catalog': { target: 'http://localhost:5254', changeOrigin: true },
      '/api/warehouses': { target: 'http://localhost:5104', changeOrigin: true },
      '/api': { target: 'http://localhost:5140', changeOrigin: true },
    },
  },
});
