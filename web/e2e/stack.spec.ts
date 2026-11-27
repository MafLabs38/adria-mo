import { expect, test, type Page } from '@playwright/test';
import { expectImagesLoaded, expectNoHorizontalScroll, watchErrors } from './helpers';

// The whole flow on a sandbox (deploy/sandbox), on a phone: the super-admin creates an administrator, who chooses his own
// password, then reports a defect with a photo; the constat reaches the public page through the published snapshot.
const superAdmin = process.env['E2E_SUPER_ADMIN'] ?? '';
const superAdminPassword = process.env['E2E_SUPER_ADMIN_PASSWORD'] ?? '';
const admin = `voisin${Date.now()}`;
const adminPassword = 'le chat dort sur le radiateur';

async function login(page: Page, userName: string, password: string) {
  await page.goto('./admin/');
  await page.getByLabel('Identifiant').fill(userName);
  await page.getByLabel('Mot de passe').fill(password);
  await page.getByRole('button', { name: 'Se connecter' }).click();
}

test('@stack from a new account to a constat on the public page', async ({ page }) => {
  test.setTimeout(90_000);
  const errors = watchErrors(page);

  // The super-admin creates the account and reads the temporary password to hand over.
  await login(page, superAdmin, superAdminPassword);
  await page.getByRole('link', { name: 'Comptes' }).click();
  await page.getByLabel('Identifiant').fill(admin);
  const temporary = await page.getByLabel('Mot de passe provisoire').inputValue();
  expect(temporary).toMatch(/^[a-z]{4}(-[a-z]{4}){3}$/);
  await page.getByRole('button', { name: 'Créer le compte' }).click();
  await expect(page.getByRole('status')).toContainText(temporary);
  await page.getByRole('button', { name: /Se déconnecter/ }).click();
  await expect(page.getByRole('heading', { name: 'Connexion' })).toBeVisible();

  // The administrator must choose his password first; a weak one is refused with a French reason.
  await login(page, admin, temporary);
  await expect(page.getByRole('heading', { name: 'Choisissez votre mot de passe' })).toBeVisible();
  await page.getByLabel('Mot de passe provisoire').fill(temporary);
  await page.getByLabel('Nouveau mot de passe', { exact: true }).fill('motdepasse123');
  await page.getByLabel('Nouveau mot de passe, une seconde fois').fill('motdepasse123');
  await page.getByRole('button', { name: 'Enregistrer le mot de passe' }).click();
  await expect(page.getByRole('alert')).toContainText('trop courant');
  await page.getByLabel('Nouveau mot de passe', { exact: true }).fill(adminPassword);
  await page.getByLabel('Nouveau mot de passe, une seconde fois').fill(adminPassword);
  await page.getByRole('button', { name: 'Enregistrer le mot de passe' }).click();

  // He reports a defect with a photo taken three days before (its EXIF date).
  await page.getByRole('link', { name: '+ Constater' }).click();
  await page.getByLabel('Depuis la galerie').setInputFiles('e2e/fixtures/porte.jpg');
  await page.getByLabel('Titre').fill('Porte du local poubelles forcée');
  await page.getByLabel('Catégorie').selectOption({ label: 'Portes et fermetures' });
  await page.getByLabel('Emplacement').fill('Local poubelles');
  await page.getByLabel('Commentaire').fill('Serrure arrachée');
  await page.getByRole('button', { name: 'Enregistrer le constat' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Porte du local poubelles forcée' })).toBeVisible();
  await expect(page.getByText('Photo prise le 23 novembre 2026 à 09:15')).toBeVisible();
  await expect(page.getByText(/^SHA-256 [0-9a-f]{64}$/)).toBeVisible();
  await expectImagesLoaded(page, 'img[alt="Pièce du constat"]');
  await expectNoHorizontalScroll(page);

  // The publication gathers changes a few seconds, then the public page shows the constat and its photo, without GPS.
  await expect(async () => {
    await page.goto('./');
    await expect(page.getByRole('link', { name: /Porte du local poubelles forcée/ })).toBeVisible({ timeout: 1000 });
  }).toPass({ timeout: 30_000 });
  await page.getByRole('link', { name: /Porte du local poubelles forcée/ }).click();
  await expect(page.getByText('Serrure arrachée')).toBeVisible();
  await expect(page.getByText(/constaté le 23 novembre 2026/)).toBeVisible();
  await expectImagesLoaded(page, 'article img');
  const published = await page.locator('article img').first().getAttribute('src');
  const bytes = await (await page.request.get(published ?? '')).body();
  expect(bytes.includes(Buffer.from('Exif'))).toBe(false);
  expect(errors).toEqual([]);
});
