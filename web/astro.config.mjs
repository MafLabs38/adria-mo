// @ts-check
import react from '@astrojs/react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'astro/config';

// The public part is static (spec §10.2): it is served by a CDN and reads only the published snapshot, never the API.
// ADRIAMO_BASE lets the same build live at the root of its own address or under a path of Adria Résidents (spec §10.5).
// PUBLIC_ADRIAMO_DATA_URL (the published snapshot, default `<base>data/`) and PUBLIC_ADRIAMO_API_URL (the administration's
// API, default `/api/v1`) are read at build time.
export default defineConfig({
  output: 'static',
  base: process.env.ADRIAMO_BASE ?? '/',
  trailingSlash: 'ignore',
  integrations: [react()],
  vite: {
    plugins: [tailwindcss()],
    // `yarn dev` only: the administration calls /api/v1 on its own origin, as behind the static server of the sandbox.
    server: {
      proxy: {
        '/api': process.env.ADRIAMO_DEV_API ?? 'http://localhost:5080',
      },
    },
  },
});
