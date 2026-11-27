import { useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import { uploadPieces } from '../../lib/upload';
import ConstatFields, { type ConstatFieldValues } from './ConstatFields';
import PhotoPicker from './PhotoPicker';
import type { ConstatDetail } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton } from './ui';
import { useCategories } from './useCategories';

export default function NewConstat({
  api,
  onCreated,
}: Readonly<{ api: Api; onCreated: (number: number, failures: string[]) => void }>) {
  const categories = useCategories(api);
  const [values, setValues] = useState<ConstatFieldValues>({ title: '', category: '', location: '', isPublic: true });
  const [comment, setComment] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [progress, setProgress] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setError(null);
    setProgress('Création du constat…');
    try {
      const created = await api.post<ConstatDetail>('/constats', values);
      const outcomes = await uploadPieces(api, created.number, files, comment, (done, total) =>
        setProgress(total > 0 ? `Envoi des photos : ${done} sur ${total}…` : null),
      );
      onCreated(
        created.number,
        outcomes.filter((o) => o.error !== null).map((o) => o.file || 'commentaire'),
      );
    } catch (error_) {
      setError(error_);
      setProgress(null);
    }
  };

  return (
    <Panel title="Nouveau constat">
      <form onSubmit={(e) => void submit(e)} className="space-y-3">
        <PhotoPicker files={files} onChange={setFiles} />
        <ConstatFields values={values} categories={categories} onChange={setValues} />
        <label className="block font-medium">
          <span>Commentaire</span>
          <textarea
            className={inputClass}
            rows={3}
            maxLength={2000}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
          />
        </label>
        <ErrorMessage error={error} />
        {progress ? <output className="block text-sm text-brand-900">{progress}</output> : null}
        <button type="submit" className={`${primaryButton} w-full`} disabled={progress !== null}>
          Enregistrer le constat
        </button>
      </form>
    </Panel>
  );
}
