import { defineConfig, devices } from '@playwright/test';

// The end-to-end tests run in a phone-sized browser: the product is mobile first (spec §4).
// Without E2E_BASE_URL, they run on the built static site (`yarn preview`): only the @smoke tests, whose snapshot is
// simulated. With E2E_BASE_URL, they run on a sandbox (deploy/sandbox): the @stack test drives the whole flow, the @offline
// test checks the public page with the back end stopped.
// ADRIAMO_BASE: the site built under a path (spec §10.5), served by `yarn preview` under that path; the tests navigate relatively.
const sandbox = process.env['E2E_BASE_URL'] || undefined;
const configuredBase = process.env['ADRIAMO_BASE'] ?? '/';
const base = configuredBase.endsWith('/') ? configuredBase : `${configuredBase}/`;

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env['CI']),
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: sandbox ?? `http://127.0.0.1:4321${base}`,
    trace: 'retain-on-failure',
  },
  // The @stack and @offline tests need a sandbox: without one they are not run.
  ...(sandbox ? {} : { grepInvert: /@stack|@offline/ }),
  projects: [{ name: 'mobile', use: { ...devices['Pixel 7'] } }],
  ...(sandbox
    ? {}
    : {
        webServer: {
          command: 'yarn preview --host 127.0.0.1 --port 4321',
          url: `http://127.0.0.1:4321${base}`,
          reuseExistingServer: !process.env['CI'],
          timeout: 60_000,
        },
      }),
});
