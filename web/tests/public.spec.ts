import { expect, test } from '@playwright/test';

/**
 * Recorrido público. Estos tests no necesitan datos sembrados: comprueban el chrome, la
 * navegación y las reglas que no dependen del catálogo.
 */

test('la portada carga con su titular y el CTA a planes', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toContainText('builders senior');
  await expect(page.getByRole('link', { name: 'Ver planes' })).toBeVisible();
});

test('el banner de cookies aparece y "solo las necesarias" lo cierra', async ({ page }) => {
  await page.goto('/');

  const banner = page.getByRole('dialog', { name: 'Consentimiento de cookies' });
  await expect(banner).toBeVisible();

  await banner.getByRole('button', { name: 'Solo las necesarias' }).click();
  await expect(banner).toBeHidden();

  // La decisión persiste: al recargar no vuelve a preguntar.
  await page.reload();
  await expect(page.getByRole('dialog', { name: 'Consentimiento de cookies' })).toBeHidden();
});

test('no se carga analítica antes de aceptarla', async ({ page }) => {
  const analyticsRequests: string[] = [];

  page.on('request', (request) => {
    if (request.url().includes('plausible') || request.url().includes('/js/script.js')) {
      analyticsRequests.push(request.url());
    }
  });

  await page.goto('/');
  await page.waitForLoadState('networkidle');

  expect(analyticsRequests).toEqual([]);
});

test('el enlace de salto lleva al contenido principal', async ({ page }) => {
  await page.goto('/');
  await page.keyboard.press('Tab');

  const skip = page.getByRole('link', { name: 'Saltar al contenido' });
  await expect(skip).toBeFocused();
});

test('una ruta inexistente muestra la página 404, no una pantalla en blanco', async ({ page }) => {
  await page.goto('/esta-ruta-no-existe');

  await expect(page.getByRole('heading', { name: 'Esta página no existe' })).toBeVisible();
});

test('las páginas legales están publicadas y enlazadas entre sí', async ({ page }) => {
  await page.goto('/legal/privacidad');
  await expect(page.getByRole('heading', { name: 'Política de privacidad' })).toBeVisible();

  await page.getByRole('link', { name: 'Política de cookies' }).first().click();
  await expect(page.getByRole('heading', { name: 'Política de cookies' })).toBeVisible();

  // La tabla de cookies debe nombrar las dos que realmente se usan.
  await expect(page.getByText('ink_rt')).toBeVisible();
  await expect(page.getByText('ink_vid')).toBeVisible();
});

test('la verificación de certificados rechaza un código inventado', async ({ page }) => {
  await page.goto('/check-certificate');

  await page.getByLabel('Código').fill('INK-ZZZZ-ZZZZ');
  await page.getByRole('button', { name: 'Verificar' }).click();

  await expect(page.getByText('No válido')).toBeVisible();
});

test('el menú móvil se abre y se cierra al navegar', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'El menú hamburguesa solo existe por debajo de 900 px.');

  await page.goto('/');

  await page.getByRole('button', { name: 'Abrir menú' }).click();
  const nav = page.getByRole('navigation', { name: 'Navegación principal' });
  await expect(nav).toBeVisible();

  await nav.getByRole('link', { name: 'Cursos' }).click();
  await expect(page).toHaveURL(/\/cursos$/);
  await expect(nav).toBeHidden();
});
