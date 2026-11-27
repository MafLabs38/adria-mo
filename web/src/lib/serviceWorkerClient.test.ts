import { describe, expect, it } from 'vitest';
import { adminScope, isSameOriginPath, serviceWorkerUrl } from './serviceWorkerClient';

describe('serviceWorkerClient', () => {
  it('places the service worker in the administration, under the base path, with the API path', () => {
    expect(adminScope('/')).toBe('/admin/');
    expect(adminScope('/constats')).toBe('/constats/admin/');
    expect(serviceWorkerUrl('/constats/', '/api/v1')).toBe('/constats/admin/sw.js?api=%2Fapi%2Fv1');
  });

  it('registers the service worker only when the API is a path of this site', () => {
    expect(isSameOriginPath('/api/v1')).toBe(true);
    expect(isSameOriginPath('https://api.example/api/v1')).toBe(false);
    expect(isSameOriginPath('//api.example/api/v1')).toBe(false);
  });
});
