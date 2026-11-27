import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  confirmShared,
  enqueue,
  flush,
  forget,
  inboxSender,
  retry as retryItem,
  serialized,
  type FlushOutcome,
  type OutboxItem,
  type OutboxStore,
} from '../../lib/outbox';
import { openOutbox } from '../../lib/outboxStore';
import { adminScope } from '../../lib/serviceWorkerClient';

export interface Outbox {
  /** The photos waiting in the phone, or `null` while they are read. */
  items: OutboxItem[] | null;
  /** Why the last sending stopped, if it did. */
  stopped: FlushOutcome['stopped'];
  /** The browser cannot keep photos (a private window without storage). */
  unavailable: boolean;
  add: (files: readonly File[]) => Promise<void>;
  remove: (id: string) => Promise<void>;
  /** Sends the photos shared from the gallery, once their owner has seen them. */
  confirm: () => Promise<void>;
  /** Forgets the photos shared from the gallery. */
  dismiss: () => Promise<void>;
  /** Sends again a photo set aside after errors of the back end. */
  retry: (id: string) => Promise<void>;
  send: () => void;
}

/**
 * The outbox seen from the page: what waits, and the sending. The service worker sends when it controls the page (it can go on
 * after the page is closed, and when the network comes back); otherwise the page sends itself.
 */
export function useOutbox(apiBase: string, onSent: () => void): Outbox {
  const [store, setStore] = useState<OutboxStore | null>(null);
  const [items, setItems] = useState<OutboxItem[] | null>(null);
  const [stopped, setStopped] = useState<FlushOutcome['stopped']>(null);
  const [unavailable, setUnavailable] = useState(false);
  // The latest callback, without making the sending depend on it: a new function at each render must not send again.
  const onSentRef = useRef(onSent);
  useEffect(() => {
    onSentRef.current = onSent;
  }, [onSent]);

  useEffect(() => {
    openOutbox(adminScope(import.meta.env.BASE_URL)).then(
      async (opened) => {
        setStore(opened);
        setItems(await opened.all());
      },
      () => setUnavailable(true),
    );
  }, []);

  const refresh = useCallback(async () => {
    if (store) {
      setItems(await store.all());
    }
  }, [store]);

  const done = useCallback(
    (outcome: FlushOutcome) => {
      setStopped(outcome.stopped);
      void refresh();
      if (outcome.sent > 0) {
        onSentRef.current();
      }
    },
    [refresh],
  );

  const sendHere = useMemo(
    () => (store ? serialized(() => flush(store, inboxSender(apiBase))) : null),
    [store, apiBase],
  );

  const send = useCallback(() => {
    const controller = globalThis.navigator.serviceWorker?.controller;
    if (controller) {
      controller.postMessage({ type: 'send' });
    } else if (sendHere) {
      sendHere().then(done, () => setStopped('unreachable'));
    }
  }, [sendHere, done]);

  useEffect(() => {
    send();
    const fromWorker = (event: MessageEvent) => {
      const message = event.data as { type?: string; outcome?: FlushOutcome } | null;
      if (message?.type === 'outbox' && message.outcome) {
        done(message.outcome);
      }
    };
    globalThis.navigator.serviceWorker?.addEventListener('message', fromWorker);
    globalThis.addEventListener('online', send);
    return () => {
      globalThis.navigator.serviceWorker?.removeEventListener('message', fromWorker);
      globalThis.removeEventListener('online', send);
    };
  }, [refresh, send, done]);

  const add = useCallback(
    async (files: readonly File[]) => {
      if (store) {
        await enqueue(store, files);
        await refresh();
        send();
      }
    },
    [store, refresh, send],
  );

  const remove = useCallback(
    async (id: string) => {
      if (store) {
        await store.remove(id);
        await refresh();
      }
    },
    [store, refresh],
  );

  const confirm = useCallback(async () => {
    if (store) {
      await confirmShared(store);
      await refresh();
      send();
    }
  }, [store, refresh, send]);

  const dismiss = useCallback(async () => {
    if (store) {
      await forget(store, 'shared');
      await refresh();
    }
  }, [store, refresh]);

  const retry = useCallback(
    async (id: string) => {
      if (store) {
        await retryItem(store, id);
        await refresh();
        send();
      }
    },
    [store, refresh, send],
  );

  return { items, stopped, unavailable, add, remove, confirm, dismiss, retry, send };
}
