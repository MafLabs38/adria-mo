import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import { expectImagesLoaded, expectNoHorizontalScroll, watchErrors } from './helpers';

// The public page reads only the published snapshot: here a simulated one, so the test needs no back end at all.
const photo = readFileSync(new URL('fixtures/porte.jpg', import.meta.url));
const now = new Date();
const daysAgo = (days: number) => new Date(now.getTime() - days * 86_400_000).toISOString();

const snapshot = {
  version: 1,
  publishedAt: now.toISOString(),
  timeZone: 'Europe/Paris',
  constats: [
    {
      number: 12,
      title: 'Porte du hall forcée',
      categoryKey: 'portes',
      category: 'Portes et fermetures',
      location: 'Hall B',
      status: 'open',
      firstObservedAt: daysAgo(94),
      updatedAt: daysAgo(1),
      pieces: [
        {
          observedAt: daysAgo(94),
          text: 'Gond arraché',
          photo: { src: 'photos/a.jpg', thumbnail: 'photos/a-t.jpg', width: 1200, height: 900 },
        },
        { observedAt: daysAgo(1), text: 'Toujours cassée', photo: null },
      ],
    },
    {
      number: 7,
      title: 'Ampoule grillée au 3ᵉ',
      categoryKey: 'eclairage',
      category: 'Éclairage',
      location: null,
      status: 'resolved',
      firstObservedAt: daysAgo(0),
      updatedAt: daysAgo(0),
      pieces: [],
    },
  ],
};

test.beforeEach(async ({ page }) => {
  await page.route('**/data/constats.json', (route) => route.fulfill({ json: snapshot }));
  await page.route('**/data/photos/*', (route) => route.fulfill({ body: photo, contentType: 'image/jpeg' }));
});

test('@smoke the public page lists the constats of the building on a phone', async ({ page }) => {
  const errors = watchErrors(page);
  await page.goto('./');

  await expect(page).toHaveTitle(/Constats de l'immeuble/);
  await expect(page.getByRole('heading', { level: 1, name: "Constats de l'immeuble" })).toBeVisible();
  const cards = page.getByRole('main').getByRole('listitem');
  await expect(cards).toHaveCount(2);
  await expect(cards.first()).toContainText('Porte du hall forcée');
  await expect(cards.first()).toContainText('Portes et fermetures · Hall B');
  await expect(cards.first()).toContainText('Ouvert');
  await expect(cards.first()).toContainText('ouvert depuis 94 jours');
  await expect(cards.nth(1)).toContainText("ouvert aujourd'hui");
  await expect(page.getByText(/^À jour au /)).toBeVisible();
  await expectImagesLoaded(page, 'main li img');

  await page.getByLabel('État').selectOption({ label: 'Résolu (vérifié)' });
  await expect(cards).toHaveCount(1);
  await expect(page.getByText('1 constat', { exact: true })).toBeVisible();
  await page.getByLabel('État').selectOption({ label: 'Tous' });
  await page.getByLabel('Catégorie').selectOption({ label: 'Portes et fermetures' });
  await expect(cards).toHaveCount(1);
  await expect(cards.first()).toContainText('Porte du hall forcée');

  await expectNoHorizontalScroll(page);
  expect(errors).toEqual([]);
});

test('@smoke the page of a constat shows its pieces, oldest first', async ({ page }) => {
  const errors = watchErrors(page);
  await page.goto('./');
  await page.getByRole('link', { name: /Porte du hall forcée/ }).click();

  await expect(page).toHaveURL(/constat\/?\?n=12$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Porte du hall forcée' })).toBeVisible();
  await expect(page.getByText('Constat n° 12')).toBeVisible();
  const pieces = page.getByRole('main').getByRole('listitem');
  await expect(pieces).toHaveCount(2);
  await expect(pieces.first()).toContainText('Gond arraché');
  await expect(pieces.nth(1)).toContainText('Toujours cassée');
  await expectImagesLoaded(page, 'article img');
  await expectNoHorizontalScroll(page);

  await page.getByRole('link', { name: '← Tous les constats' }).click();
  await expect(page.getByRole('heading', { level: 1, name: "Constats de l'immeuble" })).toBeVisible();
  expect(errors).toEqual([]);
});

test('@smoke an unknown constat says it is not published', async ({ page }) => {
  await page.goto('./constat/?n=999');
  await expect(page.getByRole('alert')).toHaveText("Ce constat n'est pas (ou plus) publié.");
});

test('@smoke before any publication the page says there is nothing yet', async ({ page }) => {
  await page.route('**/data/constats.json', (route) => route.fulfill({ status: 404 }));
  await page.goto('./');
  await expect(page.getByText("Aucun constat publié pour l'instant.")).toBeVisible();
});

test('@smoke a broken snapshot shows a message, not a broken page', async ({ page }) => {
  await page.route('**/data/constats.json', (route) => route.fulfill({ json: { version: 99 } }));
  await page.goto('./');
  await expect(page.getByRole('alert')).toContainText('ne peuvent pas être affichés');
});
