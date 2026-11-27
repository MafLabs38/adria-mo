import { existsSync, readFileSync } from 'node:fs';
import { describe, expect, it, vi } from 'vitest';
import type { OutboxItem, OutboxStore } from './outbox';
import {
  acceptShare,
  apiPathOf,
  cacheFirst,
  cacheName,
  keepOnly,
  MAX_OUTBOX,
  MAX_SHARED,
  networkFirst,
  routeOf,
  sameOriginAssets,
  SHARE_FIELD,
  SHARE_PATH,
} from './serviceWorker';

const scope = new URL('https://adria.example/constats/admin/');
const route = (method: string, path: string, mode = 'cors') =>
  routeOf(method, new URL(path, 'https://adria.example'), mode, scope, '/api/v1');

/**
 * The Cache API of the browser, in memory: matched on the whole address (no `Vary`, no `ignoreSearch`), which is how the
 * service worker uses it; the e2e tests run the real one.
 */
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
  it('receives the photos shared to the address of the manifest, and nothing else posted', () => {
    expect(route('POST', '/constats/admin/partager/')).toBe('share');
    expect(route('GET', '/constats/admin/partager/')).toBe('network');
    expect(route('POST', '/constats/admin/partager')).toBe('network');
    expect(route('PUT', '/constats/admin/partager/')).toBe('network');
    expect(route('DELETE', '/constats/admin/')).toBe('network');
  });

  it('keeps the page of the administration and the scripts named by their content', () => {
    expect(route('GET', '/constats/admin/', 'navigate')).toBe('page');
    expect(route('GET', '/constats/admin/index.html', 'navigate')).toBe('page');
    // Another page under the scope would overwrite the one kept: the administration is one page, with routes after `#`.
    expect(route('GET', '/constats/admin/autre/', 'navigate')).toBe('network');
    expect(cacheName('/constats/admin/')).toBe('adria-mo-admin:/constats/admin/');
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
  function memory(count = 0) {
    const added: OutboxItem[] = Array.from({ length: count }, (_, i) => ({
      id: `old${i}`,
      file: new Blob(),
      name: 'old',
      addedAt: i,
    }));
    const store = {
      add: (item: OutboxItem) => Promise.resolve(void added.push(item)),
      all: () => Promise.resolve([...added]),
    } as OutboxStore;
    return { added, store };
  }
  const form = (count: number) => {
    const data = new FormData();
    for (let i = 0; i < count; i++) {
      data.append(SHARE_FIELD, new File([new Uint8Array([i])], `IMG_${i}.jpg`));
    }
    return data;
  };

  it('keeps the shared photos, to be confirmed, and nothing else', async () => {
    const { added, store } = memory();
    const shared = form(2);
    shared.append(SHARE_FIELD, new File([], 'vide.jpg'));
    shared.append(SHARE_FIELD, 'du texte');
    shared.append('title', 'Partage');

    expect(await acceptShare(shared, store)).toBe(2);
    expect(added.map((i) => [i.name, i.shared])).toEqual([
      ['IMG_0.jpg', true],
      ['IMG_1.jpg', true],
    ]);
  });

  it('takes a limited number of photos from a share and in all', async () => {
    expect(await acceptShare(form(MAX_SHARED + 5), memory().store)).toBe(MAX_SHARED);
    expect(await acceptShare(form(10), memory(MAX_OUTBOX - 3).store)).toBe(3);
    expect(await acceptShare(form(10), memory(MAX_OUTBOX + 1).store)).toBe(0);
  });
});

describe('sameOriginAssets', () => {
  it('keeps the scripts and styles of this site only', () => {
    expect(
      sameOriginAssets(
        [
          'https://adria.example/_astro/AdminApp.js',
          'https://adria.example/api/v1/auth/me',
          'https://adria.example.evil/_astro/x.js',
          'pas une adresse',
          42,
        ],
        'https://adria.example',
      ),
    ).toEqual(['https://adria.example/_astro/AdminApp.js']);
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
    const response = await networkFirst(new Request(page), kept, page, () =>
      Promise.resolve(new Response('error', { status: 500 })),
    );
    expect(response.status).toBe(500);
    expect(kept.entries.size).toBe(0);
  });

  it('serves the page kept when a proxy answers for a stopped server', async () => {
    const kept = cache();
    const stopped = () => Promise.resolve(new Response('bad gateway', { status: 502 }));
    expect((await networkFirst(new Request(page), kept, page, stopped)).status).toBe(502);
    await kept.put(page, new Response('kept'));
    expect(await (await networkFirst(new Request(page), kept, page, stopped)).text()).toBe('kept');
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

    await keepOnly(kept, [script], fetcher);

    expect([...kept.entries.keys()].sort()).toEqual([script, 'https://adria.example/constats/admin/']);
    await keepOnly(kept, [script], fetcher);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});

describe('the manifest', () => {
  const manifest = JSON.parse(
    readFileSync(new URL('../../public/admin/manifest.webmanifest', import.meta.url), 'utf8'),
  ) as {
    start_url: string;
    scope: string;
    share_target: { action: string; method: string; enctype: string; params: { files: { name: string }[] } };
  };

  it('shares photos to the address and the field the service worker receives', () => {
    expect(manifest.share_target.action).toBe(`./${SHARE_PATH}`);
    expect(manifest.share_target.method).toBe('POST');
    expect(manifest.share_target.enctype).toBe('multipart/form-data');
    expect(manifest.share_target.params.files.map((f) => f.name)).toEqual([SHARE_FIELD]);
  });

  it('opens the administration, inside the scope of the service worker, with no address hard-coded', () => {
    // The manifest lives in admin/, with the service worker: the build for the CDN leaves the whole folder out.
    expect(manifest.start_url).toBe('./');
    expect(manifest.scope).toBe('./');
  });

  it('names icons that exist, beside it', () => {
    const icons = (manifest as unknown as { icons: { src: string }[] }).icons;
    expect(icons.length).toBeGreaterThan(0);
    for (const icon of icons) {
      expect(existsSync(new URL(icon.src, new URL('../../public/admin/', import.meta.url)))).toBe(true);
    }
  });
});
