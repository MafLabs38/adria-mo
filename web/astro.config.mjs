// @ts-check
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'astro/config';

// The public part is static (spec §10.2): it is served by a CDN and reads only the published snapshot, never the API.
// ADRIAMO_BASE lets the same build live at the root of its own address or under a path of Adria Résidents (spec §10.5).
export default defineConfig({
  output: 'static',
  base: process.env.ADRIAMO_BASE ?? '/',
  trailingSlash: 'ignore',
  vite: {
    plugins: [tailwindcss()],
  },
});
