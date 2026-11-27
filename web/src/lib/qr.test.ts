import { encode } from 'uqr';
import { describe, expect, it } from 'vitest';
import { qrImage, qrShape } from './qr';

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

  it('makes the smallest code for a short text: 21 modules and the quiet zone', () => {
    expect(qrShape('Adria').size).toBe(21 + 8);
  });

  it('gives an SVG image of the code, white behind', () => {
    const text = 'https://adria.example/admin/#/appairer/code';
    const src = qrImage(text);

    expect(src.startsWith('data:image/svg+xml,')).toBe(true);
    const svg = decodeURIComponent(src.slice('data:image/svg+xml,'.length));
    expect(svg).toContain(`viewBox="0 0 ${qrShape(text).size} ${qrShape(text).size}"`);
    expect(svg).toContain('fill="#fff"');
    expect(svg).toContain(`d="${qrShape(text).path}"`);
  });
});
