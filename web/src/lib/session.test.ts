import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './api';
import { isStaleCode, openSession } from './session';

const code = 'Zm9vYmFyYmF6cXV4LXF1dXhfY29yZ2VncmF1bHQ';
const me = { userName: 'eric' };

function address(hash: string) {
  return { hash, replace: vi.fn() };
}

/** A back end that gives the same answer to every call, and records the calls. */
function api(answer: () => Promise<unknown>) {
  const get = vi.fn(answer);
  const post = vi.fn(answer);
  return {
    get: get as unknown as <T>(path: string) => Promise<T>,
    post: post as unknown as <T>(path: string, body?: unknown) => Promise<T>,
    calls: { get, post },
  };
}

const refused = (status: number) => () => Promise.reject(new ApiError(status, {}, `HTTP ${status}`));

describe('openSession', () => {
  it('pairs with the code of the address, and takes the code out of it first', async () => {
    const at = address(`#/appairer/${code}`);
    const backEnd = api(() => Promise.resolve(me));

    expect(await openSession(backEnd, at)).toEqual({ kind: 'in', me });
    expect(at.replace).toHaveBeenCalledWith('#/a-classer');
    expect(backEnd.calls.post).toHaveBeenCalledWith('/auth/pair', { code });
    expect(backEnd.calls.get).not.toHaveBeenCalled();
  });

  it('asks for the login when the code is refused', async () => {
    const session = await openSession(api(refused(401)), address(`#/appairer/${code}`));

    expect(session.kind).toBe('anonymous');
    expect(session.kind === 'anonymous' && session.pairingError && isStaleCode(session.pairingError)).toBe(true);
    expect(isStaleCode(new ApiError(400, {}, 'x'))).toBe(false);
  });

  it('opens with the session the browser has', async () => {
    expect(
      await openSession(
        api(() => Promise.resolve(me)),
        address('#/a-classer'),
      ),
    ).toEqual({ kind: 'in', me });
    expect(await openSession(api(refused(401)), address(''))).toEqual({ kind: 'anonymous' });
  });

  it.each([
    ['no network', () => Promise.reject(new TypeError('Failed to fetch'))],
    ['a stopped server behind its proxy', refused(502)],
    ['a tunnel without its server', refused(530)],
  ])('keeps the photos for later with %s, pairing or not', async (_, answer) => {
    expect(await openSession(api(answer), address(''))).toEqual({ kind: 'offline' });
    expect(await openSession(api(answer), address(`#/appairer/${code}`))).toEqual({ kind: 'offline' });
  });

  it('shows an error of the back end itself', async () => {
    const session = await openSession(api(refused(500)), address(''));
    expect(session.kind).toBe('error');
  });
});
