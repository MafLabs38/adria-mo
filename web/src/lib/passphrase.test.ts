import { describe, expect, it } from 'vitest';
import { suggestPassphrase } from './passphrase';

describe('suggestPassphrase', () => {
  it('gives four groups of two syllables, nineteen characters', () => {
    const passphrase = suggestPassphrase();
    expect(passphrase).toMatch(/^([bdfgklmnprstvz][aeiou]){2}(-([bdfgklmnprstvz][aeiou]){2}){3}$/);
    expect(passphrase).toHaveLength(19);
  });

  it('takes every letter from the random source', () => {
    const fixed = (values: Uint32Array) => values.map((_, index) => index);
    expect(suggestPassphrase(fixed)).toBe('befo-kami-puse-voba');
  });

  it('differs from one call to the next', () => {
    expect(new Set(Array.from({ length: 20 }, () => suggestPassphrase())).size).toBe(20);
  });

  it('reads a short random source as zeros', () => {
    expect(suggestPassphrase(() => new Uint32Array(0))).toBe('baba-baba-baba-baba');
  });
});
