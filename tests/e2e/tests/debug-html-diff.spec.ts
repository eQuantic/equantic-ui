import { test, expect } from '@playwright/test';
import { writeFileSync } from 'fs';
import { join } from 'path';

/**
 * Extract classes from HTML element, preserving order
 */
function extractClasses(html: string): string {
  const match = html.match(/class="([^"]*)"/);
  return match ? match[1] : '';
}

/**
 * Compare two class strings and return differences
 */
function compareClasses(ssr: string, csr: string): { missing: string[], extra: string[] } {
  const ssrClasses = ssr.split(' ').filter(c => c.trim());
  const csrClasses = csr.split(' ').filter(c => c.trim());

  const missing = ssrClasses.filter(c => !csrClasses.includes(c));
  const extra = csrClasses.filter(c => !ssrClasses.includes(c));

  return { missing, extra };
}

test.describe('Debug HTML Diff', () => {
  test('Detectar diferenças entre SSR e CSR', async ({ page }) => {
    // Capture ALL browser console logs
    const logs: string[] = [];
    page.on('console', msg => {
      const text = msg.text();
      logs.push(text);
      console.log(`BROWSER: ${text}`);
    });

    // Capture page errors
    page.on('pageerror', error => {
      console.log(`PAGE ERROR: ${error.message}`);
      console.log(error.stack);
    });

    // Capture request failures
    page.on('requestfailed', request => {
      console.log(`REQUEST FAILED: ${request.url()} - ${request.failure()?.errorText}`);
    });

    // 1. Captura o HTML inicial (SSR) - ANTES da hidratação
    const response = await page.goto('/');
    expect(response?.status()).toBe(200);

    await page.waitForSelector('#app[data-ssr="true"]');

    // Captura o HTML completo da página SSR
    const ssrFullHTML = await page.content();
    const ssrAppHTML = await page.evaluate(() => {
      const app = document.querySelector('#app');
      return app?.outerHTML || '';
    });

    console.log('[SSR] Full HTML length:', ssrFullHTML.length);
    console.log('[SSR] App HTML length:', ssrAppHTML.length);

    // Salva SSR HTML
    writeFileSync(
      join(__dirname, '../test-results/ssr-full.html'),
      ssrFullHTML,
      'utf-8'
    );
    writeFileSync(
      join(__dirname, '../test-results/ssr-app.html'),
      ssrAppHTML,
      'utf-8'
    );

    // 2. Aguarda a hidratação completar
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // 3. Captura o HTML após hidratação (CSR)
    const csrFullHTML = await page.content();
    const csrAppHTML = await page.evaluate(() => {
      const app = document.querySelector('#app');
      return app?.outerHTML || '';
    });

    console.log('[CSR] Full HTML length:', csrFullHTML.length);
    console.log('[CSR] App HTML length:', csrAppHTML.length);

    // Salva CSR HTML
    writeFileSync(
      join(__dirname, '../test-results/csr-full.html'),
      csrFullHTML,
      'utf-8'
    );
    writeFileSync(
      join(__dirname, '../test-results/csr-app.html'),
      csrAppHTML,
      'utf-8'
    );

    // 4. Extrai apenas os botões para comparação detalhada
    const ssrButtons = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map((btn, idx) => ({
        index: idx,
        text: btn.textContent?.trim(),
        classes: btn.className,
        outerHTML: btn.outerHTML,
      }));
    });

    // Recarrega a página para pegar o estado CSR limpo
    await page.reload();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    const csrButtons = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map((btn, idx) => ({
        index: idx,
        text: btn.textContent?.trim(),
        classes: btn.className,
        outerHTML: btn.outerHTML,
      }));
    });

    // Compara botões
    console.log('\n=== COMPARAÇÃO DE BOTÕES ===\n');
    for (let i = 0; i < Math.max(ssrButtons.length, csrButtons.length); i++) {
      const ssrBtn = ssrButtons[i];
      const csrBtn = csrButtons[i];

      console.log(`\n--- Botão ${i} ---`);
      console.log('SSR Text:', ssrBtn?.text);
      console.log('CSR Text:', csrBtn?.text);
      console.log('SSR Classes:', ssrBtn?.classes);
      console.log('CSR Classes:', csrBtn?.classes);

      if (ssrBtn?.classes !== csrBtn?.classes) {
        console.log('⚠️  DIFERENÇA DETECTADA!');
        console.log('SSR HTML:', ssrBtn?.outerHTML);
        console.log('CSR HTML:', csrBtn?.outerHTML);
      } else {
        console.log('✓ Classes idênticas');
      }
    }

    // Salva comparação de botões
    writeFileSync(
      join(__dirname, '../test-results/buttons-comparison.json'),
      JSON.stringify({ ssr: ssrButtons, csr: csrButtons }, null, 2),
      'utf-8'
    );

    console.log('\n=== Arquivos salvos em tests/e2e/test-results/ ===');
    console.log('- ssr-full.html');
    console.log('- csr-full.html');
    console.log('- ssr-app.html');
    console.log('- csr-app.html');
    console.log('- buttons-comparison.json');

    // Print all captured logs
    console.log('\n=== ALL BROWSER LOGS ===');
    console.log(`Total logs: ${logs.length}`);
    const uniqueLogs = [...new Set(logs)];
    console.log(`Unique logs: ${uniqueLogs.length}`);
    uniqueLogs.slice(0, 20).forEach((log, i) => console.log(`${i + 1}. ${log.substring(0, 100)}`));
  });
});
