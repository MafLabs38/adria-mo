import { expect, test } from '@playwright/test';
import { expectImagesLoaded, watchErrors } from './helpers';

// The promise of the public part (spec §10.1), on a sandbox whose back end was stopped after the @stack test: the constats and
// their photos stay online; only the administration is unavailable.
test('@offline with the back end stopped the public page still shows the constats and their photos', async ({
  page,
  request,
}) => {
  const errors = watchErrors(page);
  expect((await request.get('/api/v1/info')).ok()).toBe(false);

  await page.goto('/');
  await expect(page.getByRole('link', { name: /Porte du local poubelles forcée/ }).first()).toBeVisible();
  await expectImagesLoaded(page, 'li img');
  await page
    .getByRole('link', { name: /Porte du local poubelles forcée/ })
    .first()
    .click();
  await expectImagesLoaded(page, 'article img');

  await page.goto('/admin/');
  await expect(page.getByRole('alert')).toBeVisible();
  expect(errors).toEqual([]);
});
