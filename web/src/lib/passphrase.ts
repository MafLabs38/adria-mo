/**
 * A temporary password the super-admin can read aloud (spec §2.1): four groups of syllables, such as "bavo-kimu-sela-ripo".
 * Sixteen consonants and four vowels, powers of two, so each letter takes whole bits of a random byte with no bias: eight
 * syllables of 6 bits, 48 bits, enough for a password that must be changed at the first login.
 */
const CONSONANTS = 'bdfgjklmnprstvwz';
const VOWELS = 'aeio';

export type RandomSource = (values: Uint8Array) => Uint8Array;

const cryptoSource: RandomSource = (values) => globalThis.crypto.getRandomValues(values);

export function suggestPassphrase(random: RandomSource = cryptoSource): string {
  const values = random(new Uint8Array(8));
  const groups: string[] = [];
  for (let group = 0; group < 4; group++) {
    let word = '';
    for (let syllable = 0; syllable < 2; syllable++) {
      const value = values[group * 2 + syllable] ?? 0;
      // Low 4 bits for the consonant, the next 2 for the vowel.
      word += CONSONANTS.charAt(value & 0b1111) + VOWELS.charAt((value >> 4) & 0b11);
    }
    groups.push(word);
  }
  return groups.join('-');
}
