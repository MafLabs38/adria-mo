import { expect, test } from '@playwright/test';
import { expectImagesLoaded, watchErrors } from './helpers';

// The promise of the public part (spec §10.1). Runs after @stack, on the same sandbox, once its back end is stopped: the constat
// @stack published, and its photos, stay online; only the administration is unavailable.
test('@offline (after the stack test) with the back end stopped the public page still shows the constats and their photos', async ({
  page,
  request,
}) => {
  const errors = watchErrors(page);
  expect((await request.get('./api/v1/info')).ok()).toBe(false);

  await page.goto('./');
  await expect(
    page.getByRole('link', { name: /Porte du local poubelles forcée/ }).first(),
    'the constat the @stack test publishes (run it first, on this sandbox)',
  ).toBeVisible();
  await expectImagesLoaded(page, 'main li img');
  await page
    .getByRole('link', { name: /Porte du local poubelles forcée/ })
    .first()
    .click();
  await expectImagesLoaded(page, 'article img');

  await page.goto('./admin/');
  await expect(page.getByRole('alert')).toBeVisible();
  expect(errors).toEqual([]);
});
