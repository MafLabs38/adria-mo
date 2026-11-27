import { describe, expect, it, vi } from 'vitest';
import { Api, ApiError, APP_HEADER, apiUrl } from './api';

function json(status: number, body: unknown, type = 'application/json'): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': type } });
}

describe('apiUrl', () => {
  it.each([
    [undefined, '/api/v1'],
    ['  ', '/api/v1'],
    ['https://api.example.org/api/v1/', 'https://api.example.org/api/v1'],
  ])('setting %s gives %s', (configured, expected) => {
    expect(apiUrl(configured)).toBe(expected);
  });
});

describe('Api', () => {
  it('sends JSON with the application header and the session cookie', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(json(200, { ok: true }));
    const result = await new Api('/api/v1', fetcher).post<{ ok: boolean }>('/auth/login', { userName: 'eric' });

    expect(result).toEqual({ ok: true });
    const [url, init] = fetcher.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/auth/login');
    expect(init?.method).toBe('POST');
    expect(init?.credentials).toBe('same-origin');
    expect(init?.headers).toMatchObject({ [APP_HEADER]: '1', 'Content-Type': 'application/json' });
    expect(init?.body).toBe('{"userName":"eric"}');
  });

  it('sends a form as it is, letting the browser set its type', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(json(201, { id: 1 }));
    const form = new FormData();
    await new Api('/api/v1', fetcher).post('/constats/1/pieces', form);

    const init = fetcher.mock.calls[0]?.[1];
    expect(init?.body).toBe(form);
    expect(init?.headers).not.toHaveProperty('Content-Type');
  });

  it('reads nothing from a 204 and sends no body without one', async () => {
    const fetcher = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    const api = new Api('/api/v1', fetcher);

    await expect(api.post('/auth/logout')).resolves.toBeUndefined();
    expect(fetcher.mock.calls[0]?.[1]?.body).toBeUndefined();
  });

  it('gets and puts', async () => {
    const fetcher = vi.fn<typeof fetch>().mockImplementation(() => Promise.resolve(json(200, [])));
    const api = new Api('/api/v1', fetcher);

    await api.get('/constats');
    await api.put('/constats/2', { title: 'x' });

    expect(fetcher.mock.calls.map((c) => c[1]?.method)).toEqual(['GET', 'PUT']);
  });

  it('turns a validation problem into codes per field', async () => {
    const problem = {
      title: 'One or more validation errors occurred.',
      errors: { newPassword: ['TooShort', 'TooCommon'] },
    };
    const api = new Api(
      '/api/v1',
      vi.fn<typeof fetch>().mockResolvedValue(json(400, problem, 'application/problem+json')),
    );

    const error = await api.post('/auth/password', {}).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).codes).toEqual({ newPassword: ['TooShort', 'TooCommon'] });
    expect((error as ApiError).allCodes).toEqual(['TooShort', 'TooCommon']);
    expect((error as ApiError).name).toBe('ApiError');
  });

  it('keeps the detail of a problem and a code sent as a string', async () => {
    const detailed = new Api(
      '/api/v1',
      vi.fn<typeof fetch>().mockResolvedValue(json(401, { detail: 'Connexion refusée' })),
    );
    const conflict = new Api('/api/v1', vi.fn<typeof fetch>().mockResolvedValue(json(409, 'AlreadyAdded')));

    const refused = (await detailed.get('/auth/me').catch((e: unknown) => e)) as ApiError;
    const duplicate = (await conflict.post('/x').catch((e: unknown) => e)) as ApiError;

    expect(refused.message).toBe('Connexion refusée');
    expect(refused.allCodes).toEqual([]);
    expect(duplicate.codes).toEqual({ '': ['AlreadyAdded'] });
    expect(duplicate.message).toBe('HTTP 409');
  });

  it('survives an error without a JSON body', async () => {
    const api = new Api(
      '/api/v1',
      vi.fn<typeof fetch>().mockResolvedValue(new Response('Bad gateway', { status: 502 })),
    );

    const error = (await api.get('/constats').catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.message).toBe('HTTP 502');
  });

  it('builds the address of a photo', () => {
    expect(new Api('/api/v1').photoUrl('ab c.jpg')).toBe('/api/v1/photos/ab%20c.jpg');
  });

  it('uses the global fetch by default', async () => {
    const spy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(200, 1));
    await expect(new Api('/api/v1').get('/info')).resolves.toBe(1);
    spy.mockRestore();
  });
});
