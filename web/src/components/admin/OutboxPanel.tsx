import { message } from '../../lib/labels';
import PhotoPicker from './PhotoPicker';
import SharedPhotos from './SharedPhotos';
import { Panel } from './ui';
import type { Outbox } from './useOutbox';

function waitingText(count: number, stopped: Outbox['stopped']): string {
  if (count === 0) {
    return 'Aucune photo en attente dans ce téléphone.';
  }
  const photos = count === 1 ? '1 photo attend' : `${count} photos attendent`;
  if (stopped === 'session') {
    return `${photos} dans ce téléphone : reconnectez-vous pour les envoyer.`;
  }
  if (stopped === 'unreachable') {
    return `${photos} dans ce téléphone : pas de réseau, ou serveur arrêté pour l'instant ; elles partiront toutes seules.`;
  }
  if (stopped === 'server') {
    return `${photos} dans ce téléphone : le serveur n'a pas pu les enregistrer, nouvel essai plus tard.`;
  }
  return `${photos} dans ce téléphone : envoi en cours…`;
}

/** Photos sent to the inbox from the phone (spec §4.1): without network, they wait in the phone and leave by themselves. */
export default function OutboxPanel({ outbox }: Readonly<{ outbox: Outbox }>) {
  if (outbox.unavailable) {
    return (
      <Panel title="Envoyer des photos">
        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-900">
          Ce navigateur ne peut pas garder de photos (fenêtre de navigation privée ?) : ouvrez l'administration dans une
          fenêtre normale.
        </p>
      </Panel>
    );
  }
  const items = outbox.items ?? [];
  const shared = items.filter((item) => item.shared && item.refused === undefined);
  const waiting = items.filter((item) => !item.shared && item.refused === undefined);
  const refused = items.filter((item) => item.refused !== undefined);
  return (
    <Panel title="Envoyer des photos">
      <p className="text-sm text-slate-700">
        Elles arrivent dans « À classer ». Sans réseau, elles attendent dans le téléphone et partent toutes seules au
        retour du réseau.
      </p>
      {shared.length > 0 ? (
        <SharedPhotos items={shared} onConfirm={() => void outbox.confirm()} onDismiss={() => void outbox.dismiss()} />
      ) : null}
      <PhotoPicker files={[]} onChange={(files) => void outbox.add(files)} />
      <output className="block text-sm text-brand-900">
        {outbox.items === null ? 'Lecture des photos en attente…' : waitingText(waiting.length, outbox.stopped)}
      </output>
      {refused.length > 0 ? (
        <ul className="space-y-1 text-sm" aria-label="Photos mises de côté">
          {refused.map((item) => (
            <li
              key={item.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-red-50 px-2 py-1 text-red-900"
            >
              <span className="min-w-0 flex-1 truncate">
                {item.name} : {message(item.refused ?? '')}
              </span>
              {item.refused === 'ServerError' ? (
                <button type="button" className="font-semibold underline" onClick={() => void outbox.retry(item.id)}>
                  Réessayer
                </button>
              ) : null}
              <button
                type="button"
                className="underline"
                onClick={() => {
                  if (globalThis.confirm(`Retirer « ${item.name} » ? Elle sera effacée de ce téléphone.`)) {
                    void outbox.remove(item.id);
                  }
                }}
              >
                Retirer
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </Panel>
  );
}
