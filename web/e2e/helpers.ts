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
