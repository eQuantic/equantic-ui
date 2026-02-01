import { test, expect } from '@playwright/test';

/**
 * Normaliza HTML removendo atributos dinâmicos e espaços em branco
 */
function normalizeHTML(html: string): string {
  return html
    // Remove timestamps dinâmicos (ex: "18:22")
    .replace(/\d{2}:\d{2}/g, 'HH:MM')
    // Remove GUIDs
    .replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, 'GUID')
    // Remove build IDs
    .replace(/v=[a-z0-9]+/gi, 'v=BUILD_ID')
    // Remove espaços extras
    .replace(/\s+/g, ' ')
    .trim();
}

/**
 * Extrai apenas as classes CSS de um elemento, removendo estilos inline dinâmicos
 */
function extractClasses(html: string): string[] {
  const classMatches = html.match(/class="([^"]*)"/g) || [];
  return classMatches.map(match => {
    const classes = match.replace(/class="([^"]*)"/, '$1').split(' ').filter(c => c);
    return classes.sort().join(' ');
  }).sort();
}

test.describe('SSR Hydration', () => {
  test('SSR e CSR devem gerar HTML idêntico', async ({ page }) => {
    // 1. Captura o HTML inicial (SSR)
    const response = await page.goto('/');
    expect(response?.status()).toBe(200);

    // Aguarda o container estar presente
    await page.waitForSelector('#app[data-ssr="true"]');

    // Captura o HTML do SSR (antes da hidratação)
    const ssrHTML = await page.evaluate(() => {
      const app = document.querySelector('#app');
      return app?.innerHTML || '';
    });

    expect(ssrHTML).not.toBe('');
    console.log('[SSR] HTML length:', ssrHTML.length);

    // 2. Aguarda a hidratação completar
    // Espera o boot do runtime executar (aguarda network idle)
    await page.waitForLoadState('networkidle');

    // Aguarda um pouco para garantir que a hidratação terminou
    await page.waitForTimeout(1000);

    // 3. Captura o HTML após hidratação (CSR)
    const csrHTML = await page.evaluate(() => {
      const app = document.querySelector('#app');
      return app?.innerHTML || '';
    });

    console.log('[CSR] HTML length:', csrHTML.length);

    // 4. Normaliza e compara
    const normalizedSSR = normalizeHTML(ssrHTML);
    const normalizedCSR = normalizeHTML(csrHTML);

    // Compara comprimentos primeiro (indicador rápido de diferença)
    expect(normalizedCSR.length).toBe(normalizedSSR.length);

    // Compara HTML completo
    expect(normalizedCSR).toBe(normalizedSSR);
  });

  test('Todos os botões devem ter classes de tema aplicadas', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#app[data-ssr="true"]');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Verifica que todos os botões têm as classes base do tema
    const buttons = await page.locator('button').all();

    for (const button of buttons) {
      const classes = await button.getAttribute('class');

      // Todo botão deve ter as classes base do tema
      expect(classes).toContain('inline-flex');
      expect(classes).toContain('items-center');
      expect(classes).toContain('justify-center');
      expect(classes).toContain('rounded-lg');
      expect(classes).toContain('font-medium');
      expect(classes).toContain('transition-all');
    }

    console.log(`✓ Verificados ${buttons.length} botões com tema aplicado`);
  });

  test('Classes CSS devem ser idênticas entre SSR e CSR', async ({ page }) => {
    // Captura classes do SSR
    const response = await page.goto('/');
    expect(response?.status()).toBe(200);

    await page.waitForSelector('#app[data-ssr="true"]');

    const ssrClasses = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map(btn => btn.className);
    });

    // Aguarda hidratação
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Captura classes do CSR
    const csrClasses = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button'));
      return buttons.map(btn => btn.className);
    });

    // Compara cada botão
    expect(csrClasses.length).toBe(ssrClasses.length);

    for (let i = 0; i < ssrClasses.length; i++) {
      const ssrClassList = ssrClasses[i].split(' ').filter(c => c).sort().join(' ');
      const csrClassList = csrClasses[i].split(' ').filter(c => c).sort().join(' ');

      expect(csrClassList).toBe(ssrClassList);
    }

    console.log(`✓ ${ssrClasses.length} botões verificados - classes idênticas`);
  });

  test('Estado inicial deve ser hidratado corretamente', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#app[data-ssr="true"]');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Verifica que os dados do estado estão presentes
    const todos = await page.locator('.flex.flex-col.gap-\\[5px\\] > div').count();

    // Deve ter pelo menos os 2 todos padrão
    expect(todos).toBeGreaterThanOrEqual(2);

    // Verifica textos dos todos
    const todoTexts = await page.locator('span.flex-1.transition-all').allTextContents();
    expect(todoTexts).toContain('Learn eQuantic.UI');
    expect(todoTexts).toContain('Build TodoList App');

    console.log(`✓ ${todos} todos carregados corretamente`);
  });

  test('Eventos devem funcionar após hidratação', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#app[data-ssr="true"]');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Conta todos iniciais
    const initialCount = await page.locator('.flex.flex-col.gap-\\[5px\\] > div').count();

    // Clica no botão "Add"
    await page.fill('input[placeholder="New task..."]', 'Test Task');
    await page.click('button:has-text("Add")');

    // Aguarda a task ser adicionada
    await page.waitForTimeout(500);

    // Verifica que a nova task foi adicionada
    const newCount = await page.locator('.flex.flex-col.gap-\\[5px\\] > div').count();
    expect(newCount).toBe(initialCount + 1);

    // Verifica que a task está na lista
    const todoTexts = await page.locator('span.flex-1.transition-all').allTextContents();
    expect(todoTexts).toContain('Test Task');

    console.log('✓ Eventos funcionando após hidratação');
  });
});
