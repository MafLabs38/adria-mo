import { useCallback, useEffect, useState } from 'react';
import type { Api } from '../../lib/api';
import { seriesTitle } from '../../lib/labels';
import InboxSeriesCard from './InboxSeriesCard';
import OutboxPanel from './OutboxPanel';
import type { AttachResult, ConstatSummary, InboxListing } from './types';
import { ErrorMessage } from './ui';
import { useCategories } from './useCategories';
import { useOutbox } from './useOutbox';

function attachedText(result: AttachResult): string {
  const photos = result.attached === 1 ? '1 photo rattachée' : `${result.attached} photos rattachées`;
  if (result.alreadyThere === 0) {
    return `${photos} au constat n° ${result.constat}.`;
  }
  const already = result.alreadyThere === 1 ? '1 y était déjà' : `${result.alreadyThere} y étaient déjà`;
  return `${photos} au constat n° ${result.constat} (${already}).`;
}

/**
 * The inbox "À classer" (spec §4.1): the photos shared from the gallery or sent later, by the date they were taken, in series of
 * photos taken a few minutes apart; each series goes to a constat in one gesture.
 */
export default function InboxPage({ api, timeZone }: Readonly<{ api: Api; timeZone: string }>) {
  const categories = useCategories(api);
  const [listing, setListing] = useState<InboxListing | null>(null);
  const [constats, setConstats] = useState<ConstatSummary[]>([]);
  const [done, setDone] = useState<AttachResult | null>(null);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(() => {
    api.get<InboxListing>('/inbox').then(setListing, setError);
    api
      .get<ConstatSummary[]>('/constats')
      .then((list) => setConstats(list.filter((c) => c.status !== 'closed')), setError);
  }, [api]);
  useEffect(load, [load]);
  const outbox = useOutbox(api.base, load);

  const groups = listing
    ? [
        ...listing.series.map((s) => ({ title: seriesTitle(s.startedAt, s.endedAt, timeZone), photos: s.photos })),
        ...(listing.undated.length > 0 ? [{ title: 'Photos sans date de prise de vue', photos: listing.undated }] : []),
      ]
    : [];

  return (
    <div className="space-y-4">
      <h1 className="font-display text-2xl font-bold text-brand-800">À classer</h1>
      <OutboxPanel outbox={outbox} />
      {done ? (
        <output className="block rounded-lg bg-brand-100 p-3 text-sm text-brand-900">
          {attachedText(done)}{' '}
          <a className="font-semibold underline" href={`#/constats/${done.constat}`}>
            Voir le constat
          </a>
        </output>
      ) : null}
      <ErrorMessage error={error} />
      {listing && groups.length === 0 ? <p>Rien à classer : toutes les photos sont rangées.</p> : null}
      {groups.map((group) => (
        <InboxSeriesCard
          // The first photo names the series: a photo arriving later keeps what was typed in the card.
          key={group.photos[0]?.id ?? group.title}
          api={api}
          title={group.title}
          timeZone={timeZone}
          photos={group.photos}
          constats={constats}
          categories={categories}
          onAttached={(result) => {
            setDone(result);
            load();
          }}
          onDiscarded={load}
        />
      ))}
    </div>
  );
}
