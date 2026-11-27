import { useEffect, useMemo, useState } from 'react';
import { Api, ApiError } from '../../lib/api';
import ConstatEditor from './ConstatEditor';
import ConstatList from './ConstatList';
import LoginForm from './LoginForm';
import NewConstat from './NewConstat';
import PasswordForm from './PasswordForm';
import { SUPER_ADMIN, type Me } from './types';
import UsersPanel from './UsersPanel';
import { ErrorMessage, secondaryButton } from './ui';

type Session = { kind: 'loading' } | { kind: 'anonymous' } | { kind: 'error'; error: unknown } | { kind: 'in'; me: Me };

function useHash(): string {
  const [hash, setHash] = useState(() => globalThis.location.hash);
  useEffect(() => {
    const update = () => setHash(globalThis.location.hash);
    globalThis.addEventListener('hashchange', update);
    return () => globalThis.removeEventListener('hashchange', update);
  }, []);
  return hash;
}

function Nav({ me, onLogout }: Readonly<{ me: Me; onLogout: () => void }>) {
  const link = 'rounded-lg px-3 py-2 font-semibold text-brand-800 hover:bg-brand-100';
  return (
    <nav aria-label="Administration" className="mb-4 flex flex-wrap items-center gap-1">
      <a className={link} href="#/constats">
        Constats
      </a>
      <a className={link} href="#/compte">
        Mon compte
      </a>
      {me.roles.includes(SUPER_ADMIN) ? (
        <a className={link} href="#/comptes">
          Comptes
        </a>
      ) : null}
      <button type="button" className={`${link} ml-auto`} onClick={onLogout}>
        Se déconnecter ({me.userName})
      </button>
    </nav>
  );
}

interface PageProps {
  api: Api;
  me: Me;
  hash: string;
  onMe: (me: Me) => void;
  /** The photos refused when the constat was created, shown on its page. */
  failures: string[];
  onCreated: (number: number, failures: string[]) => void;
}

function Page({ api, me, hash, onMe, failures, onCreated }: Readonly<PageProps>) {
  if (hash === '#/compte') {
    return <PasswordForm api={api} forced={false} onChanged={onMe} />;
  }
  if (hash === '#/comptes' && me.roles.includes(SUPER_ADMIN)) {
    return <UsersPanel api={api} />;
  }
  if (hash === '#/constats/nouveau') {
    return <NewConstat api={api} onCreated={onCreated} />;
  }
  const match = /^#\/constats\/(\d+)$/.exec(hash);
  if (match?.[1]) {
    const number = Number(match[1]);
    return <ConstatEditor key={number} api={api} number={number} failures={failures} timeZone={me.timeZone} />;
  }
  return <ConstatList api={api} timeZone={me.timeZone} />;
}

export default function AdminApp({ apiBase }: Readonly<{ apiBase: string }>) {
  const api = useMemo(() => new Api(apiBase), [apiBase]);
  const [session, setSession] = useState<Session>({ kind: 'loading' });
  const [failures, setFailures] = useState<string[]>([]);
  const hash = useHash();
  const created = (number: number, refused: string[]) => {
    setFailures(refused);
    globalThis.location.hash = `#/constats/${number}`;
  };

  useEffect(() => {
    api.get<Me>('/auth/me').then(
      (me) => setSession({ kind: 'in', me }),
      (error: unknown) =>
        setSession(
          error instanceof ApiError && error.status === 401 ? { kind: 'anonymous' } : { kind: 'error', error },
        ),
    );
  }, [api]);

  const logout = async () => {
    await api.post('/auth/logout').catch(() => undefined);
    setSession({ kind: 'anonymous' });
  };

  switch (session.kind) {
    case 'loading':
      return <p>Chargement…</p>;
    case 'error':
      return (
        <div className="space-y-3">
          <ErrorMessage error={session.error} />
          <button type="button" className={secondaryButton} onClick={() => globalThis.location.reload()}>
            Réessayer
          </button>
        </div>
      );
    case 'anonymous':
      return <LoginForm api={api} onLoggedIn={(me) => setSession({ kind: 'in', me })} />;
    case 'in': {
      const { me } = session;
      if (me.mustChangePassword) {
        return <PasswordForm api={api} forced onChanged={(next) => setSession({ kind: 'in', me: next })} />;
      }
      return (
        <div>
          <Nav me={me} onLogout={() => void logout()} />
          <Page
            api={api}
            me={me}
            hash={hash}
            onMe={(next) => setSession({ kind: 'in', me: next })}
            failures={failures}
            onCreated={created}
          />
        </div>
      );
    }
  }
}
