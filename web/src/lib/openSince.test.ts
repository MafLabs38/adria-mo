import { describe, expect, it } from 'vitest';

import { daysOpen, openSinceLabel } from './openSince';

describe('daysOpen', () => {
  it('counts zero on the day of the first observation, whatever the hour', () => {
    expect(daysOpen(new Date('2026-11-27T08:00:00Z'), new Date('2026-11-27T23:59:00Z'))).toBe(0);
  });

  it('counts calendar days, not periods of 24 hours', () => {
    expect(daysOpen(new Date('2026-11-26T23:00:00Z'), new Date('2026-11-27T01:00:00Z'))).toBe(1);
  });

  it('counts across months and years', () => {
    expect(daysOpen(new Date('2025-12-31T12:00:00Z'), new Date('2026-03-05T12:00:00Z'))).toBe(64);
  });

  it('refuses a first observation after today', () => {
    expect(() => daysOpen(new Date('2026-11-28T00:00:00Z'), new Date('2026-11-27T00:00:00Z'))).toThrow(RangeError);
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

  it.each([-1, 1.5, Number.NaN])('refuses %d', (days) => {
    expect(() => openSinceLabel(days)).toThrow(RangeError);
  });
});
