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
  /**
   * Shared from the gallery and not confirmed yet: it is not sent before its owner says so on the page, because any site can
   * post a form to the share target (spec §4.1).
   */
  shared?: true;
  /** How many times the back end answered with an error of its own (a full disk…), counted at most once in a while. */
  failures?: number;
  /** When the last of those errors was counted (milliseconds). */
  failedAt?: number;
  /** The code of a refusal by the back end (`NotAnImage`, `TooLarge`, `ServerError`…): the photo is not sent again. */
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
  /**
   * Why photos are still waiting: no answer from the back end (no network, or the server of the building is stopped), an error
   * of the back end itself (tried again later, a few times), or no session (the phone must log in again).
   */
  stopped: 'unreachable' | 'server' | 'session' | null;
}

/** Sends one photo, and gives the answer of the back end. A network failure throws. */
export type Sender = (item: OutboxItem) => Promise<Response>;

/**
 * The errors of the back end after which a photo is set aside (its owner can send it again): it never blocks the others, and is
 * never lost.
 */
export const MAX_FAILURES = 3;

/** An error counts against a photo at most once in this time: a short outage, tried often, does not set photos aside. */
export const FAILURE_SPACING_MS = 5 * 60 * 1000;

/** Errors of the back end in a row after which a sending stops: the back end is failing, not one photo. */
const SERVER_ERRORS_IN_A_ROW = 2;

/** The answers of a proxy or a tunnel whose back end does not answer: 502, 503, 504, and Cloudflare's 52x and 530. */
export function isUnreachable(status: number): boolean {
  return status === 502 || status === 503 || status === 504 || (status >= 520 && status <= 530);
}

/** Puts files in the outbox, oldest first. */
export async function enqueue(
  store: OutboxStore,
  files: readonly File[],
  options: { shared?: true; now?: () => number } = {},
): Promise<void> {
  const now = options.now ?? Date.now;
  for (const file of files) {
    const item: OutboxItem = { id: crypto.randomUUID(), file, name: file.name || 'photo', addedAt: now() };
    await store.add(options.shared ? { ...item, shared: true } : item);
  }
}

/** Confirms the photos shared from the gallery: they are sent with the others. */
export async function confirmShared(store: OutboxStore): Promise<void> {
  for (const item of await store.all()) {
    if (item.shared) {
      const confirmed: OutboxItem = { id: item.id, file: item.file, name: item.name, addedAt: item.addedAt };
      await store.put(confirmed);
    }
  }
}

/** Sends again a photo set aside after errors of the back end. */
export async function retry(store: OutboxStore, id: string): Promise<void> {
  const item = (await store.all()).find((candidate) => candidate.id === id);
  if (item) {
    await store.put({ id: item.id, file: item.file, name: item.name, addedAt: item.addedAt });
  }
}

/**
 * Before a logout: the photos still waiting are forgotten, so they never leave under the name of the next account on this
 * browser; when there are some, only if their owner agrees.
 */
export async function forgetBeforeLogout(store: OutboxStore, confirm: (count: number) => boolean): Promise<boolean> {
  const waiting = (await store.all()).length;
  if (waiting > 0 && !confirm(waiting)) {
    return false;
  }
  await forget(store, 'all');
  return true;
}

/** Forgets photos of the outbox: those shared and not confirmed, or all of them. */
export async function forget(store: OutboxStore, which: 'shared' | 'all'): Promise<void> {
  for (const item of await store.all()) {
    if (which === 'all' || item.shared) {
      await store.remove(item.id);
    }
  }
}

/**
 * Sends the confirmed photos, oldest first. Without an answer it stops: the next try starts from the same photo. A photo the
 * back end already holds counts as sent; one it refuses stays, marked, for its owner to remove; one it fails on is tried again
 * at the next sending, and set aside after a few failures.
 */
export async function flush(store: OutboxStore, send: Sender, now: () => number = Date.now): Promise<FlushOutcome> {
  const outcome: FlushOutcome = { sent: 0, refused: 0, stopped: null };
  const waiting = (await store.all())
    .filter((item) => item.refused === undefined && !item.shared)
    .sort((a, b) => a.addedAt - b.addedAt);
  let serverErrors = 0;
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
      serverErrors = 0;
    } else if (response.status === 400 || response.status === 413) {
      await store.put({ ...item, refused: await refusalOf(response) });
      outcome.refused++;
    } else if (response.status === 401 || response.status === 403) {
      return { ...outcome, stopped: 'session' };
    } else if (response.status === 429 || isUnreachable(response.status)) {
      return { ...outcome, stopped: 'unreachable' };
    } else {
      await store.put(failed(item, now()));
      outcome.stopped = 'server';
      if (++serverErrors >= SERVER_ERRORS_IN_A_ROW) {
        return outcome;
      }
    }
  }
  return outcome;
}

/** A photo after an error of the back end: counted once in a while, set aside after a few. */
function failed(item: OutboxItem, at: number): OutboxItem {
  if (item.failedAt !== undefined && at - item.failedAt < FAILURE_SPACING_MS) {
    return item;
  }
  const failures = (item.failures ?? 0) + 1;
  return failures >= MAX_FAILURES
    ? { ...item, failures, failedAt: at, refused: 'ServerError' }
    : { ...item, failures, failedAt: at };
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
