import { describe, expect, it } from 'vitest';
import { suggestPassphrase } from './passphrase';

describe('suggestPassphrase', () => {
  it('gives four groups of two syllables, nineteen characters', () => {
    const passphrase = suggestPassphrase();
    expect(passphrase).toMatch(/^([bdfgjklmnprstvwz][aeio]){2}(-([bdfgjklmnprstvwz][aeio]){2}){3}$/);
    expect(passphrase).toHaveLength(19);
  });

  it('takes each letter from whole bits of the random bytes', () => {
    // 0x00 → "ba", 0x1f → "ze", 0x2a → "ri", 0x31 → "do".
    const fixed = () => new Uint8Array([0x00, 0x1f, 0x2a, 0x31, 0x00, 0x00, 0x00, 0x00]);
    expect(suggestPassphrase(fixed)).toBe('baze-rido-baba-baba');
  });

  it('reads a short random source as zeros', () => {
    expect(suggestPassphrase(() => new Uint8Array(0))).toBe('baba-baba-baba-baba');
  });
});
