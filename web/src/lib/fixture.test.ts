import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// The photo of the end-to-end tests is invented (CLAUDE.md, rule 1) and must carry what the back end has to read or remove: an
// EXIF date and a GPS position (a made-up one). Otherwise the end-to-end check "the published copy has no metadata" proves nothing.
const photo = readFileSync(new URL('../../e2e/fixtures/porte.jpg', import.meta.url));

describe('the photo of the end-to-end tests', () => {
  it('is a JPEG with an EXIF segment', () => {
    expect(photo.subarray(0, 2)).toEqual(Buffer.from([0xff, 0xd8]));
    expect(photo.includes(Buffer.from('Exif\0\0'))).toBe(true);
  });

  it('carries the date the tests expect', () => {
    expect(photo.includes(Buffer.from('2026:11:23 09:15:00'))).toBe(true);
  });

  it('carries a GPS block', () => {
    // Tag 0x8825 (GPSInfo), in either byte order.
    expect(photo.includes(Buffer.from([0x88, 0x25])) || photo.includes(Buffer.from([0x25, 0x88]))).toBe(true);
  });
});
