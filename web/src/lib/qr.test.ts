import { encode } from 'uqr';
import { describe, expect, it } from 'vitest';
import { qrShape } from './qr';

describe('qrShape', () => {
  it('draws one unit square per dark module, inside a quiet zone', () => {
    const text = 'https://adria.example/admin/#/appairer/Zm9vYmFyYmF6cXV4LXF1dXhfY29yZ2VncmF1bHQ';
    const { data } = encode(text, { ecc: 'M', border: 4 });

    const { size, path } = qrShape(text);

    expect(size).toBe(data.length);
    expect(path.match(/h1v1h-1z/g)).toHaveLength(data.flat().filter(Boolean).length);
    expect(path).not.toMatch(/M[0-3] /);
    expect(path.startsWith('M4 4h1v1h-1z')).toBe(true);
  });
});
