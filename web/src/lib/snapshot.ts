import { withSlash } from './paths';

/**
 * The public snapshot the back end publishes (spec §10.2, `constats.json`): the only data the public page reads. It is read in
 * the browser, so the page shows the last publication even while the back end is down.
 */

export interface PublicPhoto {
  src: string;
  thumbnail: string;
  width: number;
  height: number;
}

export interface PublicPiece {
  observedAt: string;
  text: string | null;
  photo: PublicPhoto | null;
}

export interface PublicConstat {
  number: number;
  title: string;
  category: string;
  location: string | null;
  status: string;
  firstObservedAt: string;
  updatedAt: string;
  pieces: PublicPiece[];
}

export interface Snapshot {
  version: number;
  publishedAt: string;
  timeZone: string;
  constats: PublicConstat[];
}

/** The format this page reads; a newer one is refused rather than shown wrong. */
export const SNAPSHOT_VERSION = 1;

/** The folder of the published data, from the build setting, else `data/` under the base path. */
export function dataUrl(base: string, configured: string | undefined): string {
  const url = configured?.trim() ? configured.trim() : `${withSlash(base)}data/`;
  return url.endsWith('/') ? url : `${url}/`;
}

/** The address of a published file (a photo), from a path relative to the snapshot. */
export function fileUrl(data: string, path: string): string {
  if (path.startsWith('/') || path.includes('..') || path.includes(':')) {
    throw new RangeError(`Not a relative path of the snapshot: ${path}`);
  }
  return `${data}${path}`;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Checks the shape of a parsed `constats.json`: the page shows nothing rather than a half-read snapshot. */
export function parseSnapshot(json: unknown): Snapshot {
  if (!isObject(json) || json['version'] !== SNAPSHOT_VERSION) {
    throw new TypeError('Unknown snapshot format.');
  }
  const { publishedAt, timeZone, constats } = json;
  if (typeof publishedAt !== 'string' || typeof timeZone !== 'string' || !Array.isArray(constats)) {
    throw new TypeError('Incomplete snapshot.');
  }
  for (const constat of constats) {
    if (
      !isObject(constat) ||
      typeof constat['number'] !== 'number' ||
      typeof constat['title'] !== 'string' ||
      typeof constat['firstObservedAt'] !== 'string' ||
      !Array.isArray(constat['pieces'])
    ) {
      throw new TypeError('Incomplete constat in the snapshot.');
    }
  }
  return json as unknown as Snapshot;
}

/** The first photo of a constat, for its card. */
export function coverPhoto(constat: PublicConstat): PublicPhoto | null {
  return constat.pieces.find((piece) => piece.photo !== null)?.photo ?? null;
}
