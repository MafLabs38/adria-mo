import { useEffect } from 'react';
import type { Api } from '../../lib/api';
import OutboxPanel from './OutboxPanel';
import { Panel, secondaryButton } from './ui';
import { useOutbox } from './useOutbox';

/**
 * The administration opened without network (spec §4.1): the photos taken now wait in the phone and leave by themselves when
 * the network comes back; the rest of the administration comes back with it.
 */
export default function OfflinePage({ api, onOnline }: Readonly<{ api: Api; onOnline: () => void }>) {
  const outbox = useOutbox(api.base, () => undefined);
  useEffect(() => {
    globalThis.addEventListener('online', onOnline);
    return () => globalThis.removeEventListener('online', onOnline);
  }, [onOnline]);

  return (
    <div className="space-y-4">
      <Panel title="Pas de réseau">
        <p>
          L'administration reviendra avec le réseau. En attendant, vos photos restent dans le téléphone et partiront
          toutes seules dans « À classer ».
        </p>
        <button type="button" className={secondaryButton} onClick={onOnline}>
          Réessayer
        </button>
      </Panel>
      <OutboxPanel outbox={outbox} />
    </div>
  );
}
