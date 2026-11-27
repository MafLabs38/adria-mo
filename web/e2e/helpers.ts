import { readFileSync } from 'node:fs';
import { expect, type Page } from '@playwright/test';

/** Collects the errors of the page, to check none happened. */
export function watchErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  return errors;
}

/** Mobile first: nothing wider than the phone's screen. */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - globalThis.innerWidth);
  expect(overflow).toBeLessThanOrEqual(0);
}

/** Whether every image of the page that should be shown was really loaded. */
export async function expectImagesLoaded(page: Page, selector: string): Promise<void> {
  const images = page.locator(selector);
  await expect(images.first()).toBeVisible();
  const broken = await images.evaluateAll(
    (list) =>
      list.filter((img) => !(img as HTMLImageElement).complete || (img as HTMLImageElement).naturalWidth === 0).length,
  );
  expect(broken).toBe(0);
}

/** Makes the photos of one run differ from those of an earlier run on the same sandbox (the back end refuses a photo twice). */
const run = Buffer.from(`run ${Date.now()}`, 'ascii');

/**
 * The invented photo of the fixtures, taken `minutes` after its own date (23 November 2026 09:15): another file (another
 * SHA-256), dated by its EXIF block, to build series of photos without adding real ones to the repository. A few bytes after
 * the end of the image, which decoders ignore, make it unique to this run.
 */
export function photoTakenAt(minutes: number): { name: string; mimeType: string; buffer: Buffer } {
  const original = readFileSync(new URL('fixtures/porte.jpg', import.meta.url));
  const from = Buffer.from('2026:11:23 09:15:00', 'ascii');
  const to = Buffer.from(`2026:11:23 09:${String(15 + minutes).padStart(2, '0')}:00`, 'ascii');
  const at = original.indexOf(from);
  if (at < 0 || to.length !== from.length) {
    throw new Error('The fixture has no date to change.');
  }
  const buffer = Buffer.concat([original, run]);
  to.copy(buffer, at);
  return { name: `IMG_09${15 + minutes}.jpg`, mimeType: 'image/jpeg', buffer };
}

/** Waits until the service worker of the administration controls the page (it claims it once installed). */
export async function waitForServiceWorker(page: Page): Promise<void> {
  await page.waitForFunction(() => navigator.serviceWorker.controller !== null, undefined, { timeout: 15_000 });
}
