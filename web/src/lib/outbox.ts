/**
 * The photos that wait in the phone until they can be sent (spec §4.1): shared from the gallery or taken without network, they
 * are kept in the browser and sent to the inbox "À classer" as soon as the back end answers, one at a time.
 */
import { APP_HEADER } from './api';

export interface OutboxItem {
  id: string;
  file: Blob;
  /** The name of the file on the phone, shown while it waits. */
  name: string;
  /** When it was put in the outbox (milliseconds). */
  addedAt: number;
  /** The code of a refusal by the back end (`NotAnImage`, `TooLarge`…): the photo is not sent again. */
  refused?: string;
}

export interface OutboxStore {
  add(item: OutboxItem): Promise<void>;
  all(): Promise<OutboxItem[]>;
  put(item: OutboxItem): Promise<void>;
  remove(id: string): Promise<void>;
}

export interface FlushOutcome {
  sent: number;
  refused: number;
  /** Why the sending stopped before the end: no network or no answer, or no session (the phone must log in again). */
  stopped: 'unreachable' | 'session' | null;
}

/** Sends one photo, and gives the answer of the back end. A network failure throws. */
export type Sender = (item: OutboxItem) => Promise<Response>;

/** Puts files in the outbox, oldest first. */
export async function enqueue(store: OutboxStore, files: readonly File[], now: () => number = Date.now): Promise<void> {
  for (const file of files) {
    await store.add({ id: crypto.randomUUID(), file, name: file.name || 'photo', addedAt: now() });
  }
}

/**
 * Sends the waiting photos, oldest first, and stops at the first that cannot go now: the next try starts from it. A photo the
 * back end already holds counts as sent; one it refuses stays, marked, for its owner to remove.
 */
export async function flush(store: OutboxStore, send: Sender): Promise<FlushOutcome> {
  const outcome: FlushOutcome = { sent: 0, refused: 0, stopped: null };
  const waiting = (await store.all())
    .filter((item) => item.refused === undefined)
    .sort((a, b) => a.addedAt - b.addedAt);
  for (const item of waiting) {
    let response: Response;
    try {
      response = await send(item);
    } catch {
      return { ...outcome, stopped: 'unreachable' };
    }
    if (response.ok || response.status === 409) {
      await store.remove(item.id);
      outcome.sent++;
    } else if (response.status === 400 || response.status === 413) {
      await store.put({ ...item, refused: await refusalOf(response) });
      outcome.refused++;
    } else if (response.status === 401 || response.status === 403) {
      return { ...outcome, stopped: 'session' };
    } else {
      return { ...outcome, stopped: 'unreachable' };
    }
  }
  return outcome;
}

async function refusalOf(response: Response): Promise<string> {
  if (response.status === 413) {
    return 'TooLarge';
  }
  try {
    const body = (await response.json()) as { errors?: Record<string, string[]> };
    return body.errors?.['photo']?.[0] ?? 'NotAnImage';
  } catch {
    return 'NotAnImage';
  }
}

/** The sender of the inbox of the back end at `apiBase`. */
export function inboxSender(apiBase: string, fetcher: typeof fetch = (input, init) => fetch(input, init)): Sender {
  return (item) => {
    const form = new FormData();
    form.append('photo', item.file, item.name);
    return fetcher(`${apiBase}/inbox`, {
      method: 'POST',
      body: form,
      credentials: 'same-origin',
      headers: { [APP_HEADER]: '1', Accept: 'application/json' },
    });
  };
}

/** One flush at a time: a flush asked while another runs starts after it, for what arrived meanwhile. */
export function serialized(run: () => Promise<FlushOutcome>): () => Promise<FlushOutcome> {
  let last: Promise<unknown> = Promise.resolve();
  return () => {
    const next = last.then(run, run);
    last = next.catch(() => undefined);
    return next;
  };
}
