/** A base path with exactly one trailing slash: "/" stays "/", "/constats" becomes "/constats/". */
export function withSlash(base: string): string {
  return base.endsWith('/') ? base : `${base}/`;
}
