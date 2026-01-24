import { test, expect } from '@playwright/test';

test('Create Rectangle Zone', async ({ page }) => {
  // Enable Console Logs
  page.on('console', msg => console.log(`BROWSER: ${msg.text()}`));

  // 1. Navigate to the app
  await page.goto('http://localhost:5173');

  // Wait for Cesium to load (canvas to be present)
  // Use the first widget (Main Map) specifically
  const mapWidget = page.locator('.cesium-widget').first();
  const mapCanvas = mapWidget.locator('canvas');
  await expect(mapCanvas).toBeVisible({ timeout: 15000 });

  // 2. Click "Create Rectangle Zone"
  await page.getByText('Create Rectangle Zone').click();
  
  await page.waitForTimeout(1500); // Wait for state change

  // 3. Simulate Drag on Map (Center Screen)
  const box = await mapCanvas.boundingBox();
  if (box) {
    const centerX = box.x + box.width / 2;
    const centerY = box.y + box.height / 2;
    
    // Start slightly offset
    const startX = centerX - 100;
    const startY = centerY - 100;
    const endX = centerX + 100;
    const endY = centerY + 100;

    console.log(`Test Drag: ${startX},${startY} -> ${endX},${endY}`);

    // Move to start
    await page.mouse.move(startX, startY);
    await page.waitForTimeout(100);
    
    // Press Left Down
    await page.mouse.down();
    await page.waitForTimeout(100);
    
    // Drag faster
    await page.mouse.move(endX, endY, { steps: 5 });
    await page.waitForTimeout(100);
    
    // Release Left Up
    await page.mouse.up();
    await page.waitForTimeout(500); // Wait for modal animation
  }

  // 4. Assert Modal Appears
  // The header is "CREATE NO-FLY ZONE" inside an h3
  const modalHeader = page.getByText('CREATE NO-FLY ZONE');
  await expect(modalHeader).toBeVisible({ timeout: 5000 });
});
