// @ts-check
import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const entry = fileURLToPath(new URL('../src/sw/sw.ts', import.meta.url));
/** Under the administration, so the service worker's scope is the administration and never the public pages. */
const output = 'admin/sw.js';

/** @returns {Promise<string>} The service worker, bundled in one classic script (a service worker loads no module by default). */
async function bundle() {
  const result = await build({
    entryPoints: [entry],
    bundle: true,
    write: false,
    format: 'iife',
    target: 'es2022',
    minify: true,
    legalComments: 'none',
  });
  const [file] = result.outputFiles;
  if (!file) {
    throw new Error('The service worker was not bundled.');
  }
  return file.text;
}

/**
 * Bundles src/sw/sw.ts to admin/sw.js: written in the build, served by `astro dev`.
 * @returns {import('astro').AstroIntegration}
 */
export default function serviceWorker() {
  return {
    name: 'adria-mo-service-worker',
    hooks: {
      'astro:server:setup': ({ server }) => {
        server.middlewares.use((request, response, next) => {
          const path = (request.url ?? '').split('?')[0] ?? '';
          if (!path.endsWith(`/${output}`)) {
            next();
            return;
          }
          bundle().then(
            (code) => {
              response.setHeader('Content-Type', 'text/javascript');
              response.end(code);
            },
            (error) => next(error),
          );
        });
      },
      'astro:build:done': async ({ dir }) => {
        await writeFile(new URL(output, dir), await bundle());
      },
    },
  };
}
