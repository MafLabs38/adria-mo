import { useMemo } from 'react';
import { qrImage } from '../../lib/qr';

/** A QR code drawn in the page (no image service): black on white, with its quiet zone, large enough to scan from a screen. */
export default function QrCode({ text, label }: Readonly<{ text: string; label: string }>) {
  const src = useMemo(() => qrImage(text), [text]);
  return <img src={src} alt={label} className="mx-auto h-64 w-64 max-w-full [image-rendering:pixelated]" />;
}
