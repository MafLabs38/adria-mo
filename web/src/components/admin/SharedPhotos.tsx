import { useEffect, useMemo } from 'react';
import type { OutboxItem } from '../../lib/outbox';
import { primaryButton, secondaryButton } from './ui';

/**
 * The photos just shared from the gallery, shown before they leave the phone (spec §4.1): a page of another site can post to
 * the share target too, so nothing it slips in is sent without its owner seeing it.
 */
export default function SharedPhotos({
  items,
  onConfirm,
  onDismiss,
}: Readonly<{ items: OutboxItem[]; onConfirm: () => void; onDismiss: () => void }>) {
  const previews = useMemo(
    () => items.map((item) => ({ id: item.id, name: item.name, url: URL.createObjectURL(item.file) })),
    [items],
  );
  useEffect(() => () => previews.forEach((preview) => URL.revokeObjectURL(preview.url)), [previews]);

  const count = items.length;
  return (
    <div className="space-y-2 rounded-xl bg-accent-400/20 p-3">
      <p className="font-semibold">
        {count === 1 ? '1 photo partagée depuis la galerie' : `${count} photos partagées depuis la galerie`}
      </p>
      <ul className="grid grid-cols-4 gap-1">
        {previews.map((preview) => (
          <li key={preview.id}>
            <img src={preview.url} alt={preview.name} className="aspect-square w-full rounded object-cover" />
          </li>
        ))}
      </ul>
      <div className="flex gap-2">
        <button type="button" className={`${primaryButton} flex-1`} onClick={onConfirm}>
          {count === 1 ? 'Envoyer cette photo' : `Envoyer ces ${count} photos`}
        </button>
        <button type="button" className={secondaryButton} onClick={onDismiss}>
          Ignorer
        </button>
      </div>
    </div>
  );
}
