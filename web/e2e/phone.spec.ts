import { devices, expect, test, type Browser, type Page } from '@playwright/test';
import { expectImagesLoaded, photoTakenAt, waitForServiceWorker, watchErrors } from './helpers';

// The phone of an administrator (spec §4.1), on a sandbox: paired by QR code without typing a password, photos shared from the
// gallery that wait in "À classer" in series, photos kept in the phone without network and sent when it comes back.
const superAdmin = process.env['E2E_SUPER_ADMIN'] ?? '';
const superAdminPassword = process.env['E2E_SUPER_ADMIN_PASSWORD'] ?? '';
const phoneName = `Téléphone ${Date.now()}`;

async function login(page: Page) {
  await page.goto('./admin/');
  await page.getByLabel('Identifiant').fill(superAdmin);
  await page.getByLabel('Mot de passe').fill(superAdminPassword);
  await page.getByRole('button', { name: 'Se connecter' }).click();
  await expect(page.getByRole('link', { name: 'À classer' })).toBeVisible();
}

/** What the gallery of Android does when photos are shared to the installed site: a form posted to the share target. */
async function shareFromGallery(page: Page, files: ReturnType<typeof photoTakenAt>[]) {
  await page.evaluate(() => {
    const form = document.createElement('form');
    form.method = 'post';
    form.enctype = 'multipart/form-data';
    form.action = './partager/';
    const input = document.createElement('input');
    input.type = 'file';
    input.name = 'photos';
    input.multiple = true;
    input.id = 'shared';
    form.append(input);
    document.body.append(form);
  });
  await page.setInputFiles('#shared', files);
  await page.evaluate(() => document.querySelector<HTMLFormElement>('form[action="./partager/"]')?.requestSubmit());
}

async function newPhone(browser: Browser, baseURL: string | undefined) {
  const context = await browser.newContext({ ...devices['Pixel 7'], ...(baseURL ? { baseURL } : {}) });
  return { context, page: await context.newPage() };
}

test('@stack a phone paired by QR code shares photos that are classified in series', async ({
  page,
  browser,
  baseURL,
}) => {
  test.setTimeout(120_000);
  const errors = watchErrors(page);

  // On the computer: a QR code for the phone.
  await login(page);
  await page.getByRole('link', { name: 'Mon compte' }).click();
  await page.getByLabel('Nom du téléphone').fill(phoneName);
  const created = page.waitForResponse((r) => r.url().endsWith('/api/v1/devices') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Ajouter un téléphone' }).click();
  const { code } = (await (await created).json()) as { code: string };
  await expect(page.getByRole('img', { name: "Code QR d'appairage du téléphone" })).toBeVisible();

  // The phone opens the address of the QR code: logged in, without a password, on the inbox.
  const phone = await newPhone(browser, baseURL);
  const phoneErrors = watchErrors(phone.page);
  await phone.page.goto(`./admin/#/appairer/${code}`);
  await expect(phone.page.getByRole('heading', { name: 'À classer' })).toBeVisible();
  await expect(phone.page).not.toHaveURL(new RegExp(code));
  await expect(page.getByText(`« ${phoneName} » est appairé`)).toBeVisible({ timeout: 10_000 });

  // Two photos taken two minutes apart, shared from the gallery: one series.
  await waitForServiceWorker(phone.page);
  await shareFromGallery(phone.page, [photoTakenAt(2), photoTakenAt(4)]);
  await expect(phone.page).toHaveURL(/\/admin\/#\/a-classer$/);
  // Nothing leaves before its owner has seen the photos: a page of another site can post to the share target too.
  await expect(phone.page.getByText('2 photos partagées depuis la galerie')).toBeVisible();
  await expectImagesLoaded(phone.page, 'img[alt^="IMG_09"]');
  await phone.page.getByRole('button', { name: 'Envoyer ces 2 photos' }).click();
  const series = phone.page.getByRole('region', { name: '23 novembre 2026, de 09:17 à 09:19' });
  await expect(series).toBeVisible({ timeout: 20_000 });
  await expectImagesLoaded(phone.page, 'img[alt^="Prise à"]');
  await expect(phone.page.getByText('Aucune photo en attente dans ce téléphone.')).toBeVisible();

  // The series becomes a constat, dated by its first photo.
  await series.getByLabel('Rattacher à').selectOption({ label: 'Un nouveau constat' });
  await series.getByLabel('Titre').fill('Tags dans la cage d’escalier');
  await series.getByLabel('Catégorie').selectOption({ label: 'Dégradations' });
  await series.getByRole('button', { name: 'Rattacher 2 photos' }).click();
  await expect(phone.page.getByText(/^2 photos rattachées au constat n° \d+\./)).toBeVisible();
  await phone.page.getByRole('link', { name: 'Voir le constat' }).click();
  await expect(phone.page.getByRole('heading', { level: 1, name: 'Tags dans la cage d’escalier' })).toBeVisible();
  await expect(phone.page.getByText('Photo prise le 23 novembre 2026 à 09:17')).toBeVisible();
  await expect(phone.page.getByText('Photo prise le 23 novembre 2026 à 09:19')).toBeVisible();

  // Removed from the computer, the phone is logged out.
  await page.reload();
  page.once('dialog', (dialog) => void dialog.accept());
  await page.getByRole('listitem').filter({ hasText: phoneName }).getByRole('button', { name: 'Retirer' }).click();
  await expect(page.getByRole('listitem').filter({ hasText: phoneName })).toHaveCount(0);
  await phone.page.reload();
  await expect(phone.page.getByRole('heading', { name: 'Connexion' })).toBeVisible();

  expect(errors).toEqual([]);
  expect(phoneErrors).toEqual([]);
  await phone.context.close();
});

test('@stack without network the administration opens, keeps the photos taken and sends them when the network comes back', async ({
  page,
  context,
}) => {
  test.setTimeout(90_000);
  const errors = watchErrors(page);
  // A minute of its own (09:35 to 09:59), so a photo left by an earlier run on the same sandbox is in another series.
  const minute = 20 + (Date.now() % 25);
  await login(page);
  await page.getByRole('link', { name: 'À classer' }).click();
  await waitForServiceWorker(page);
  // Once more online, so the page and its scripts are kept by the service worker.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'À classer' })).toBeVisible();

  // On a landing without wifi: the administration opens from the phone, the photo taken waits in it.
  await context.setOffline(true);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Administration injoignable' })).toBeVisible();
  await page.getByLabel('Depuis la galerie').setInputFiles(photoTakenAt(minute));
  await expect(page.getByText(/^1 photo attend dans ce téléphone : pas de réseau, ou serveur arrêté/)).toBeVisible();

  // Back home: the network comes back, the photo leaves by itself and waits in "À classer".
  await context.setOffline(false);
  await page.evaluate(() => globalThis.dispatchEvent(new Event('online')));
  await expect(page.getByText('Aucune photo en attente dans ce téléphone.')).toBeVisible({ timeout: 15_000 });
  const series = page.getByRole('region', { name: `23 novembre 2026 à 09:${15 + minute}` });
  await expect(series).toBeVisible();

  // Left as found: the photo is discarded.
  page.once('dialog', (dialog) => void dialog.accept());
  await series.getByRole('button', { name: 'Écarter' }).click();
  await expect(series).toHaveCount(0);
  expect(errors).toEqual([]);
});
