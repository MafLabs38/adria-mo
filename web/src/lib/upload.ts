import type { Api } from './api';

export interface UploadOutcome {
  /** The name of the file, or `''` for a comment alone. */
  file: string;
  error: unknown;
}

/**
 * Adds pieces to a constat, one request per photo so a large batch goes through a slow wifi one photo at a time. The comment goes
 * with the first photo, or alone when there is no photo. Every outcome is returned: one refused photo does not stop the others.
 */
export async function uploadPieces(
  api: Pick<Api, 'post'>,
  constat: number,
  files: readonly File[],
  comment: string,
  onProgress: (done: number, total: number) => void = () => undefined,
): Promise<UploadOutcome[]> {
  const text = comment.trim();
  const outcomes: UploadOutcome[] = [];
  if (files.length === 0) {
    if (text) {
      outcomes.push(await send(api, constat, null, text));
    }
    return outcomes;
  }
  for (const [index, file] of files.entries()) {
    onProgress(index, files.length);
    outcomes.push(await send(api, constat, file, index === 0 ? text : ''));
  }
  onProgress(files.length, files.length);
  return outcomes;
}

async function send(api: Pick<Api, 'post'>, constat: number, file: File | null, text: string): Promise<UploadOutcome> {
  const form = new FormData();
  if (file) {
    form.append('photo', file, file.name);
  }
  if (text) {
    form.append('text', text);
  }
  try {
    await api.post(`/constats/${constat}/pieces`, form);
    return { file: file?.name ?? '', error: null };
  } catch (error) {
    return { file: file?.name ?? '', error };
  }
}
