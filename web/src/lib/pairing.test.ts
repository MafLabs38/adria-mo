import { describe, expect, it } from 'vitest';
import { pairingCodeOf, pairingUrl } from './pairing';

const code = 'Zm9vYmFyYmF6cXV4LXF1dXhfY29yZ2VncmF1bHQ';

describe('pairing', () => {
  it('puts the code in the fragment of the administration, under the base path', () => {
    expect(pairingUrl('https://adria.example', '/constats/', code)).toBe(
      `https://adria.example/constats/admin/#/appairer/${code}`,
    );
    expect(pairingUrl('https://adria.example', '/constats', code)).toBe(
      `https://adria.example/constats/admin/#/appairer/${code}`,
    );
  });

  it('reads the code back from the fragment only', () => {
    expect(pairingCodeOf(`#/appairer/${code}`)).toBe(code);
    expect(pairingCodeOf('#/a-classer')).toBeNull();
    expect(pairingCodeOf('#/appairer/court')).toBeNull();
    expect(pairingCodeOf(`#/appairer/${code}/../comptes`)).toBeNull();
  });
});
