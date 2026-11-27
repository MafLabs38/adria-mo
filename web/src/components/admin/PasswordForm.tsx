import { useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import type { Me } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton } from './ui';

interface Props {
  api: Api;
  /** A first login with a temporary password: the account must choose its own (spec §2.1). */
  forced: boolean;
  onChanged: (me: Me) => void;
}

export default function PasswordForm({ api, forced, onChanged }: Readonly<Props>) {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);
  const mismatch = confirm.length > 0 && next !== confirm;

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    if (mismatch) {
      return;
    }
    setBusy(true);
    setError(null);
    setDone(false);
    try {
      const me = await api.post<Me>('/auth/password', { currentPassword: current, newPassword: next });
      setCurrent('');
      setNext('');
      setConfirm('');
      setDone(true);
      onChanged(me);
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel title={forced ? 'Choisissez votre mot de passe' : 'Changer mon mot de passe'}>
      {forced ? (
        <p className="text-sm text-slate-700">
          Le mot de passe que l'on vous a donné est provisoire. Choisissez le vôtre : personne d'autre ne le connaîtra.
        </p>
      ) : null}
      <p className="text-sm text-slate-600">
        Au moins 12 caractères. Une phrase facile à retenir est plus sûre qu'un mot compliqué (« le chat dort sur le
        radiateur »). Vos autres sessions seront fermées.
      </p>
      <form onSubmit={(e) => void submit(e)} className="space-y-3">
        <label className="block font-medium">
          {forced ? 'Mot de passe provisoire' : 'Mot de passe actuel'}
          <input
            className={inputClass}
            type="password"
            autoComplete="current-password"
            required
            value={current}
            onChange={(e) => setCurrent(e.target.value)}
          />
        </label>
        <label className="block font-medium">
          Nouveau mot de passe
          <input
            className={inputClass}
            type="password"
            autoComplete="new-password"
            required
            minLength={12}
            maxLength={128}
            value={next}
            onChange={(e) => setNext(e.target.value)}
          />
        </label>
        <label className="block font-medium">
          Nouveau mot de passe, une seconde fois
          <input
            className={inputClass}
            type="password"
            autoComplete="new-password"
            required
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            aria-invalid={mismatch}
          />
        </label>
        {mismatch ? <p className="text-sm text-red-800">Les deux saisies sont différentes.</p> : null}
        <ErrorMessage error={error} />
        {done ? (
          <p role="status" className="rounded-lg bg-brand-50 p-3 text-sm text-brand-900">
            Mot de passe changé.
          </p>
        ) : null}
        <button type="submit" className={`${primaryButton} w-full`} disabled={busy || mismatch}>
          Enregistrer le mot de passe
        </button>
      </form>
    </Panel>
  );
}
