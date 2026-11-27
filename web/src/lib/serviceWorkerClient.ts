/** The page's side of the service worker (serviceWorker.ts): its registration, and the scripts it keeps for offline use. */

/** The address of the service worker: under the administration, with the API's path, which it cannot read elsewhere. */
export function serviceWorkerUrl(base: string, apiBase: string): string {
  return `${adminScope(base)}sw.js?api=${encodeURIComponent(apiBase)}`;
}

/** The scope of the service worker: the administration only. */
export function adminScope(base: string): string {
  const root = base.endsWith('/') ? base : base + '/';
  return `${root}admin/`;
}

/** The scripts and styles of the page, which the service worker keeps so the page opens without network. */
export function loadedAssets(entries: readonly { name: string }[], origin: string): string[] {
  return entries.map((entry) => entry.name).filter((url) => url.startsWith(`${origin}/`) && url.includes('/_astro/'));
}

/** Registers the service worker, then gives it the scripts to keep. Without service workers (an old browser), nothing. */
export async function registerServiceWorker(base: string, apiBase: string): Promise<void> {
  const container = globalThis.navigator.serviceWorker as ServiceWorkerContainer | undefined;
  if (!container) {
    return;
  }
  await container.register(serviceWorkerUrl(base, apiBase), { scope: adminScope(base) });
  const registration = await container.ready;
  registration.active?.postMessage({
    type: 'keep',
    urls: loadedAssets(performance.getEntriesByType('resource'), globalThis.location.origin),
  });
}
