/** The administration's calls to the back end (never made by the public pages, spec §10.2). */

/** The header the back end requires on every request that changes something (spec §2.1). */
export const APP_HEADER = 'X-Adria-Mo';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    /** The error codes per field of a validation problem, or a single code under `''`. */
    readonly codes: Record<string, string[]>,
    detail: string,
  ) {
    super(detail);
    this.name = 'ApiError';
  }

  /** Every code, whatever the field. */
  get allCodes(): string[] {
    return Object.values(this.codes).flat();
  }
}

export function apiUrl(configured: string | undefined): string {
  let url = configured?.trim() ? configured.trim() : '/api/v1';
  while (url.endsWith('/')) {
    url = url.slice(0, -1);
  }
  return url;
}

type Fetch = typeof globalThis.fetch;

async function errorOf(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    // Not JSON: the status says enough.
  }
  const problem = (typeof body === 'object' && body !== null ? body : {}) as Record<string, unknown>;
  const errors = problem['errors'];
  let codes: Record<string, string[]> = {};
  if (typeof errors === 'object' && errors !== null) {
    codes = errors as Record<string, string[]>;
  } else if (typeof body === 'string') {
    codes = { '': [body] };
  }
  const detail = typeof problem['detail'] === 'string' ? problem['detail'] : `HTTP ${response.status}`;
  return new ApiError(response.status, codes, detail);
}

export class Api {
  constructor(
    readonly base: string,
    private readonly fetcher: Fetch = (input, init) => globalThis.fetch(input, init),
  ) {}

  async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const headers: Record<string, string> = { [APP_HEADER]: '1', Accept: 'application/json' };
    let payload: BodyInit | undefined;
    if (body instanceof FormData) {
      payload = body;
    } else if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
      payload = JSON.stringify(body);
    }
    const init: RequestInit = { method, headers, credentials: 'same-origin' };
    if (payload !== undefined) {
      init.body = payload;
    }
    const response = await this.fetcher(`${this.base}${path}`, init);
    if (!response.ok) {
      throw await errorOf(response);
    }
    if (response.status === 204) {
      return undefined as T;
    }
    return (await response.json()) as T;
  }

  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  delete<T>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }

  photoUrl(name: string): string {
    return `${this.base}/photos/${encodeURIComponent(name)}`;
  }
}
