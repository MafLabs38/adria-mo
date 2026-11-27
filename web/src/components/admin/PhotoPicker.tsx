import { useId } from 'react';

interface Props {
  files: File[];
  onChange: (files: File[]) => void;
}

/** Two ways in (spec §4): the camera, straight from the page, or several photos from the gallery. */
export default function PhotoPicker({ files, onChange }: Readonly<Props>) {
  const cameraId = useId();
  const galleryId = useId();
  const add = (input: HTMLInputElement) => {
    onChange([...files, ...Array.from(input.files ?? [])]);
    // Emptied, so the same photo can be chosen again after "Retirer".
    input.value = '';
  };
  const button =
    'flex-1 cursor-pointer rounded-xl bg-accent-400 px-3 py-3 text-center font-semibold text-slate-900 hover:bg-accent-500 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-brand-700';

  return (
    <fieldset className="space-y-2">
      <legend className="font-medium">Photos</legend>
      <div className="flex gap-2">
        <label htmlFor={cameraId} className={button}>
          <span aria-hidden="true">📷 </span>
          <span>Prendre une photo</span>
          <input
            id={cameraId}
            type="file"
            accept="image/*"
            capture="environment"
            className="sr-only"
            onChange={(e) => add(e.target)}
          />
        </label>
        <label htmlFor={galleryId} className={button}>
          <span aria-hidden="true">🖼️ </span>
          <span>Depuis la galerie</span>
          <input
            id={galleryId}
            type="file"
            accept="image/*"
            multiple
            className="sr-only"
            onChange={(e) => add(e.target)}
          />
        </label>
      </div>
      {files.length > 0 ? (
        <ul className="space-y-1 text-sm">
          {files.map((file, index) => (
            <li
              key={`${file.name}-${index}`}
              className="flex items-center justify-between gap-2 rounded-lg bg-brand-50 px-2 py-1"
            >
              <span className="truncate">{file.name}</span>
              <button
                type="button"
                className="text-red-800 underline"
                onClick={() => onChange(files.filter((_, i) => i !== index))}
              >
                Retirer
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </fieldset>
  );
}
