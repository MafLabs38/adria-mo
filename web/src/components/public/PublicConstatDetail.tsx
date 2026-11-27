import { useState } from 'react';
import { formatDate, formatDateTime } from '../../lib/labels';
import { daysOpen, openSinceLabel } from '../../lib/openSince';
import { fileUrl } from '../../lib/snapshot';
import StatusBadge from './StatusBadge';
import { useSnapshot } from './useSnapshot';

interface Props {
  data: string;
  /** The address of the list, for the way back. */
  list: string;
}

function readNumber(): number | null {
  if (globalThis.location === undefined) {
    return null;
  }
  const value = Number(new URLSearchParams(globalThis.location.search).get('n'));
  return Number.isInteger(value) && value > 0 ? value : null;
}

export default function PublicConstatDetail({ data, list }: Readonly<Props>) {
  const state = useSnapshot(data);
  const [number] = useState(readNumber);

  const back = (
    <a href={list} className="text-sm font-semibold text-brand-700 underline">
      ← Tous les constats
    </a>
  );
  if (state.kind === 'loading') {
    return <p className="text-slate-600">Chargement…</p>;
  }
  if (state.kind === 'error') {
    return (
      <div className="space-y-3">
        <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-900">
          Ce constat ne peut pas être affiché pour l'instant. Réessayez dans un moment.
        </p>
        {back}
      </div>
    );
  }
  const constat = state.kind === 'ready' ? state.snapshot.constats.find((c) => c.number === number) : undefined;
  if (state.kind !== 'ready' || constat === undefined) {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-slate-700">
          Ce constat n'est pas (ou plus) publié.
        </p>
        {back}
      </div>
    );
  }

  const timeZone = state.snapshot.timeZone;
  return (
    <article className="space-y-4">
      {back}
      <header>
        <p className="text-sm text-slate-600">Constat n° {constat.number}</p>
        <h1 className="font-display text-3xl font-extrabold text-brand-900">{constat.title}</h1>
        <p className="text-slate-700">{[constat.category, constat.location].filter(Boolean).join(' · ')}</p>
        <p className="mt-2 flex flex-wrap items-center gap-2">
          <StatusBadge status={constat.status} />
          <span>{openSinceLabel(daysOpen(new Date(constat.firstObservedAt), new Date(), timeZone))}</span>
          <span className="text-sm text-slate-600">(constaté le {formatDate(constat.firstObservedAt, timeZone)})</span>
        </p>
      </header>
      <section aria-labelledby="pieces-title">
        <h2 id="pieces-title" className="font-display text-xl font-bold text-brand-800">
          Pièces du dossier
        </h2>
        <ol className="mt-3 space-y-4 border-l-2 border-brand-200 pl-4">
          {constat.pieces.map((piece, index) => (
            <li key={`${piece.observedAt}-${index}`} className="rounded-2xl bg-white p-3 shadow-sm">
              <p className="text-sm font-semibold text-brand-800">{formatDateTime(piece.observedAt, timeZone)}</p>
              {piece.text ? <p className="mt-1 whitespace-pre-line">{piece.text}</p> : null}
              {piece.photo ? (
                <a href={fileUrl(data, piece.photo.src)} className="mt-2 block">
                  <img
                    src={fileUrl(data, piece.photo.src)}
                    width={piece.photo.width}
                    height={piece.photo.height}
                    alt={`Constat n° ${constat.number}, ${formatDate(piece.observedAt, timeZone)}`}
                    loading="lazy"
                    className="h-auto w-full rounded-xl"
                  />
                </a>
              ) : null}
            </li>
          ))}
        </ol>
      </section>
      <p className="text-xs text-slate-500">À jour au {formatDateTime(state.snapshot.publishedAt, timeZone)}</p>
    </article>
  );
}
