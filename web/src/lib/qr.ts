import { encode } from 'uqr';

/** A QR code as one SVG path of unit squares, and its size in modules (quiet zone included). */
export interface QrShape {
  size: number;
  path: string;
}

/** The QR code of a text, drawn by the page itself: the address never goes to a service of QR codes. */
export function qrShape(text: string): QrShape {
  const { data, size } = encode(text, { ecc: 'M', border: 4 });
  const squares: string[] = [];
  data.forEach((row, y) =>
    row.forEach((dark, x) => {
      if (dark) {
        squares.push(`M${x} ${y}h1v1h-1z`);
      }
    }),
  );
  return { size, path: squares.join('') };
}
