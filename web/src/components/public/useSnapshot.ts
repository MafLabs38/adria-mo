import { useEffect, useState } from 'react';
import { parseSnapshot, type Snapshot } from '../../lib/snapshot';

export type SnapshotState =
  { kind: 'loading' } | { kind: 'none' } | { kind: 'error' } | { kind: 'ready'; snapshot: Snapshot };

/** Reads the published snapshot (never the API): a 404 means nothing was published yet. */
export function useSnapshot(data: string): SnapshotState {
  const [state, setState] = useState<SnapshotState>({ kind: 'loading' });
  useEffect(() => {
    let active = true;
    const load = async (): Promise<SnapshotState> => {
      try {
        const response = await fetch(`${data}constats.json`, { cache: 'no-cache' });
        if (response.status === 404) {
          return { kind: 'none' };
        }
        if (!response.ok) {
          return { kind: 'error' };
        }
        return { kind: 'ready', snapshot: parseSnapshot(await response.json()) };
      } catch {
        return { kind: 'error' };
      }
    };
    void load().then((next) => {
      if (active) {
        setState(next);
      }
    });
    return () => {
      active = false;
    };
  }, [data]);
  return state;
}
