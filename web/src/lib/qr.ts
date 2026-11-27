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

/** The QR code of a text as the address of an SVG image (a `data:` URL, which the page's CSP allows for images). */
export function qrImage(text: string): string {
  const { size, path } = qrShape(text);
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" shape-rendering="crispEdges">` +
    `<rect width="${size}" height="${size}" fill="#fff"/><path d="${path}" fill="#000"/></svg>`;
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}
