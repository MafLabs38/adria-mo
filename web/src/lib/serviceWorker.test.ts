import { readFileSync } from 'node:fs';
import { describe, expect, it, vi } from 'vitest';
import type { OutboxItem, OutboxStore } from './outbox';
import {
  acceptShare,
  apiPathOf,
  cacheFirst,
  keepOnly,
  networkFirst,
  routeOf,
  SHARE_FIELD,
  SHARE_PATH,
} from './serviceWorker';

const scope = new URL('https://adria.example/constats/admin/');
const route = (method: string, path: string, mode = 'cors') =>
  routeOf(method, new URL(path, 'https://adria.example'), mode, scope, '/api/v1');

/** The Cache API of the browser, in memory. */
class MemoryCache {
  readonly entries = new Map<string, Response>();
  private static key(request: RequestInfo | URL): string {
    return request instanceof Request ? request.url : new URL(String(request), 'https://adria.example').href;
  }
  match(request: RequestInfo | URL) {
    return Promise.resolve(this.entries.get(MemoryCache.key(request))?.clone());
  }
  put(request: RequestInfo | URL, response: Response) {
    this.entries.set(MemoryCache.key(request), response);
    return Promise.resolve();
  }
  keys() {
    return Promise.resolve([...this.entries.keys()].map((url) => new Request(url)));
  }
  delete(request: RequestInfo | URL) {
    return Promise.resolve(this.entries.delete(MemoryCache.key(request)));
  }
}
const cache = () => new MemoryCache() as MemoryCache & Cache;

describe('routeOf', () => {
  it('receives the photos shared to the address of the manifest', () => {
    expect(route('POST', '/constats/admin/partager/')).toBe('share');
    expect(route('GET', '/constats/admin/partager/')).toBe('network');
  });

  it('keeps the page of the administration and the scripts named by their content', () => {
    expect(route('GET', '/constats/admin/', 'navigate')).toBe('page');
    expect(route('GET', '/constats/_astro/AdminApp.Cx1.js')).toBe('asset');
  });

  it('never keeps the API, another site, a public page or another method', () => {
    expect(route('GET', '/api/v1/inbox')).toBe('network');
    expect(route('GET', '/api/v1')).toBe('network');
    expect(route('POST', '/api/v1/inbox')).toBe('network');
    expect(route('GET', '/constats/', 'navigate')).toBe('network');
    expect(route('GET', '/constats/data/constats.json')).toBe('network');
    expect(routeOf('GET', new URL('https://other.example/_astro/x.js'), 'cors', scope, '/api/v1')).toBe('network');
  });
});

describe('apiPathOf', () => {
  it('reads the path given in the address of the service worker', () => {
    expect(apiPathOf('https://adria.example/admin/sw.js?api=%2Fapi%2Fv1%2F')).toBe('/api/v1');
  });

  it.each(['', '?api=https%3A%2F%2Fevil.example%2Fapi', '?api=%2F%2Fevil.example%2Fapi', '?api=api'])(
    'falls back to /api/v1 for anything but a path of this site (%s)',
    (query) => {
      expect(apiPathOf(`https://adria.example/admin/sw.js${query}`)).toBe('/api/v1');
    },
  );
});

describe('acceptShare', () => {
  it('puts the shared photos in the outbox, and nothing else', async () => {
    const added: OutboxItem[] = [];
    const store = { add: (item: OutboxItem) => Promise.resolve(void added.push(item)) } as OutboxStore;
    const form = new FormData();
    form.append(SHARE_FIELD, new File([new Uint8Array([1])], 'IMG_1.jpg'));
    form.append(SHARE_FIELD, new File([new Uint8Array([2])], 'IMG_2.jpg'));
    form.append(SHARE_FIELD, new File([], 'vide.jpg'));
    form.append(SHARE_FIELD, 'du texte');
    form.append('title', 'Partage');

    expect(await acceptShare(form, store)).toBe(2);
    expect(added.map((i) => i.name)).toEqual(['IMG_1.jpg', 'IMG_2.jpg']);
  });
});

describe('networkFirst', () => {
  const page = 'https://adria.example/constats/admin/';

  it('serves the network and keeps the page for later', async () => {
    const kept = cache();
    const response = await networkFirst(new Request(page), kept, page, () => Promise.resolve(new Response('fresh')));
    expect(await response.text()).toBe('fresh');
    expect(await (await kept.match(page))?.text()).toBe('fresh');
  });

  it('does not keep an error page', async () => {
    const kept = cache();
    await networkFirst(new Request(page), kept, page, () => Promise.resolve(new Response('down', { status: 502 })));
    expect(kept.entries.size).toBe(0);
  });

  it('serves the page kept when there is no network, and fails without one', async () => {
    const kept = cache();
    const offline = () => Promise.reject(new TypeError('Failed to fetch'));
    await expect(networkFirst(new Request(page), kept, page, offline)).rejects.toThrow('Failed to fetch');
    await kept.put(page, new Response('kept'));
    expect(await (await networkFirst(new Request(`${page}#/a-classer`), kept, page, offline)).text()).toBe('kept');
  });
});

describe('cacheFirst and keepOnly', () => {
  const script = 'https://adria.example/_astro/AdminApp.new.js';

  it('fetches a script once, then serves it from the cache', async () => {
    const kept = cache();
    const fetcher = vi.fn(() => Promise.resolve(new Response('code')));
    await cacheFirst(new Request(script), kept, fetcher);
    expect(await (await cacheFirst(new Request(script), kept, fetcher)).text()).toBe('code');
    expect(fetcher).toHaveBeenCalledTimes(1);
    await cacheFirst(new Request(`${script}?missing`), kept, () => Promise.resolve(new Response('', { status: 404 })));
    expect(kept.entries.size).toBe(1);
  });

  it('keeps the scripts of the page and forgets those of earlier versions', async () => {
    const kept = cache();
    await kept.put('https://adria.example/_astro/AdminApp.old.js', new Response('old'));
    await kept.put('https://adria.example/constats/admin/', new Response('page'));
    const fetcher = vi.fn(() => Promise.resolve(new Response('new')));

    await keepOnly(kept, [script, 'https://adria.example/api/v1/auth/me'], fetcher);

    expect([...kept.entries.keys()].sort()).toEqual([script, 'https://adria.example/constats/admin/']);
    await keepOnly(kept, [script], fetcher);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});

describe('the manifest', () => {
  const manifest = JSON.parse(readFileSync(new URL('../../public/manifest.webmanifest', import.meta.url), 'utf8')) as {
    start_url: string;
    scope: string;
    icons: { sizes: string; purpose: string }[];
    share_target: { action: string; method: string; enctype: string; params: { files: { name: string }[] } };
  };

  it('shares photos to the address and the field the service worker receives', () => {
    expect(manifest.share_target.action).toBe(`./admin/${SHARE_PATH}`);
    expect(manifest.share_target.method).toBe('POST');
    expect(manifest.share_target.enctype).toBe('multipart/form-data');
    expect(manifest.share_target.params.files.map((f) => f.name)).toEqual([SHARE_FIELD]);
  });

  it('opens the administration, inside the scope of the service worker, with no address hard-coded', () => {
    expect(manifest.start_url).toBe('./admin/');
    expect(manifest.scope).toBe('./admin/');
    expect(manifest.icons.map((i) => `${i.sizes} ${i.purpose}`)).toEqual([
      '192x192 any',
      '512x512 any',
      '512x512 maskable',
    ]);
  });
});
