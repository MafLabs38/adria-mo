import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime, message, statusLabel } from './labels';

describe('statusLabel', () => {
  it.each([
    ['open', 'Ouvert'],
    ['reported-to-landlord', 'Signalé au bailleur'],
    ['intervention-planned', 'Intervention prévue'],
    ['in-progress', 'En cours'],
    ['resolved', 'Résolu (vérifié)'],
    ['closed', 'Clos'],
    ['reopened', 'Rouvert'],
  ])('says %s as %s', (status, label) => {
    expect(statusLabel(status).label).toBe(label);
    expect(statusLabel(status).tone).not.toBe('');
  });

  it('shows an unknown status as it is', () => {
    expect(statusLabel('demolished')).toEqual({ label: 'demolished', tone: 'bg-slate-200 text-slate-700' });
  });
});

describe('message', () => {
  it('says the rules of a password in French', () => {
    expect(message('TooShort')).toBe('Au moins 12 caractères.');
    expect(message('PasswordMismatch')).toBe("Le mot de passe actuel n'est pas le bon.");
  });

  it('shows an unknown code as it is', () => {
    expect(message('SomethingNew')).toBe('SomethingNew');
  });
});

describe('dates', () => {
  // 23:30 UTC on the 26th is 00:30 on the 27th in Paris.
  const instant = '2026-11-26T23:30:00Z';

  it('are written in the building time zone', () => {
    expect(formatDate(instant, 'Europe/Paris')).toBe('27 novembre 2026');
    expect(formatDate(instant, 'UTC')).toBe('26 novembre 2026');
    expect(formatDateTime(instant, 'Europe/Paris')).toBe('27 novembre 2026 à 00:30');
  });
});
