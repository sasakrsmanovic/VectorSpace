import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests/browser', timeout: 180_000, expect: { timeout: 20_000 },
  fullyParallel: false, workers: 1, retries: 0,
  reporter: [['list'], ['html', { outputFolder: 'artifacts/playwright-report', open: 'never' }], ['junit', { outputFile: 'artifacts/browser-results.xml' }]],
  use: {
    baseURL: process.env.VECTORSPACE_URL || 'http://127.0.0.1:4173/VectorSpace/',
    viewport: { width: 1680, height: 1000 },
    trace: 'retain-on-failure', screenshot: 'only-on-failure',
    launchOptions: { args: ['--enable-unsafe-swiftshader', '--use-gl=angle', '--use-angle=swiftshader'] }
  },
  outputDir: 'artifacts/test-results'
});
