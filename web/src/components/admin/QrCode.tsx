import { useMemo } from 'react';
import { qrShape } from '../../lib/qr';

/** A QR code drawn in the page (no image service): black on white, with its quiet zone, large enough to scan from a screen. */
export default function QrCode({ text, label }: Readonly<{ text: string; label: string }>) {
  const { size, path } = useMemo(() => qrShape(text), [text]);
  return (
    <svg
      role="img"
      aria-label={label}
      viewBox={`0 0 ${size} ${size}`}
      shapeRendering="crispEdges"
      className="mx-auto h-64 w-64 max-w-full"
    >
      <rect width={size} height={size} fill="#fff" />
      <path d={path} fill="#000" />
    </svg>
  );
}
