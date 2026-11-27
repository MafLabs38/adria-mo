import { useId, useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import ConstatFields, { type ConstatFieldValues } from './ConstatFields';
import type { AttachResult, Category, ConstatSummary, InboxItem } from './types';
import { ErrorMessage, inputClass, primaryButton, secondaryButton } from './ui';

const NEW = 'nouveau';

interface Props {
  api: Api;
  title: string;
  timeZone: string;
  photos: InboxItem[];
  constats: ConstatSummary[];
  categories: Category[];
  onAttached: (result: AttachResult) => void;
  onDiscarded: () => void;
}

/** One series of the inbox: its photos, all chosen at first, attached in one gesture to an existing or a new constat. */
export default function InboxSeriesCard({
  api,
  title,
  timeZone,
  photos,
  constats,
  categories,
  onAttached,
  onDiscarded,
}: Readonly<Props>) {
  const headingId = useId();
  const time = new Intl.DateTimeFormat('fr-FR', { timeStyle: 'short', timeZone });
  const describe = (photo: InboxItem) => {
    const when = photo.takenAt ? 'Prise à ' + time.format(new Date(photo.takenAt)) : 'Sans date';
    return `${when}, envoyée par ${photo.author}`;
  };
  // What is left out, rather than what is chosen: a photo arriving in the series is chosen, one that left is forgotten.
  const [leftOut, setLeftOut] = useState<Set<number>>(() => new Set());
  const chosen = photos.filter((photo) => !leftOut.has(photo.id)).map((photo) => photo.id);
  const [target, setTarget] = useState('');
  const [values, setValues] = useState<ConstatFieldValues>({ title: '', category: '', location: '', isPublic: true });
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const toggle = (id: number) =>
    setLeftOut((current) => {
      const next = new Set(current);
      if (!next.delete(id)) {
        next.add(id);
      }
      return next;
    });

  const attach = async (event: SyntheticEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const destination = target === NEW ? { create: values } : { constat: Number(target) };
      onAttached(await api.post<AttachResult>('/inbox/attach', { photos: chosen, text: comment, ...destination }));
    } catch (error_) {
      setError(error_);
    } finally {
      setBusy(false);
    }
  };

  const discard = async () => {
    const count = chosen.length;
    if (
      !globalThis.confirm(
        count === 1
          ? 'Écarter cette photo ? Elle sera effacée.'
          : `Écarter ces ${count} photos ? Elles seront effacées.`,
      )
    ) {
      return;
    }
    setBusy(true);
    try {
      for (const id of chosen) {
        await api.delete(`/inbox/${id}`);
      }
      onDiscarded();
    } catch (error_) {
      setError(error_);
    } finally {
      setBusy(false);
    }
  };

  const count = chosen.length;
  return (
    <section aria-labelledby={headingId} className="rounded-2xl bg-white p-4 shadow-sm">
      <h2 id={headingId} className="font-display text-lg font-bold text-brand-800">
        {title}
      </h2>
      <form onSubmit={(e) => void attach(e)} className="mt-3 space-y-3">
        <fieldset>
          <legend className="sr-only">Photos de la série</legend>
          <ul className="grid grid-cols-3 gap-2">
            {photos.map((photo, index) => (
              <li key={photo.id}>
                <label className="relative block cursor-pointer">
                  <img
                    src={api.photoUrl(photo.thumbnail)}
                    alt={describe(photo)}
                    loading="lazy"
                    className={`aspect-square w-full rounded-lg object-cover ${leftOut.has(photo.id) ? 'opacity-40' : ''}`}
                  />
                  <input
                    type="checkbox"
                    className="absolute top-1 left-1 h-6 w-6"
                    checked={!leftOut.has(photo.id)}
                    onChange={() => toggle(photo.id)}
                    aria-label={`Choisir la photo ${index + 1} sur ${photos.length}`}
                  />
                </label>
              </li>
            ))}
          </ul>
        </fieldset>
        <label className="block font-medium">
          <span>Rattacher à</span>
          <select className={inputClass} required value={target} onChange={(e) => setTarget(e.target.value)}>
            <option value="">Choisir…</option>
            <option value={NEW}>Un nouveau constat</option>
            {constats.map((c) => (
              <option key={c.number} value={c.number}>
                n° {c.number} · {c.title}
              </option>
            ))}
          </select>
        </label>
        {target === NEW ? <ConstatFields values={values} categories={categories} onChange={setValues} /> : null}
        <label className="block font-medium">
          <span>Commentaire</span>
          <textarea
            className={inputClass}
            rows={2}
            maxLength={2000}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
          />
        </label>
        <ErrorMessage error={error} />
        <div className="flex gap-2">
          <button type="submit" className={`${primaryButton} flex-1`} disabled={busy || count === 0}>
            {count === 1 ? 'Rattacher 1 photo' : `Rattacher ${count} photos`}
          </button>
          <button
            type="button"
            className={secondaryButton}
            disabled={busy || count === 0}
            onClick={() => void discard()}
          >
            Écarter
          </button>
        </div>
      </form>
    </section>
  );
}
