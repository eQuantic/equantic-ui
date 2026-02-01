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

test.describe('SSR vs CSR Comparison', () => {
  test('Deve detectar diferenças reais entre SSR e CSR', async ({ page }) => {
    // Capture ALL browser console logs
    const logs: string[] = [];
    const errors: string[] = [];

    page.on('console', msg => {
      const text = msg.text();
      logs.push(text);

      // Log important messages
      if (text.includes('[boot]') ||
          text.includes('registerTheme') ||
          text.includes('ServiceProvider') ||
          text.includes('IAppTheme')) {
        console.log(`BROWSER: ${text}`);
      }
    });

    page.on('pageerror', error => {
      const msg = `PAGE ERROR: ${error.message}`;
      errors.push(msg);
      console.log(msg);
    });

    page.on('requestfailed', request => {
      const msg = `REQUEST FAILED: ${request.url()} - ${request.failure()?.errorText}`;
      errors.push(msg);
      console.log(msg);
    });

    // ====================
    // PHASE 1: Capture SSR (before hydration)
    // ====================
    console.log('\n=== PHASE 1: SSR Capture ===');

    const response = await page.goto('/');
    expect(response?.status()).toBe(200);

    // Wait for SSR marker but DON'T wait for hydration
    await page.waitForSelector('#app[data-ssr="true"]', { timeout: 5000 });

    // Capture SSR HTML IMMEDIATELY (before JS loads)
    const ssrButtons = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map((btn, idx) => ({
        index: idx,
        text: btn.textContent?.trim() || '',
        classes: btn.className,
        html: btn.outerHTML
      }));
    });

    console.log(`Captured ${ssrButtons.length} SSR buttons`);

    // ====================
    // PHASE 2: Wait for CSR hydration
    // ====================
    console.log('\n=== PHASE 2: CSR Hydration ===');

    // Wait for network idle (JS loaded and executed)
    await page.waitForLoadState('networkidle');

    // Give extra time for hydration to complete
    await page.waitForTimeout(2000);

    // Capture CSR HTML AFTER hydration
    const csrButtons = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map((btn, idx) => ({
        index: idx,
        text: btn.textContent?.trim() || '',
        classes: btn.className,
        html: btn.outerHTML
      }));
    });

    console.log(`Captured ${csrButtons.length} CSR buttons`);

    // ====================
    // PHASE 3: Compare and Report
    // ====================
    console.log('\n=== PHASE 3: Comparison ===');

    expect(csrButtons.length).toBe(ssrButtons.length);

    const differences: any[] = [];

    for (let i = 0; i < ssrButtons.length; i++) {
      const ssr = ssrButtons[i];
      const csr = csrButtons[i];

      // Compare classes
      const diff = compareClasses(ssr.classes, csr.classes);

      if (diff.missing.length > 0 || diff.extra.length > 0) {
        differences.push({
          button: i,
          text: ssr.text,
          ssr: {
            classes: ssr.classes,
            html: ssr.html
          },
          csr: {
            classes: csr.classes,
            html: csr.html
          },
          diff: {
            missing: diff.missing,
            extra: diff.extra
          }
        });

        console.log(`\n❌ DIFFERENCE in Button ${i} (${ssr.text}):`);
        console.log(`   SSR classes: ${ssr.classes}`);
        console.log(`   CSR classes: ${csr.classes}`);
        if (diff.missing.length > 0) {
          console.log(`   Missing in CSR: ${diff.missing.join(', ')}`);
        }
        if (diff.extra.length > 0) {
          console.log(`   Extra in CSR: ${diff.extra.join(', ')}`);
        }
      } else {
        console.log(`✓ Button ${i} (${ssr.text}): Identical`);
      }
    }

    // ====================
    // PHASE 4: Save detailed report
    // ====================
    const report = {
      timestamp: new Date().toISOString(),
      summary: {
        totalButtons: ssrButtons.length,
        differences: differences.length,
        identical: ssrButtons.length - differences.length
      },
      logs: {
        total: logs.length,
        unique: [...new Set(logs)].length,
        bootLogs: logs.filter(l => l.includes('[boot]')),
        themeLogs: logs.filter(l => l.includes('registerTheme')),
        serviceProviderLogs: logs.filter(l => l.includes('ServiceProvider'))
      },
      errors,
      differences,
      allButtons: {
        ssr: ssrButtons,
        csr: csrButtons
      }
    };

    const reportPath = join(__dirname, '../test-results/ssr-csr-diff-report.json');
    writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf-8');

    console.log('\n=== SUMMARY ===');
    console.log(`Total Buttons: ${report.summary.totalButtons}`);
    console.log(`Identical: ${report.summary.identical}`);
    console.log(`Differences: ${report.summary.differences}`);
    console.log(`\nBrowser Logs:`);
    console.log(`  Total: ${report.logs.total}`);
    console.log(`  Boot logs: ${report.logs.bootLogs.length}`);
    console.log(`  Theme logs: ${report.logs.themeLogs.length}`);
    console.log(`  Errors: ${errors.length}`);
    console.log(`\nDetailed report saved to: test-results/ssr-csr-diff-report.json`);

    // Print sample of important logs
    if (report.logs.bootLogs.length > 0) {
      console.log('\nBoot logs:');
      report.logs.bootLogs.slice(0, 5).forEach(log => console.log(`  ${log}`));
    } else {
      console.log('\n⚠️  NO BOOT LOGS FOUND - boot() may not be executing!');
    }

    if (report.logs.themeLogs.length > 0) {
      console.log('\nTheme logs:');
      report.logs.themeLogs.slice(0, 5).forEach(log => console.log(`  ${log}`));
    } else {
      console.log('\n⚠️  NO THEME LOGS FOUND - __registerTheme() may not be executing!');
    }

    // ====================
    // ASSERTION: Fail if differences found
    // ====================
    if (differences.length > 0) {
      const msg = `Found ${differences.length} buttons with different classes between SSR and CSR. Check test-results/ssr-csr-diff-report.json for details.`;
      console.log(`\n❌ TEST FAILED: ${msg}`);
      expect(differences).toEqual([]); // This will fail and show the differences
    } else {
      console.log('\n✅ TEST PASSED: All buttons identical between SSR and CSR');
    }
  });
});
