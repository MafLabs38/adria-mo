import { expect, test } from '@playwright/test';

test('@smoke the public page shows the constats of the building on a phone', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));

  await page.goto('/');

  await expect(page).toHaveTitle(/Constats de l'immeuble/);
  await expect(page.getByRole('heading', { level: 1, name: "Constats de l'immeuble" })).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: 'Constats en cours' })).toBeVisible();
  expect(errors).toEqual([]);
});
