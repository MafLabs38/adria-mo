import { useEffect } from 'react';
import type { Api } from '../../lib/api';
import OutboxPanel from './OutboxPanel';
import { Panel, secondaryButton } from './ui';
import { useOutbox } from './useOutbox';

/**
 * The administration opened without an answer from the back end (spec §4.1): no network, or the server of the building stopped.
 * The photos taken now wait in the phone and leave by themselves; once one leaves, the server answers again and the rest of the
 * administration comes back.
 */
export default function OfflinePage({ api, onOnline }: Readonly<{ api: Api; onOnline: () => void }>) {
  const outbox = useOutbox(api.base, onOnline);
  useEffect(() => {
    globalThis.addEventListener('online', onOnline);
    return () => globalThis.removeEventListener('online', onOnline);
  }, [onOnline]);

  return (
    <div className="space-y-4">
      <Panel title="Administration injoignable">
        <p>
          Pas de réseau, ou le serveur de l'immeuble est arrêté. En attendant, vos photos restent dans le téléphone et
          partiront toutes seules dans « À classer ».
        </p>
        <button type="button" className={secondaryButton} onClick={onOnline}>
          Réessayer
        </button>
      </Panel>
      <OutboxPanel outbox={outbox} />
    </div>
  );
}
