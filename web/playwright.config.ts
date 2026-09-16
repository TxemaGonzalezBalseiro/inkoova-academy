import { defineConfig, devices } from '@playwright/test';

/**
 * E2E contra el compose local. La API debe estar levantada:
 *
 *   docker compose -f ../infra/docker-compose.yml up -d
 *   npm run test:e2e
 *
 * Los tests que necesitan datos concretos (un curso publicado, una cuenta) los crean ellos
 * mismos o se saltan solos: un test que depende de que alguien haya sembrado la base a mano
 * falla por motivos que no son el código.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',

  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'on-first-retry',
    locale: 'es-ES',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // El plan exige que la plataforma funcione hasta 375 px (T-05).
    { name: 'mobile', use: { ...devices['iPhone 13'] } },
  ],

  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'npm run dev',
        url: 'http://localhost:5173',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
      },
});
