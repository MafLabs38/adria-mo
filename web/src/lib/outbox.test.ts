import { describe, expect, it, vi } from 'vitest';
import { enqueue, flush, inboxSender, serialized, type OutboxItem, type OutboxStore } from './outbox';

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
    () => clock++,
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

  it.each([500, 502, 429])('stops when the back end answers %s, and keeps the photo', async (status) => {
    const store = await filled('a.jpg');
    expect(await flush(store, () => Promise.resolve(answer(status)))).toEqual({
      sent: 0,
      refused: 0,
      stopped: 'unreachable',
    });
    expect(store.items.size).toBe(1);
  });

  it.each([401, 403])('stops when the session is gone (%s): the phone must log in again', async (status) => {
    const store = await filled('a.jpg', 'b.jpg');
    const send = vi.fn().mockResolvedValue(answer(status));

    expect(await flush(store, send)).toEqual({ sent: 0, refused: 0, stopped: 'session' });
    expect(send).toHaveBeenCalledTimes(1);
  });

  it('marks a refused photo with its reason, goes on with the others and never sends it again', async () => {
    const store = await filled('notes.pdf', 'b.jpg', 'big.jpg', 'odd.jpg', 'plain.jpg');
    const answers = [
      answer(400, { errors: { photo: ['NotAnImage'] } }),
      answer(201),
      answer(413),
      answer(400, { errors: {} }),
      new Response('not json', { status: 400 }),
    ];

    const outcome = await flush(store, () => Promise.resolve(answers.shift() ?? answer(500)));

    expect(outcome).toEqual({ sent: 1, refused: 4, stopped: null });
    expect([...store.items.values()].map((i) => [i.name, i.refused])).toEqual([
      ['notes.pdf', 'NotAnImage'],
      ['big.jpg', 'TooLarge'],
      ['odd.jpg', 'NotAnImage'],
      ['plain.jpg', 'NotAnImage'],
    ]);
    const again = vi.fn();
    expect(await flush(store, again)).toEqual({ sent: 0, refused: 0, stopped: null });
    expect(again).not.toHaveBeenCalled();
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
    let running = 0;
    let most = 0;
    const run = vi.fn(async () => {
      running++;
      most = Math.max(most, running);
      await new Promise((resolve) => setTimeout(resolve, 5));
      running--;
      return { sent: 0, refused: 0, stopped: null };
    });
    const flushOnce = serialized(run);

    await Promise.all([flushOnce(), flushOnce(), flushOnce()]);

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
