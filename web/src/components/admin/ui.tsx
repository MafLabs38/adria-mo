import type { ReactNode } from 'react';
import { ApiError } from '../../lib/api';
import { message } from '../../lib/labels';

export const inputClass = 'mt-1 block w-full rounded-lg border border-slate-300 bg-white p-2.5 text-base';
export const primaryButton =
  'rounded-xl bg-brand-700 px-4 py-3 font-semibold text-white hover:bg-brand-800 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600';
export const secondaryButton =
  'rounded-xl bg-white px-4 py-3 font-semibold text-brand-800 ring-1 ring-brand-300 hover:bg-brand-50 disabled:opacity-60';

/** The French text of an error of the API, or of the network. */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return 'Votre session a pris fin : reconnectez-vous.';
    }
    if (error.status === 429) {
      return 'Trop de tentatives : réessayez dans une minute.';
    }
    const codes = error.allCodes;
    return codes.length > 0 ? codes.map(message).join(' ') : error.message;
  }
  return 'Le serveur ne répond pas. Vérifiez la connexion et réessayez.';
}

export function ErrorMessage({ error }: Readonly<{ error: unknown }>) {
  if (error === null || error === undefined) {
    return null;
  }
  return (
    <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-900">
      {errorText(error)}
    </p>
  );
}

export function Panel({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <section className="rounded-2xl bg-white p-4 shadow-sm">
      <h2 className="font-display text-xl font-bold text-brand-800">{title}</h2>
      <div className="mt-3 space-y-3">{children}</div>
    </section>
  );
}
