/** The outbox (outbox.ts) in IndexedDB: it survives the page, the service worker and the phone's restart. */
import type { OutboxItem, OutboxStore } from './outbox';

const STORE = 'outbox';

/**
 * The database of the outbox of one administration: named after its scope, so two builds under two base paths of one site
 * (spec §10.5) never send each other's photos.
 */
export function outboxDatabase(scope: string): string {
  return `adria-mo:${scope}`;
}

function request<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB request failed'));
  });
}

export async function openOutbox(scope: string, factory: IDBFactory = indexedDB): Promise<OutboxStore> {
  const open = factory.open(outboxDatabase(scope), 1);
  open.onupgradeneeded = () => open.result.createObjectStore(STORE, { keyPath: 'id' });
  const db = await request(open);

  const run = async <T>(mode: IDBTransactionMode, act: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> => {
    const transaction = db.transaction(STORE, mode);
    const done = new Promise<void>((resolve, reject) => {
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error ?? new Error('IndexedDB transaction failed'));
      transaction.onabort = () => reject(transaction.error ?? new Error('IndexedDB transaction aborted'));
    });
    const [result] = await Promise.all([request(act(transaction.objectStore(STORE))), done]);
    return result;
  };

  return {
    add: async (item) => {
      await run('readwrite', (store) => store.add(item));
    },
    put: async (item) => {
      await run('readwrite', (store) => store.put(item));
    },
    remove: async (id) => {
      await run('readwrite', (store) => store.delete(id));
    },
    all: () => run('readonly', (store) => store.getAll() as IDBRequest<OutboxItem[]>),
  };
}
