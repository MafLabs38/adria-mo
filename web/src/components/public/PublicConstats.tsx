import { useMemo, useState } from 'react';
import { formatDateTime, statusLabel } from '../../lib/labels';
import { daysOpen, openSinceLabel } from '../../lib/openSince';
import { coverPhoto, fileUrl, type PublicConstat } from '../../lib/snapshot';
import StatusBadge from './StatusBadge';
import { useSnapshot } from './useSnapshot';

interface Props {
  /** The folder of the published data. */
  data: string;
  /** The address of the page of one constat. */
  detail: string;
}

function Card({
  constat,
  data,
  detail,
  timeZone,
}: Readonly<{ constat: PublicConstat; data: string; detail: string; timeZone: string }>) {
  const cover = coverPhoto(constat);
  const days = daysOpen(new Date(constat.firstObservedAt), new Date(), timeZone);
  return (
    <li>
      <a
        href={`${detail}?n=${constat.number}`}
        className="flex gap-3 rounded-2xl bg-white p-3 shadow-sm ring-1 ring-brand-100 hover:ring-brand-400 focus-visible:outline-2 focus-visible:outline-brand-600"
      >
        {cover ? (
          <img
            src={fileUrl(data, cover.thumbnail)}
            alt=""
            loading="lazy"
            className="h-20 w-20 flex-none rounded-xl object-cover"
          />
        ) : (
          <div aria-hidden="true" className="h-20 w-20 flex-none rounded-xl bg-brand-100" />
        )}
        <div className="min-w-0">
          <p className="truncate font-display text-lg font-bold text-brand-900">{constat.title}</p>
          <p className="truncate text-sm text-slate-600">
            {[constat.category, constat.location].filter(Boolean).join(' · ')}
          </p>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm">
            <StatusBadge status={constat.status} />
            <span className="text-slate-700">{openSinceLabel(days)}</span>
          </p>
        </div>
      </a>
    </li>
  );
}

export default function PublicConstats({ data, detail }: Readonly<Props>) {
  const state = useSnapshot(data);
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState('');

  const constats = useMemo(() => (state.kind === 'ready' ? state.snapshot.constats : []), [state]);
  const categories = useMemo(
    () => [...new Set(constats.map((c) => c.category))].sort((a, b) => a.localeCompare(b, 'fr')),
    [constats],
  );
  const statuses = useMemo(() => [...new Set(constats.map((c) => c.status))], [constats]);
  const shown = constats.filter((c) => (!category || c.category === category) && (!status || c.status === status));

  if (state.kind === 'loading') {
    return <p className="text-slate-600">Chargement des constats…</p>;
  }
  if (state.kind === 'error') {
    return (
      <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-900">
        Les constats ne peuvent pas être affichés pour l'instant. Réessayez dans un moment.
      </p>
    );
  }
  if (state.kind === 'none' || constats.length === 0) {
    return <p className="text-slate-600">Aucun constat publié pour l'instant.</p>;
  }

  return (
    <div>
      <div className="mb-4 grid grid-cols-2 gap-2">
        <label className="text-sm font-medium text-brand-900">
          Catégorie
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className="mt-1 block w-full rounded-lg border-slate-300 bg-white p-2"
          >
            <option value="">Toutes</option>
            {categories.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-medium text-brand-900">
          État
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            className="mt-1 block w-full rounded-lg border-slate-300 bg-white p-2"
          >
            <option value="">Tous</option>
            {statuses.map((s) => (
              <option key={s} value={s}>
                {statusLabel(s).label}
              </option>
            ))}
          </select>
        </label>
      </div>
      <p className="mb-2 text-sm text-slate-600" aria-live="polite">
        {shown.length} constat{shown.length > 1 ? 's' : ''}
      </p>
      <ul className="space-y-3">
        {shown.map((c) => (
          <Card key={c.number} constat={c} data={data} detail={detail} timeZone={state.snapshot.timeZone} />
        ))}
      </ul>
      <p className="mt-6 text-xs text-slate-500">
        À jour au {formatDateTime(state.snapshot.publishedAt, state.snapshot.timeZone)}
      </p>
    </div>
  );
}
