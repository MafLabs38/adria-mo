import { IDBFactory } from 'fake-indexeddb';
import { describe, expect, it } from 'vitest';
import { openOutbox } from './outboxStore';

const item = (id: string, addedAt: number) => ({ id, file: new Blob(['x']), name: `${id}.jpg`, addedAt });

describe('openOutbox', () => {
  it('keeps, changes and forgets photos across openings', async () => {
    const factory = new IDBFactory();
    const store = await openOutbox(factory);
    await store.add(item('a', 1));
    await store.add(item('b', 2));
    await store.put({ ...item('b', 2), refused: 'NotAnImage' });
    await store.remove('a');

    const reopened = await openOutbox(factory);

    expect((await reopened.all()).map((i) => [i.id, i.refused])).toEqual([['b', 'NotAnImage']]);
  });

  it('refuses the same photo twice', async () => {
    const store = await openOutbox(new IDBFactory());
    await store.add(item('a', 1));
    await expect(store.add(item('a', 1))).rejects.toBeDefined();
    expect(await store.all()).toHaveLength(1);
  });

  it('fails when the browser keeps nothing (a private window)', async () => {
    const refusing = {
      open: () => {
        const request = {
          error: new DOMException('refused'),
          onerror: null as (() => void) | null,
        } as unknown as IDBOpenDBRequest;
        setTimeout(() => request.onerror?.(new Event('error')));
        return request;
      },
    } as unknown as globalThis.IDBFactory;

    await expect(openOutbox(refusing)).rejects.toThrow('refused');
  });
});
