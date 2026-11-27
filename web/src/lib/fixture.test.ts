import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// The photo of the end-to-end tests is invented (CLAUDE.md, rule 1) and must carry what the back end has to read or remove: an
// EXIF date and a GPS position (a made-up one).
const photo = readFileSync(new URL('../../e2e/fixtures/porte.jpg', import.meta.url));

/** The TIFF data of the EXIF segment (APP1 "Exif"), the only place EXIF tags live in a JPEG. */
function exifSegment(jpeg: Buffer): Buffer {
  let offset = 2;
  while (offset + 4 <= jpeg.length && jpeg[offset] === 0xff) {
    const marker = jpeg[offset + 1];
    const length = jpeg.readUInt16BE(offset + 2);
    const start = offset + 4;
    if (marker === 0xe1 && jpeg.subarray(start, start + 6).equals(Buffer.from('Exif\0\0'))) {
      return jpeg.subarray(start + 6, offset + 2 + length);
    }
    offset += 2 + length;
  }
  throw new Error('No EXIF segment.');
}

/** The tags of one IFD of a TIFF block. */
function tags(tiff: Buffer, ifdOffset: number): Map<number, number> {
  const little = tiff.toString('latin1', 0, 2) === 'II';
  const u16 = (at: number) => (little ? tiff.readUInt16LE(at) : tiff.readUInt16BE(at));
  const u32 = (at: number) => (little ? tiff.readUInt32LE(at) : tiff.readUInt32BE(at));
  const entries = new Map<number, number>();
  for (let i = 0; i < u16(ifdOffset); i++) {
    const entry = ifdOffset + 2 + i * 12;
    entries.set(u16(entry), u32(entry + 8));
  }
  return entries;
}

describe('the photo of the end-to-end tests', () => {
  const tiff = exifSegment(photo);
  const little = tiff.toString('latin1', 0, 2) === 'II';
  const ifd0 = tags(tiff, little ? tiff.readUInt32LE(4) : tiff.readUInt32BE(4));

  it('carries the date the tests expect in its EXIF block', () => {
    const exifIfd = ifd0.get(0x8769);
    expect(exifIfd).toBeDefined();
    expect(tags(tiff, exifIfd ?? 0).has(0x9003)).toBe(true);
    expect(tiff.includes(Buffer.from('2026:11:23 09:15:00'))).toBe(true);
  });

  it('carries a GPS position, latitude and longitude', () => {
    const gpsIfd = ifd0.get(0x8825);
    expect(gpsIfd).toBeDefined();
    const gps = tags(tiff, gpsIfd ?? 0);
    expect(gps.has(0x0002) && gps.has(0x0004)).toBe(true);
  });
});
