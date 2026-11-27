/**
 * The address a phone opens by scanning the QR code (spec §4.1). The code travels in the fragment (after `#`): a browser never
 * sends the fragment to a server, so the code is in no log and no `Referer`; the page reads it and sends it once to the API.
 */

const CODE = /^#\/appairer\/([\w-]{20,64})$/;

/** The address to put in the QR code. */
export function pairingUrl(origin: string, base: string, code: string): string {
  const root = base.endsWith('/') ? base : `${base}/`;
  return `${origin}${root}admin/#/appairer/${code}`;
}

/** The code of a pairing address, or `null`. */
export function pairingCodeOf(hash: string): string | null {
  return CODE.exec(hash)?.[1] ?? null;
}
