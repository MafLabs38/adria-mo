/**
 * A temporary password the super-admin can read aloud (spec §2.1): four groups of syllables, such as "bavo-kimu-sela-ripo".
 * Nineteen characters from a random source: 70⁸ combinations, about 49 bits: enough for a password that must be changed at the first login.
 */
const CONSONANTS = 'bdfgklmnprstvz';
const VOWELS = 'aeiou';

export type RandomSource = (values: Uint32Array) => Uint32Array;

const cryptoSource: RandomSource = (values) => globalThis.crypto.getRandomValues(values);

export function suggestPassphrase(random: RandomSource = cryptoSource): string {
  const values = random(new Uint32Array(16));
  const groups: string[] = [];
  for (let group = 0; group < 4; group++) {
    let word = '';
    for (let syllable = 0; syllable < 2; syllable++) {
      const index = group * 4 + syllable * 2;
      word += CONSONANTS.charAt((values[index] ?? 0) % CONSONANTS.length);
      word += VOWELS.charAt((values[index + 1] ?? 0) % VOWELS.length);
    }
    groups.push(word);
  }
  return groups.join('-');
}
