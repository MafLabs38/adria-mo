import { useCallback, useEffect, useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import { suggestPassphrase } from '../../lib/passphrase';
import type { UserSummary } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton, secondaryButton } from './ui';

/** The temporary password to hand over, shown once to the super-admin. */
function Handover({ userName, password }: Readonly<{ userName: string; password: string }>) {
  return (
    <output className="block rounded-lg bg-accent-400/30 p-3 text-sm">
      Donnez à <strong>{userName}</strong> ce mot de passe provisoire, de vive voix :{' '}
      <code className="font-mono text-base font-bold">{password}</code>
      <br />À sa première connexion, il devra en choisir un à lui.
    </output>
  );
}

export default function UsersPanel({ api, timeZone }: Readonly<{ api: Api; timeZone: string }>) {
  const when = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'short', timeStyle: 'short', timeZone });
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState(() => suggestPassphrase());
  const [handover, setHandover] = useState<{ userName: string; password: string } | null>(null);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(() => api.get<UserSummary[]>('/users').then(setUsers, setError), [api]);
  useEffect(() => void load(), [load]);

  const [busy, setBusy] = useState(false);

  const create = async (event: SyntheticEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.post('/users', { userName, temporaryPassword: password });
      setHandover({ userName, password });
      setUserName('');
      setPassword(suggestPassphrase());
      await load();
    } catch (error_) {
      setError(error_);
    } finally {
      setBusy(false);
    }
  };

  const reset = async (user: UserSummary) => {
    const confirmed = globalThis.confirm(
      `Réinitialiser le mot de passe de ${user.userName} ? Ses sessions seront fermées et il devra en choisir un nouveau.`,
    );
    if (!confirmed) {
      return;
    }
    const temporary = suggestPassphrase();
    setError(null);
    setBusy(true);
    try {
      await api.post(`/users/${encodeURIComponent(user.id)}/password`, { temporaryPassword: temporary });
      setHandover({ userName: user.userName, password: temporary });
      await load();
    } catch (error_) {
      setError(error_);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4">
      {handover ? <Handover {...handover} /> : null}
      <ErrorMessage error={error} />
      <Panel title="Comptes">
        <ul className="divide-y divide-slate-100">
          {users.map((u) => (
            <li key={u.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <div>
                <p className="font-semibold">{u.userName}</p>
                <p className="text-xs text-slate-600">
                  {u.roles.join(', ')}
                  {u.mustChangePassword ? ' · mot de passe provisoire' : ''}
                  {u.lockedOutUntil ? ` · bloqué jusqu'à ${when.format(new Date(u.lockedOutUntil))}` : ''}
                </p>
              </div>
              <button type="button" className={secondaryButton} disabled={busy} onClick={() => void reset(u)}>
                Réinitialiser le mot de passe
              </button>
            </li>
          ))}
        </ul>
      </Panel>
      <Panel title="Nouvel administrateur">
        <form onSubmit={(e) => void create(e)} className="space-y-3">
          <label className="block font-medium">
            <span>Identifiant</span>
            <input
              className={inputClass}
              required
              autoCapitalize="none"
              autoComplete="off"
              pattern="[A-Za-z0-9._\-]{3,64}"
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
            />
          </label>
          <div className="flex items-end gap-2">
            <label className="block flex-1 font-medium">
              <span>Mot de passe provisoire</span>
              <input
                className={inputClass}
                required
                minLength={12}
                autoComplete="off"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </label>
            <button type="button" className={secondaryButton} onClick={() => setPassword(suggestPassphrase())}>
              Autre
            </button>
          </div>
          <button type="submit" className={`${primaryButton} w-full`} disabled={busy}>
            Créer le compte
          </button>
        </form>
      </Panel>
    </div>
  );
}
