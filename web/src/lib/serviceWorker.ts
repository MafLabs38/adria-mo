/**
 * What the service worker of the administration does (src/sw/sw.ts wires it to the browser's events): receive the photos shared
 * from the gallery, keep the administration's page and scripts so it opens without network, and nothing else. The API is never
 * cached, and the public pages are outside its scope.
 */
import { apiUrl } from './api';
import { enqueue, type OutboxStore } from './outbox';

/** The address the gallery posts shared photos to, under the scope (the `share_target` of the manifest). */
export const SHARE_PATH = 'partager/';

/** The field of the shared files (the `share_target` of the manifest). */
export const SHARE_FIELD = 'photos';

/** Where a share leads: the inbox of the administration. */
export const INBOX_HASH = '#/a-classer';

/** The tag of the background sync that sends the outbox when the network comes back. */
export const SYNC_TAG = 'outbox';

/** The cache of the administration's page and scripts. */
export const CACHE_NAME = 'adria-mo-admin';

export type Route = 'share' | 'page' | 'asset' | 'network';

/** How the service worker answers a request. */
export function routeOf(method: string, url: URL, mode: string, scope: URL, apiPath: string): Route {
  if (url.origin !== scope.origin) {
    return 'network';
  }
  if (method === 'POST' && url.pathname === `${scope.pathname}${SHARE_PATH}`) {
    return 'share';
  }
  if (method !== 'GET' || url.pathname === apiPath || url.pathname.startsWith(`${apiPath}/`)) {
    return 'network';
  }
  if (mode === 'navigate' && url.pathname.startsWith(scope.pathname)) {
    return 'page';
  }
  return url.pathname.includes('/_astro/') ? 'asset' : 'network';
}

/** The API's path, given to the service worker in the query of its address; only a path of this site. */
export function apiPathOf(scriptUrl: string): string {
  const configured = new URL(scriptUrl).searchParams.get('api') ?? '';
  return (configured.startsWith('/') && !configured.startsWith('//') && apiUrl(configured)) || '/api/v1';
}

/** Puts the photos of a share in the outbox; the other fields and the empty files are ignored. */
export async function acceptShare(form: FormData, store: OutboxStore): Promise<number> {
  const files = form.getAll(SHARE_FIELD).filter((value): value is File => value instanceof File && value.size > 0);
  await enqueue(store, files);
  return files.length;
}

type Fetcher = (request: Request) => Promise<Response>;

/** The page from the network, kept for the next time without network; without network, the page kept. */
export async function networkFirst(
  request: Request,
  cache: Cache,
  pageUrl: string,
  fetcher: Fetcher,
): Promise<Response> {
  try {
    const response = await fetcher(request);
    if (response.ok) {
      await cache.put(pageUrl, response.clone());
    }
    return response;
  } catch (error) {
    const kept = await cache.match(pageUrl);
    if (kept) {
      return kept;
    }
    throw error;
  }
}

/** A script or a style named by its content: kept once fetched, it never changes. */
export async function cacheFirst(request: Request, cache: Cache, fetcher: Fetcher): Promise<Response> {
  const kept = await cache.match(request);
  if (kept) {
    return kept;
  }
  const response = await fetcher(request);
  if (response.ok) {
    await cache.put(request, response.clone());
  }
  return response;
}

/** Keeps the scripts and styles the page uses now, and forgets those of earlier versions. */
export async function keepOnly(cache: Cache, urls: readonly string[], fetcher: Fetcher): Promise<void> {
  const wanted = new Set(urls.filter((url) => url.includes('/_astro/')));
  for (const request of await cache.keys()) {
    if (request.url.includes('/_astro/') && !wanted.has(request.url)) {
      await cache.delete(request);
    }
  }
  for (const url of wanted) {
    if (!(await cache.match(url))) {
      await cacheFirst(new Request(url), cache, fetcher);
    }
  }
}
