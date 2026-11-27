import { describe, expect, it, vi } from 'vitest';
import {
  confirmShared,
  enqueue,
  FAILURE_SPACING_MS,
  flush,
  forget,
  forgetBeforeLogout,
  inboxSender,
  isUnreachable,
  MAX_FAILURES,
  retry,
  serialized,
  type OutboxItem,
  type OutboxStore,
} from './outbox';

function memoryStore(): OutboxStore & { items: Map<string, OutboxItem> } {
  const items = new Map<string, OutboxItem>();
  return {
    items,
    add: (item) => {
      items.set(item.id, item);
      return Promise.resolve();
    },
    put: (item) => {
      items.set(item.id, item);
      return Promise.resolve();
    },
    remove: (id) => {
      items.delete(id);
      return Promise.resolve();
    },
    all: () => Promise.resolve([...items.values()]),
  };
}

const photo = (name: string) => new File([new Uint8Array([1, 2, 3])], name, { type: 'image/jpeg' });
const answer = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

async function filled(...names: string[]) {
  const store = memoryStore();
  let clock = 1000;
  await enqueue(
    store,
    names.map((n) => photo(n)),
    { now: () => clock++ },
  );
  return store;
}

describe('enqueue', () => {
  it('keeps each file with its name and the time it was added', async () => {
    const store = await filled('a.jpg', 'b.jpg');

    const items = await store.all();
    expect(items.map((i) => [i.name, i.addedAt])).toEqual([
      ['a.jpg', 1000],
      ['b.jpg', 1001],
    ]);
    expect(new Set(items.map((i) => i.id)).size).toBe(2);
  });

  it('names a file without a name', async () => {
    const store = memoryStore();
    await enqueue(store, [photo('')]);
    expect((await store.all())[0]?.name).toBe('photo');
  });
});

describe('flush', () => {
  it('sends the oldest first and forgets what the back end has, already had or kept', async () => {
    const store = await filled('a.jpg', 'b.jpg', 'c.jpg');
    const sent: string[] = [];
    const statuses = [201, 200, 409];

    const outcome = await flush(store, (item) => {
      sent.push(item.name);
      return Promise.resolve(answer(statuses.shift() ?? 500));
    });

    expect(sent).toEqual(['a.jpg', 'b.jpg', 'c.jpg']);
    expect(outcome).toEqual({ sent: 3, refused: 0, stopped: null });
    expect(store.items.size).toBe(0);
  });

  it('stops without network and starts again from the photo that did not go', async () => {
    const store = await filled('a.jpg', 'b.jpg');
    const send = vi.fn().mockResolvedValueOnce(answer(201)).mockRejectedValueOnce(new TypeError('Failed to fetch'));

    expect(await flush(store, send)).toEqual({ sent: 1, refused: 0, stopped: 'unreachable' });
    expect([...store.items.values()].map((i) => i.name)).toEqual(['b.jpg']);
  });

  it.each([502, 503, 504, 530, 429])(
    'stops when a proxy answers %s for a stopped back end, and keeps the photo',
    async (status) => {
      const store = await filled('a.jpg');
      expect(await flush(store, () => Promise.resolve(answer(status)))).toEqual({
        sent: 0,
        refused: 0,
        stopped: 'unreachable',
      });
      expect(store.items.size).toBe(1);
    },
  );

  it('tries again a photo the back end failed on, sets it aside after a few failures, and goes on with the others', async () => {
    const store = await filled('broken.jpg', 'b.jpg', 'c.jpg');
    const send = (item: OutboxItem) => Promise.resolve(answer(item.name === 'broken.jpg' ? 500 : 201));
    let clock = 0;
    const later = () => (clock += FAILURE_SPACING_MS);

    expect(await flush(store, send, later)).toEqual({ sent: 2, refused: 0, stopped: 'server' });
    for (let i = 1; i < MAX_FAILURES; i++) {
      await flush(store, send, later);
    }

    expect([...store.items.values()].map((i) => [i.name, i.failures, i.refused])).toEqual([
      ['broken.jpg', MAX_FAILURES, 'ServerError'],
    ]);
    expect(await flush(store, send, later)).toEqual({ sent: 0, refused: 0, stopped: null });
  });

  it('counts an error once in a while: a short outage tried often sets nothing aside', async () => {
    const store = await filled('a.jpg');
    const failing = () => Promise.resolve(answer(500));

    for (let i = 0; i < 10; i++) {
      await flush(store, failing, () => 1000 + i);
    }

    expect([...store.items.values()].map((i) => [i.failures, i.refused])).toEqual([[1, undefined]]);
  });

  it('stops after two errors of the back end in a row: it is failing, not one photo', async () => {
    const store = await filled('a.jpg', 'b.jpg', 'c.jpg');
    const send = vi.fn().mockResolvedValue(answer(500));

    expect(await flush(store, send)).toEqual({ sent: 0, refused: 0, stopped: 'server' });
    expect(send).toHaveBeenCalledTimes(2);
  });

  it('sends again a photo set aside, from scratch', async () => {
    const store = await filled('a.jpg');
    const [item] = await store.all();
    await store.put({ ...(item as OutboxItem), failures: MAX_FAILURES, failedAt: 5, refused: 'ServerError' });

    await retry(store, (item as OutboxItem).id);
    await retry(store, 'unknown');

    expect(await flush(store, () => Promise.resolve(answer(201)))).toEqual({ sent: 1, refused: 0, stopped: null });
  });

  it.each([401, 403])('stops when the session is gone (%s): the phone must log in again', async (status) => {
    const store = await filled('a.jpg', 'b.jpg');
    const send = vi.fn().mockResolvedValue(answer(status));

    expect(await flush(store, send)).toEqual({ sent: 0, refused: 0, stopped: 'session' });
    expect(send).toHaveBeenCalledTimes(1);
  });

  it('marks a refused photo with its reason, goes on with the others and never sends it again', async () => {
    const store = await filled('notes.pdf', 'b.jpg', 'big.jpg', 'odd.jpg', 'plain.jpg', 'empty.jpg');
    const answers = [
      answer(400, { errors: { photo: ['NotAnImage'] } }),
      answer(201),
      answer(413),
      answer(400, { errors: {} }),
      new Response('not json', { status: 400 }),
      answer(400, { errors: { photo: [] } }),
    ];

    const outcome = await flush(store, () => Promise.resolve(answers.shift() ?? answer(500)));

    expect(outcome).toEqual({ sent: 1, refused: 5, stopped: null });
    expect([...store.items.values()].map((i) => [i.name, i.refused])).toEqual([
      ['notes.pdf', 'NotAnImage'],
      ['big.jpg', 'TooLarge'],
      ['odd.jpg', 'NotAnImage'],
      ['plain.jpg', 'NotAnImage'],
      ['empty.jpg', 'NotAnImage'],
    ]);
    const again = vi.fn();
    expect(await flush(store, again)).toEqual({ sent: 0, refused: 0, stopped: null });
    expect(again).not.toHaveBeenCalled();
  });
});

describe('photos shared from the gallery', () => {
  async function withShared() {
    const store = await filled('mine.jpg');
    await enqueue(store, [photo('shared.jpg')], { shared: true, now: () => 2000 });
    return store;
  }

  it('are not sent before their owner confirms them', async () => {
    const store = await withShared();
    const sent: string[] = [];

    await flush(store, (item) => {
      sent.push(item.name);
      return Promise.resolve(answer(201));
    });

    expect(sent).toEqual(['mine.jpg']);
    expect([...store.items.values()].map((i) => [i.name, i.shared])).toEqual([['shared.jpg', true]]);
  });

  it('leave once confirmed', async () => {
    const store = await withShared();
    await confirmShared(store);

    const item = [...store.items.values()].find((i) => i.name === 'shared.jpg');
    expect(item).toEqual({ id: item?.id, file: item?.file, name: 'shared.jpg', addedAt: 2000 });
  });

  it("are all forgotten at logout, with their owner's agreement when some wait", async () => {
    const store = await withShared();
    const refuse = vi.fn(() => false);

    expect(await forgetBeforeLogout(store, refuse)).toBe(false);
    expect(refuse).toHaveBeenCalledWith(2);
    expect(store.items.size).toBe(2);
    expect(await forgetBeforeLogout(store, () => true)).toBe(true);
    expect(store.items.size).toBe(0);
    const never = vi.fn(() => false);
    expect(await forgetBeforeLogout(store, never)).toBe(true);
    expect(never).not.toHaveBeenCalled();
  });

  it('can be forgotten without the others, and all photos can be forgotten', async () => {
    const store = await withShared();
    await forget(store, 'shared');
    expect([...store.items.values()].map((i) => i.name)).toEqual(['mine.jpg']);
    await forget(store, 'all');
    expect(store.items.size).toBe(0);
  });
});

describe('isUnreachable', () => {
  it('tells a proxy answering for a stopped back end from an answer of the back end', () => {
    expect([502, 503, 504, 520, 530].every(isUnreachable)).toBe(true);
    expect([500, 507, 404, 429, 531, 519].some(isUnreachable)).toBe(false);
  });
});

describe('inboxSender', () => {
  it('posts the photo to the inbox with the header of the administration and the session cookie', async () => {
    const fetcher = vi.fn().mockResolvedValue(answer(201));
    const items = await (await filled('porte.jpg')).all();

    await inboxSender('/api/v1', fetcher)(items[0] as OutboxItem);

    const [url, init] = fetcher.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/v1/inbox');
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('same-origin');
    expect(init.headers).toEqual({ 'X-Adria-Mo': '1', Accept: 'application/json' });
    expect(((init.body as FormData).get('photo') as File).name).toBe('porte.jpg');
  });
});

describe('serialized', () => {
  it('runs one flush at a time, and once more for what was asked meanwhile', async () => {
    const releases: (() => void)[] = [];
    let running = 0;
    let most = 0;
    const run = vi.fn(async () => {
      running++;
      most = Math.max(most, running);
      await new Promise<void>((resolve) => releases.push(resolve));
      running--;
      return { sent: 0, refused: 0, stopped: null };
    });
    const flushOnce = serialized(run);

    const all = Promise.all([flushOnce(), flushOnce(), flushOnce()]);
    for (let i = 0; i < 3; i++) {
      await vi.waitFor(() => expect(releases).toHaveLength(i + 1));
      releases[i]?.();
    }
    await all;

    expect(most).toBe(1);
    expect(run).toHaveBeenCalledTimes(3);
  });

  it('goes on after a flush that failed', async () => {
    const run = vi
      .fn()
      .mockRejectedValueOnce(new Error('broken'))
      .mockResolvedValueOnce({ sent: 1, refused: 0, stopped: null });
    const flushOnce = serialized(run);

    await expect(flushOnce()).rejects.toThrow('broken');
    await expect(flushOnce()).resolves.toEqual({ sent: 1, refused: 0, stopped: null });
  });
});
