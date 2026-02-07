import { test, expect } from '@playwright/test';

test.describe('TodoList End-to-End Tests', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const app = page.locator('#app');
    await expect(app).toHaveAttribute('data-ssr', 'true', { timeout: 10000 });
    // Wait for tasks to be rendered
    await page.waitForSelector('div.relative.flex.flex-row.items-center.p-4', { state: 'visible', timeout: 10000 });
    await page.waitForTimeout(2000); 
  });

  test('should display tasks from SSR or state', async ({ page }) => {
    const todoItems = page.locator('div.relative.flex.flex-row.items-center.p-4');
    await expect(todoItems).not.toHaveCount(0, { timeout: 10000 });
    
    const todoTexts = page.locator('span.flex-1.transition-all');
    const texts = await todoTexts.allTextContents();
    expect(texts.length).toBeGreaterThanOrEqual(1);
  });

  test('should add a new task', async ({ page }) => {
    const input = page.locator('input[placeholder="Add a new task..."]');
    const addButton = page.getByRole('button', { name: 'Add Task' });
    
    const uniqueTitle = `Task ${Date.now()}`;
    await input.fill(uniqueTitle);
    await addButton.click();
    
    await expect(page.getByText(uniqueTitle)).toBeVisible({ timeout: 5000 });
  });

  test('should toggle task completion', async ({ page }) => {
    const firstTask = page.locator('div.relative.flex.flex-row.items-center.p-4').first();
    const checkbox = firstTask.locator('input[type="checkbox"]');
    const badge = firstTask.locator('span, div').filter({ hasText: /^(•|✓)$/ }).first();

    const initialState = await checkbox.isChecked();
    await checkbox.click(); // Using click() on the checkbox element
    
    if (initialState) {
        await expect(badge).toHaveText('•', { timeout: 5000 });
        await expect(checkbox).not.toBeChecked();
    } else {
        await expect(badge).toHaveText('✓', { timeout: 5000 });
        await expect(checkbox).toBeChecked();
    }
  });

  test('should edit a task title', async ({ page }) => {
    const firstTask = page.locator('div.relative.flex.flex-row.items-center.p-4').first();
    const editButton = firstTask.locator('button:has-text("✏️")');
    
    await firstTask.hover();
    await editButton.click();
    
    // Use getByText to find the modal header and then look for the input
    await expect(page.getByText('Edit Task', { exact: true })).toBeVisible();
    
    // The Modal uses h3 for the title
    const modal = page.locator('div:has(> h3:has-text("Edit Task"))').locator('..');
    const editInput = page.locator('div[role="dialog"] input, div.bg-white input').last();
    
    const updatedTitle = `Updated ${Date.now()}`;
    await editInput.clear();
    await editInput.fill(updatedTitle);
    
    await page.getByRole('button', { name: 'Save Changes' }).click();
    
    await expect(page.getByText(updatedTitle)).toBeVisible({ timeout: 5000 });
  });

  test('should delete a task', async ({ page }) => {
    const todoItems = page.locator('div.relative.flex.flex-row.items-center.p-4');
    await expect(todoItems).not.toHaveCount(0);
    const initialCount = await todoItems.count();
    
    const lastTask = todoItems.last();
    const deleteButton = lastTask.locator('button:has-text("🗑️")');
    
    await lastTask.hover();
    await deleteButton.click();
    
    await expect(todoItems).toHaveCount(initialCount - 1, { timeout: 5000 });
  });

  test('should filter tasks', async ({ page }) => {
    const activeFilter = page.getByRole('button', { name: /Active/ });
    const doneFilter = page.getByRole('button', { name: /Done/ });
    const allFilter = page.getByRole('button', { name: /All/ });
    
    await expect(page.locator('div.relative.flex.flex-row.items-center.p-4')).not.toHaveCount(0);

    await activeFilter.click();
    await page.waitForTimeout(1000); 
    
    await doneFilter.click();
    await page.waitForTimeout(1000);
    
    await allFilter.click();
    await page.waitForTimeout(1000);
    await expect(page.locator('div.relative.flex.flex-row.items-center.p-4')).not.toHaveCount(0);
  });

  test('should clear completed tasks', async ({ page }) => {
    // 1. Ensure there is at least one completed task
    const todoItems = page.locator('div.relative.flex.flex-row.items-center.p-4');
    const firstTask = todoItems.first();
    const checkbox = firstTask.locator('input[type="checkbox"]');
    
    if (!(await checkbox.isChecked())) {
        await checkbox.check();
        await page.waitForTimeout(1000);
    }

    const clearButton = page.getByRole('button', { name: /Clear Completed/ });
    await expect(clearButton).toBeVisible({ timeout: 5000 });
    
    await clearButton.click();
    await expect(clearButton).not.toBeVisible({ timeout: 5000 });
  });

  test('should open and close settings drawer', async ({ page }) => {
    const settingsButton = page.locator('button:has-text("⚙️")');
    await settingsButton.click();
    
    await expect(page.getByText('Settings', { exact: true })).toBeVisible({ timeout: 5000 });
    
    const closeButton = page.getByRole('button', { name: 'Back to List' });
    await closeButton.click();
    await expect(page.getByText('Settings', { exact: true })).not.toBeVisible({ timeout: 5000 });
  });

});
