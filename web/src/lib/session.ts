/** How the administration opens (spec §2.1, §4.1): with a pairing code, with the session the browser has, or without network. */
import { ApiError } from './api';
import { isUnreachable } from './outbox';
import { pairingCodeOf } from './pairing';

export type Session<Me> =
  | { kind: 'loading' }
  | { kind: 'anonymous'; pairingError?: ApiError }
  | { kind: 'offline' }
  | { kind: 'error'; error: unknown }
  | { kind: 'in'; me: Me };

interface SessionApi {
  get<T>(path: string): Promise<T>;
  post<T>(path: string, body?: unknown): Promise<T>;
}

interface Address {
  readonly hash: string;
  replace(url: string): void;
}

/** An answer of the back end itself; anything else (no answer, a proxy answering for a stopped back end) means "offline". */
function answered(error: unknown): error is ApiError {
  return error instanceof ApiError && !isUnreachable(error.status);
}

/**
 * The session at the opening: a phone that scanned a pairing code logs in with it (the code leaves the address and the history
 * at once), any other page asks who is logged in. Without an answer, the phone keeps its photos for later.
 */
export async function openSession<Me>(api: SessionApi, address: Address): Promise<Session<Me>> {
  const code = pairingCodeOf(address.hash);
  if (code) {
    address.replace('#/a-classer');
    try {
      return { kind: 'in', me: await api.post<Me>('/auth/pair', { code }) };
    } catch (error) {
      return answered(error) ? { kind: 'anonymous', pairingError: error } : { kind: 'offline' };
    }
  }
  try {
    return { kind: 'in', me: await api.get<Me>('/auth/me') };
  } catch (error) {
    if (!answered(error)) {
      return { kind: 'offline' };
    }
    return error.status === 401 ? { kind: 'anonymous' } : { kind: 'error', error };
  }
}

/** Whether a pairing was refused because the code is no longer valid (rather than for another reason). */
export function isStaleCode(error: ApiError): boolean {
  return error.status === 401;
}
