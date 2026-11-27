import { useEffect, useState } from 'react';
import type { Api } from '../../lib/api';
import StatusBadge from '../public/StatusBadge';
import type { ConstatSummary, PublicationState } from './types';
import { ErrorMessage, primaryButton } from './ui';

/** When the public page was last published, and whether the last publication failed (it is then tried again). */
function Publication({ state, timeZone }: Readonly<{ state: PublicationState | null; timeZone: string }>) {
  if (state === null) {
    return null;
  }
  const when = (instant: string) =>
    new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short', timeZone }).format(new Date(instant));
  const shown = state.lastSuccessAt
    ? `Page publique à jour au ${when(state.lastSuccessAt)}.`
    : 'Page publique pas encore publiée.';
  if (state.lastError) {
    return (
      <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-900">
        La dernière publication a échoué ; elle est retentée toute seule. {shown}
      </p>
    );
  }
  return <p className="text-sm text-slate-600">{shown}</p>;
}

export default function ConstatList({ api, timeZone }: Readonly<{ api: Api; timeZone: string }>) {
  const [constats, setConstats] = useState<ConstatSummary[] | null>(null);
  const [publication, setPublication] = useState<PublicationState | null>(null);
  const [error, setError] = useState<unknown>(null);
  useEffect(() => {
    api.get<ConstatSummary[]>('/constats').then(setConstats, setError);
    api.get<PublicationState>('/publication').then(setPublication, () => setPublication(null));
  }, [api]);

  return (
    <div className="space-y-4">
      <a href="#/constats/nouveau" className={`${primaryButton} block text-center text-lg`}>
        + Constater
      </a>
      <Publication state={publication} timeZone={timeZone} />
      <ErrorMessage error={error} />
      {constats?.length === 0 ? <p className="text-slate-600">Aucun constat pour l'instant.</p> : null}
      <ul className="space-y-3">
        {(constats ?? []).map((c) => (
          <li key={c.number}>
            <a
              href={`#/constats/${c.number}`}
              className="flex gap-3 rounded-2xl bg-white p-3 shadow-sm ring-1 ring-brand-100 hover:ring-brand-400"
            >
              {c.thumbnail ? (
                <img
                  src={api.photoUrl(c.thumbnail)}
                  alt=""
                  loading="lazy"
                  className="h-16 w-16 flex-none rounded-xl object-cover"
                />
              ) : (
                <div aria-hidden="true" className="h-16 w-16 flex-none rounded-xl bg-brand-100" />
              )}
              <div className="min-w-0">
                <p className="truncate font-semibold text-brand-900">
                  n° {c.number} · {c.title}
                </p>
                <p className="mt-1 flex flex-wrap items-center gap-2 text-sm">
                  <StatusBadge status={c.status} />
                  <span>
                    {c.pieceCount} pièce{c.pieceCount > 1 ? 's' : ''}
                  </span>
                  {c.isPublic ? null : <span className="rounded-full bg-slate-200 px-2 text-xs">non publié</span>}
                </p>
              </div>
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
