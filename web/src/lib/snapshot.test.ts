import { describe, expect, it } from 'vitest';
import { coverPhoto, dataUrl, fileUrl, parseSnapshot, type PublicConstat } from './snapshot';

const constat: PublicConstat = {
  number: 3,
  title: 'Porte du hall forcée',
  categoryKey: 'portes',
  category: 'Portes et fermetures',
  location: null,
  status: 'open',
  firstObservedAt: '2026-11-23T09:15:00+01:00',
  updatedAt: '2026-11-27T14:05:00+01:00',
  pieces: [
    { observedAt: '2026-11-23T09:15:00+01:00', text: 'Gond arraché', photo: null },
    {
      observedAt: '2026-11-24T09:15:00+01:00',
      text: null,
      photo: { src: 'photos/a.jpg', thumbnail: 'photos/a-t.jpg', width: 4, height: 3 },
    },
  ],
};

const snapshot = {
  version: 1,
  publishedAt: '2026-11-27T14:05:00+01:00',
  timeZone: 'Europe/Paris',
  constats: [constat],
};

describe('dataUrl', () => {
  it.each([
    ['/', undefined, '/data/'],
    ['/constats', undefined, '/constats/data/'],
    ['/constats/', '  ', '/constats/data/'],
    ['/', 'https://cdn.example.org/adria', 'https://cdn.example.org/adria/'],
    ['/', ' https://cdn.example.org/adria/ ', 'https://cdn.example.org/adria/'],
  ])('base %s and setting %s give %s', (base, configured, expected) => {
    expect(dataUrl(base, configured)).toBe(expected);
  });
});

describe('fileUrl', () => {
  it('joins a relative path to the data folder', () => {
    expect(fileUrl('/data/', 'photos/a.jpg')).toBe('/data/photos/a.jpg');
  });

  it.each(['/etc/passwd', '../secret.jpg', 'photos/../../x', 'javascript:alert(1)', 'https://evil.example/x.jpg'])(
    'refuses %s, which is not a path of the snapshot',
    (path) => {
      expect(() => fileUrl('/data/', path)).toThrow(RangeError);
    },
  );
});

describe('parseSnapshot', () => {
  it('accepts a snapshot of the known format', () => {
    expect(parseSnapshot(structuredClone(snapshot))).toEqual(snapshot);
  });

  it('accepts a snapshot without constats', () => {
    expect(parseSnapshot({ ...snapshot, constats: [] }).constats).toEqual([]);
  });

  it.each([
    ['nothing', null],
    ['an array', []],
    ['another version', { ...snapshot, version: 2 }],
    ['no date', { ...snapshot, publishedAt: undefined }],
    ['no time zone', { ...snapshot, timeZone: 3 }],
    ['no list', { ...snapshot, constats: {} }],
    ['a constat that is not an object', { ...snapshot, constats: ['x'] }],
    ['a constat without number', { ...snapshot, constats: [{ ...constat, number: '3' }] }],
    ['a constat without title', { ...snapshot, constats: [{ ...constat, title: null }] }],
    ['a constat without first observation', { ...snapshot, constats: [{ ...constat, firstObservedAt: 1 }] }],
    ['a constat without pieces', { ...snapshot, constats: [{ ...constat, pieces: null }] }],
  ])('refuses %s', (_, value) => {
    expect(() => parseSnapshot(value)).toThrow(TypeError);
  });
});

describe('coverPhoto', () => {
  it('is the first photo of the constat', () => {
    expect(coverPhoto(constat)?.src).toBe('photos/a.jpg');
  });

  it('is nothing for a constat without photo', () => {
    expect(coverPhoto({ ...constat, pieces: [] })).toBeNull();
  });
});
