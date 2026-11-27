import { describe, expect, it } from 'vitest';
import { withSlash } from './paths';

describe('withSlash', () => {
  it.each([
    ['/', '/'],
    ['/constats', '/constats/'],
    ['/constats/', '/constats/'],
  ])('%s gives %s', (base, expected) => {
    expect(withSlash(base)).toBe(expected);
  });
});
