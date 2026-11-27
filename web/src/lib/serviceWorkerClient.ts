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

/** The API must be a path of this site: the service worker sends the photos there with the session cookie. */
export function isSameOriginPath(apiBase: string): boolean {
  return apiBase.startsWith('/') && !apiBase.startsWith('//');
}

/** Registers the service worker, then gives it the scripts to keep. Without service workers (an old browser), nothing. */
export async function registerServiceWorker(base: string, apiBase: string): Promise<void> {
  const container = globalThis.navigator.serviceWorker as ServiceWorkerContainer | undefined;
  if (!container) {
    return;
  }
  if (!isSameOriginPath(apiBase)) {
    // Spec §10.3: the administration and its API share one address. Elsewhere, the page sends the photos itself.
    console.warn('Adria MO: the API is not on this site, the service worker is not registered.');
    return;
  }
  await container.register(serviceWorkerUrl(base, apiBase), { scope: adminScope(base) });
  const registration = await container.ready;
  registration.active?.postMessage({
    type: 'keep',
    urls: performance.getEntriesByType('resource').map((entry) => entry.name),
  });
}
