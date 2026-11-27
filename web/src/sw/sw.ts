/**
 * The service worker of the administration (spec §4.1), bundled to `admin/sw.js` (integrations/serviceWorker.mjs): its scope is
 * the administration only. The logic is in lib/serviceWorker.ts and lib/outbox.ts; here, the browser's events.
 */
import { flush, inboxSender, serialized } from '../lib/outbox';
import { openOutbox } from '../lib/outboxStore';
import {
  acceptShare,
  apiPathOf,
  cacheFirst,
  cacheName,
  INBOX_HASH,
  keepOnly,
  networkFirst,
  routeOf,
  sameOriginAssets,
  SYNC_TAG,
} from '../lib/serviceWorker';

// The few types of the service worker's scope this file uses (the "webworker" library of TypeScript conflicts with "dom").
interface WorkerEvent extends Event {
  waitUntil(promise: Promise<unknown>): void;
}
interface WorkerFetchEvent extends WorkerEvent {
  readonly request: Request;
  respondWith(response: Promise<Response>): void;
}
interface WorkerSyncEvent extends WorkerEvent {
  readonly tag: string;
}
interface WorkerMessageEvent extends WorkerEvent {
  readonly data: unknown;
}
interface WorkerScope {
  readonly location: Location;
  readonly registration: { readonly scope: string; readonly sync?: { register(tag: string): Promise<void> } };
  readonly clients: { claim(): Promise<void>; matchAll(): Promise<{ postMessage(message: unknown): void }[]> };
  skipWaiting(): Promise<void>;
  addEventListener(type: 'install' | 'activate', listener: (event: WorkerEvent) => void): void;
  addEventListener(type: 'fetch', listener: (event: WorkerFetchEvent) => void): void;
  addEventListener(type: 'sync', listener: (event: WorkerSyncEvent) => void): void;
  addEventListener(type: 'message', listener: (event: WorkerMessageEvent) => void): void;
}

const worker = globalThis as unknown as WorkerScope;
const scope = new URL(worker.registration.scope);
const apiPath = apiPathOf(worker.location.href);
const fetcher = (request: Request) => fetch(request);
const cache = () => caches.open(cacheName(scope.pathname));
const outbox = () => openOutbox(scope.pathname);

const sendOutbox = serialized(async () => {
  const outcome = await flush(await outbox(), inboxSender(apiPath));
  for (const client of await worker.clients.matchAll()) {
    client.postMessage({ type: 'outbox', outcome });
  }
  return outcome;
});

/** Sends now, and asks the browser to send again when the network comes back if it could not. */
async function sendOrWait(): Promise<void> {
  const outcome = await sendOutbox();
  if (outcome.stopped === 'unreachable') {
    await worker.registration.sync?.register(SYNC_TAG);
  }
}

/**
 * The photos wait in the phone until their owner confirms them on the page: any site can post a form here. Whatever happens,
 * the gallery lands on the inbox, which says when the browser cannot keep photos.
 */
async function receiveShare(request: Request): Promise<Response> {
  try {
    await acceptShare(await request.formData(), await outbox());
  } catch {
    // A malformed form, or a browser that keeps nothing: the page shows it.
  }
  return Response.redirect(new URL(INBOX_HASH, scope).href, 303);
}

worker.addEventListener('install', (event) => event.waitUntil(worker.skipWaiting()));
worker.addEventListener('activate', (event) => event.waitUntil(worker.clients.claim()));

worker.addEventListener('fetch', (event) => {
  const { request } = event;
  switch (routeOf(request.method, new URL(request.url), request.mode, scope, apiPath)) {
    case 'share':
      event.respondWith(receiveShare(request));
      break;
    case 'page':
      event.respondWith(cache().then((kept) => networkFirst(request, kept, scope.href, fetcher)));
      break;
    case 'asset':
      event.respondWith(cache().then((kept) => cacheFirst(request, kept, fetcher)));
      break;
    default:
      break;
  }
});

worker.addEventListener('sync', (event) => {
  if (event.tag === SYNC_TAG) {
    // A failure makes the browser try again later.
    event.waitUntil(
      sendOutbox().then((outcome) => {
        if (outcome.stopped === 'unreachable') {
          throw new Error('The back end cannot be reached yet.');
        }
      }),
    );
  }
});

worker.addEventListener('message', (event) => {
  const message = event.data as { type?: string; urls?: unknown } | null;
  if (message?.type === 'send') {
    event.waitUntil(sendOrWait());
  } else if (message?.type === 'keep' && Array.isArray(message.urls)) {
    const urls = sameOriginAssets(message.urls, scope.origin);
    event.waitUntil(cache().then((kept) => keepOnly(kept, urls, fetcher)));
  }
});
