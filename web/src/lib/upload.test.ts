import { describe, expect, it, vi } from 'vitest';
import { uploadPieces } from './upload';

const photo = (name: string) => new File([new Uint8Array([1, 2, 3])], name, { type: 'image/jpeg' });

function recorder(failOn?: string) {
  const sent: { path: string; photo: string | null; text: string | null }[] = [];
  const post = vi.fn((path: string, body?: unknown) => {
    const form = body as FormData;
    const file = form.get('photo');
    const name = file instanceof File ? file.name : null;
    sent.push({ path, photo: name, text: (form.get('text') as string | null) ?? null });
    return name === failOn ? Promise.reject(new Error('refused')) : Promise.resolve(undefined);
  });
  return { api: { post } as never, sent };
}

describe('uploadPieces', () => {
  it('sends one request per photo, the comment with the first', async () => {
    const { api, sent } = recorder();
    const progress: string[] = [];

    const outcomes = await uploadPieces(api, 7, [photo('a.jpg'), photo('b.jpg')], '  Gond arraché ', (done, total) =>
      progress.push(`${done}/${total}`),
    );

    expect(sent).toEqual([
      { path: '/constats/7/pieces', photo: 'a.jpg', text: 'Gond arraché' },
      { path: '/constats/7/pieces', photo: 'b.jpg', text: null },
    ]);
    expect(outcomes).toEqual([
      { file: 'a.jpg', error: null },
      { file: 'b.jpg', error: null },
    ]);
    expect(progress).toEqual(['0/2', '1/2', '2/2']);
  });

  it('sends a comment alone when there is no photo', async () => {
    const { api, sent } = recorder();

    const outcomes = await uploadPieces(api, 7, [], 'Toujours cassée');

    expect(sent).toEqual([{ path: '/constats/7/pieces', photo: null, text: 'Toujours cassée' }]);
    expect(outcomes).toEqual([{ file: '', error: null }]);
  });

  it('sends nothing without photo nor comment', async () => {
    const { api, sent } = recorder();

    expect(await uploadPieces(api, 7, [], '   ')).toEqual([]);
    expect(sent).toEqual([]);
  });

  it('goes on after a refused photo and reports it', async () => {
    const { api } = recorder('a.jpg');

    const outcomes = await uploadPieces(api, 7, [photo('a.jpg'), photo('b.jpg')], '');

    expect(outcomes.map((o) => [o.file, o.error === null])).toEqual([
      ['a.jpg', false],
      ['b.jpg', true],
    ]);
  });
});
