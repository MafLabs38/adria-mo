import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime, message, seriesTitle, statusLabel } from './labels';

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

describe('seriesTitle', () => {
  const paris = 'Europe/Paris';

  it('gives the day once and the two times of a series', () => {
    expect(seriesTitle('2026-11-23T08:15:00Z', '2026-11-23T08:20:00Z', paris)).toBe(
      '23 novembre 2026, de 09:15 à 09:20',
    );
  });

  it('gives one time for photos of the same minute', () => {
    expect(seriesTitle('2026-11-23T08:15:00Z', '2026-11-23T08:15:30Z', paris)).toBe('23 novembre 2026 à 09:15');
  });

  it('gives both days for a series across midnight, in the time zone of the building', () => {
    expect(seriesTitle('2026-11-23T22:58:00Z', '2026-11-23T23:02:00Z', paris)).toBe(
      'Du 23 novembre 2026 à 23:58 au 24 novembre 2026 à 00:02',
    );
  });
});
