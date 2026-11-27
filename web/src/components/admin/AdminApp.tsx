import { useCallback, useEffect, useMemo, useState } from 'react';
import { Api, ApiError } from '../../lib/api';
import { pairingCodeOf } from '../../lib/pairing';
import { registerServiceWorker } from '../../lib/serviceWorkerClient';
import ConstatEditor from './ConstatEditor';
import ConstatList from './ConstatList';
import DevicesPanel from './DevicesPanel';
import InboxPage from './InboxPage';
import LoginForm from './LoginForm';
import NewConstat from './NewConstat';
import OfflinePage from './OfflinePage';
import PasswordForm from './PasswordForm';
import { SUPER_ADMIN, type Me } from './types';
import UsersPanel from './UsersPanel';
import { ErrorMessage, errorText, secondaryButton } from './ui';

type Session =
  | { kind: 'loading' }
  | { kind: 'anonymous'; pairingError?: unknown }
  | { kind: 'offline' }
  | { kind: 'error'; error: unknown }
  | { kind: 'in'; me: Me };

/** The session at the opening: a phone that scanned a pairing code logs in with it, any other page asks who is logged in. */
async function openSession(api: Api): Promise<Session> {
  const code = pairingCodeOf(globalThis.location.hash);
  if (code) {
    // Out of the address and of the history at once (the entry is replaced, and the page follows the new hash).
    globalThis.location.replace('#/a-classer');
    try {
      return { kind: 'in', me: await api.post<Me>('/auth/pair', { code }) };
    } catch (error) {
      return error instanceof ApiError ? { kind: 'anonymous', pairingError: error } : { kind: 'offline' };
    }
  }
  try {
    return { kind: 'in', me: await api.get<Me>('/auth/me') };
  } catch (error) {
    if (error instanceof ApiError) {
      return error.status === 401 ? { kind: 'anonymous' } : { kind: 'error', error };
    }
    // No answer at all: no network, the phone keeps the photos for later.
    return { kind: 'offline' };
  }
}

function pairingText(error: unknown): string {
  return error instanceof ApiError && error.status === 401
    ? "Ce code d'appairage n'est plus valable : affichez-en un nouveau sur l'ordinateur, ou connectez-vous."
    : errorText(error);
}

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
      <a className={link} href="#/a-classer">
        À classer
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
  /** The photos refused when a constat was created, shown on the page of that constat only. */
  failures: { number: number; files: string[] } | null;
  onCreated: (number: number, failures: string[]) => void;
}

function Page({ api, me, hash, onMe, failures, onCreated }: Readonly<PageProps>) {
  if (hash === '#/compte') {
    return (
      <div className="space-y-4">
        <PasswordForm api={api} forced={false} onChanged={onMe} />
        <DevicesPanel api={api} timeZone={me.timeZone} />
      </div>
    );
  }
  if (hash === '#/a-classer') {
    return <InboxPage api={api} timeZone={me.timeZone} />;
  }
  if (hash === '#/comptes' && me.roles.includes(SUPER_ADMIN)) {
    return <UsersPanel api={api} timeZone={me.timeZone} />;
  }
  if (hash === '#/constats/nouveau') {
    return <NewConstat api={api} onCreated={onCreated} />;
  }
  const match = /^#\/constats\/(\d+)$/.exec(hash);
  if (match?.[1]) {
    const number = Number(match[1]);
    const refused = failures?.number === number ? failures.files : [];
    return <ConstatEditor key={number} api={api} number={number} failures={refused} timeZone={me.timeZone} />;
  }
  return <ConstatList api={api} timeZone={me.timeZone} />;
}

export default function AdminApp({ apiBase }: Readonly<{ apiBase: string }>) {
  const api = useMemo(() => new Api(apiBase), [apiBase]);
  const [session, setSession] = useState<Session>({ kind: 'loading' });
  const [failures, setFailures] = useState<{ number: number; files: string[] } | null>(null);
  const hash = useHash();
  const created = (number: number, refused: string[]) => {
    setFailures({ number, files: refused });
    globalThis.location.hash = `#/constats/${number}`;
  };

  const open = useCallback(() => {
    openSession(api).then(setSession, (error: unknown) => setSession({ kind: 'error', error }));
  }, [api]);
  useEffect(open, [open]);

  // The service worker: the page without network, the photos shared from the gallery and sent later (spec §4.1).
  useEffect(() => {
    registerServiceWorker(import.meta.env.BASE_URL, apiBase).catch(() => undefined);
  }, [apiBase]);

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
    case 'offline':
      return <OfflinePage api={api} onOnline={open} />;
    case 'anonymous':
      return (
        <div className="space-y-3">
          {session.pairingError ? (
            <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-900">
              {pairingText(session.pairingError)}
            </p>
          ) : null}
          <LoginForm api={api} onLoggedIn={(me) => setSession({ kind: 'in', me })} />
        </div>
      );
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
