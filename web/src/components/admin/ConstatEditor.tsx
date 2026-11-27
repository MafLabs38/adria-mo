import { useEffect, useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import { uploadPieces } from '../../lib/upload';
import StatusBadge from '../public/StatusBadge';
import ConstatFields, { type ConstatFieldValues } from './ConstatFields';
import PhotoPicker from './PhotoPicker';
import type { ConstatDetail, PieceDetail } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton, secondaryButton } from './ui';
import { useCategories } from './useCategories';

function PieceItem({ api, piece, timeZone }: Readonly<{ api: Api; piece: PieceDetail; timeZone: string }>) {
  const when = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'long', timeStyle: 'short', timeZone });
  const dated = piece.takenAt
    ? `Photo prise le ${when.format(new Date(piece.takenAt))}`
    : `Ajouté le ${when.format(new Date(piece.addedAt))}`;
  return (
    <li className="rounded-xl bg-brand-50 p-3">
      <p className="text-sm font-semibold text-brand-900">
        {piece.photo && !piece.takenAt ? `${dated} (photo sans date)` : dated}
      </p>
      {piece.text ? <p className="mt-1 whitespace-pre-line">{piece.text}</p> : null}
      {piece.photo && piece.thumbnail ? (
        <a href={api.photoUrl(piece.photo)} target="_blank" rel="noreferrer" className="mt-2 block">
          <img
            src={api.photoUrl(piece.thumbnail)}
            alt="Pièce du constat"
            loading="lazy"
            className="max-h-60 rounded-lg"
          />
        </a>
      ) : null}
      {piece.originalSha256 ? (
        <p className="mt-1 break-all font-mono text-xs text-slate-500" title="Empreinte SHA-256 de l'original">
          SHA-256 {piece.originalSha256}
        </p>
      ) : null}
    </li>
  );
}

export default function ConstatEditor({
  api,
  number,
  failures,
  timeZone,
}: Readonly<{ api: Api; number: number; failures: string[]; timeZone: string }>) {
  const categories = useCategories(api);
  const [constat, setConstat] = useState<ConstatDetail | null>(null);
  const [values, setValues] = useState<ConstatFieldValues | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [comment, setComment] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(
    failures.length > 0 ? `Photos refusées : ${failures.join(', ')}` : null,
  );
  const [busy, setBusy] = useState(false);

  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let active = true;
    api.get<ConstatDetail>(`/constats/${number}`).then(
      (detail) => {
        if (active) {
          setConstat(detail);
          setValues({
            title: detail.title,
            category: detail.category,
            location: detail.location ?? '',
            isPublic: detail.isPublic,
          });
        }
      },
      (error_: unknown) => {
        if (active) {
          setError(error_);
        }
      },
    );
    return () => {
      active = false;
    };
  }, [api, number, version]);

  const save = async (event: SyntheticEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.put(`/constats/${number}`, values);
      setNotice('Constat enregistré.');
      reload();
    } catch (error_) {
      setError(error_);
    } finally {
      setBusy(false);
    }
  };

  const addPieces = async (event: SyntheticEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const outcomes = await uploadPieces(api, number, files, comment, (done, total) =>
      setNotice(`Envoi : ${done} sur ${total}…`),
    );
    const refused = outcomes.filter((o) => o.error !== null);
    setNotice(refused.length > 0 ? null : 'Pièces ajoutées.');
    setError(refused.length > 0 ? refused[0]?.error : null);
    setFiles([]);
    setComment('');
    setBusy(false);
    reload();
  };

  if (constat === null || values === null) {
    return error ? <ErrorMessage error={error} /> : <p>Chargement…</p>;
  }

  return (
    <div className="space-y-4">
      <a href="#/constats" className="text-sm font-semibold text-brand-700 underline">
        ← Tous les constats
      </a>
      <header>
        <p className="text-sm text-slate-600">Constat n° {constat.number}</p>
        <h1 className="font-display text-2xl font-extrabold text-brand-900">{constat.title}</h1>
        <p className="mt-1 flex gap-2">
          <StatusBadge status={constat.status} />
          <span className="text-sm">{constat.isPublic ? 'Publié' : 'Non publié'}</span>
        </p>
      </header>
      {notice ? <output className="block rounded-lg bg-brand-50 p-3 text-sm text-brand-900">{notice}</output> : null}
      <ErrorMessage error={error} />
      <Panel title="Ajouter des pièces">
        <form onSubmit={(e) => void addPieces(e)} className="space-y-3">
          <PhotoPicker files={files} onChange={setFiles} />
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
          <button
            type="submit"
            className={`${primaryButton} w-full`}
            disabled={busy || (files.length === 0 && comment.trim() === '')}
          >
            Ajouter
          </button>
        </form>
      </Panel>
      <Panel title={`Pièces (${constat.pieces.length})`}>
        <ol className="space-y-3">
          {constat.pieces.map((piece) => (
            <PieceItem key={piece.id} api={api} piece={piece} timeZone={timeZone} />
          ))}
        </ol>
      </Panel>
      <Panel title="Modifier">
        <form onSubmit={(e) => void save(e)} className="space-y-3">
          <ConstatFields values={values} categories={categories} onChange={setValues} />
          <button type="submit" className={`${secondaryButton} w-full`} disabled={busy}>
            Enregistrer les modifications
          </button>
        </form>
      </Panel>
    </div>
  );
}
