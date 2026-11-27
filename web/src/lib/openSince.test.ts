import { describe, expect, it } from 'vitest';

import { daysOpen, openSinceLabel } from './openSince';

describe('daysOpen', () => {
  it('counts zero on the day of the first observation, whatever the hour', () => {
    expect(daysOpen(new Date('2026-11-27T07:00:00Z'), new Date('2026-11-27T22:59:00Z'))).toBe(0);
  });

  it('counts calendar days in Paris, not in UTC (winter time)', () => {
    // 23:30 UTC on the 26th is 00:30 on the 27th in Paris: the same day as 11:00 on the 27th.
    expect(daysOpen(new Date('2026-11-26T23:30:00Z'), new Date('2026-11-27T10:00:00Z'))).toBe(0);
  });

  it('counts calendar days in Paris, not in UTC (summer time)', () => {
    // 22:30 UTC on 1 July is 00:30 on 2 July in Paris; 21:30 UTC on 2 July is 23:30 the same day.
    expect(daysOpen(new Date('2026-07-01T22:30:00Z'), new Date('2026-07-02T21:30:00Z'))).toBe(0);
  });

  it('counts one day across midnight in Paris, even a few minutes apart', () => {
    expect(daysOpen(new Date('2026-11-26T22:50:00Z'), new Date('2026-11-26T23:10:00Z'))).toBe(1);
  });

  it('counts across months, years and a change of time', () => {
    expect(daysOpen(new Date('2025-12-31T12:00:00Z'), new Date('2026-04-05T12:00:00Z'))).toBe(95);
  });

  it('counts zero when the clock of the visitor is behind the first observation', () => {
    expect(daysOpen(new Date('2026-11-28T12:00:00Z'), new Date('2026-11-27T12:00:00Z'))).toBe(0);
  });

  it('refuses a date that is not valid', () => {
    expect(() => daysOpen(new Date('not a date'), new Date('2026-11-27T12:00:00Z'))).toThrow(RangeError);
  });
});

describe('openSinceLabel', () => {
  it.each([
    [0, "ouvert aujourd'hui"],
    [1, 'ouvert depuis 1 jour'],
    [2, 'ouvert depuis 2 jours'],
    [94, 'ouvert depuis 94 jours'],
  ])('labels %i day(s) as "%s"', (days, label) => {
    expect(openSinceLabel(days)).toBe(label);
  });

  it.each([-1, 1.5, Number.NaN])('refuses %s', (days) => {
    expect(() => openSinceLabel(days)).toThrow(RangeError);
  });
});

describe('daysOpen in another time zone', () => {
  it('counts the days of the time zone the snapshot gives', () => {
    // 23:30 UTC on the 26th: the 27th in Paris, still the 26th in New York.
    const first = new Date('2026-11-26T23:30:00Z');
    const today = new Date('2026-11-27T12:00:00Z');
    expect(daysOpen(first, today, 'Europe/Paris')).toBe(0);
    expect(daysOpen(first, today, 'America/New_York')).toBe(1);
  });
});
