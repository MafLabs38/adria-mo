import { useState, type SyntheticEvent } from 'react';
import type { Api } from '../../lib/api';
import type { Me } from './types';
import { ErrorMessage, inputClass, Panel, primaryButton } from './ui';

export default function LoginForm({ api, onLoggedIn }: Readonly<{ api: Api; onLoggedIn: (me: Me) => void }>) {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      onLoggedIn(await api.post<Me>('/auth/login', { userName, password }));
    } catch (error_) {
      setError(error_);
      setPassword('');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel title="Connexion">
      <form onSubmit={(e) => void submit(e)} className="space-y-3">
        <label className="block font-medium">
          <span>Identifiant</span>
          <input
            className={inputClass}
            autoComplete="username"
            autoCapitalize="none"
            required
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
          />
        </label>
        <label className="block font-medium">
          <span>Mot de passe</span>
          <input
            className={inputClass}
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <ErrorMessage error={error} />
        <button type="submit" className={`${primaryButton} w-full`} disabled={busy}>
          Se connecter
        </button>
      </form>
    </Panel>
  );
}
