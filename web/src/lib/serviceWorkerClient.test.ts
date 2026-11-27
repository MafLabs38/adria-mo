import { describe, expect, it } from 'vitest';
import { adminScope, loadedAssets, serviceWorkerUrl } from './serviceWorkerClient';

describe('serviceWorkerClient', () => {
  it('places the service worker in the administration, under the base path, with the API path', () => {
    expect(adminScope('/')).toBe('/admin/');
    expect(adminScope('/constats')).toBe('/constats/admin/');
    expect(serviceWorkerUrl('/constats/', '/api/v1')).toBe('/constats/admin/sw.js?api=%2Fapi%2Fv1');
  });

  it('gives the service worker the scripts of this site only', () => {
    const entries = [
      { name: 'https://adria.example/_astro/AdminApp.js' },
      { name: 'https://adria.example/api/v1/auth/me' },
      { name: 'https://adria.example.evil/_astro/x.js' },
    ];
    expect(loadedAssets(entries, 'https://adria.example')).toEqual(['https://adria.example/_astro/AdminApp.js']);
  });
});
