import { test, expect } from '@playwright/test';

test('Create Rectangle Zone', async ({ page }) => {
  // Enable Console Logs
  page.on('console', msg => console.log(`BROWSER: ${msg.text()}`));

  // 1. Navigate to the app
  await page.goto('http://localhost:5173');

  // Wait for Cesium to load (canvas to be present)
  const mapCanvas = page.locator('.cesium-widget canvas');
  await expect(mapCanvas).toBeVisible({ timeout: 10000 });

  // 1.5 Open the Sidebar (Drawer)
  await page.locator('label.drawer-button').first().click();
  await page.waitForTimeout(500);

  // 2. Click "Create Rectangle Zone"
  await page.getByText('Create Rectangle Zone').click();
  
  // Close the drawer to unblock the map
  // Click the overlay (standard DaisyUI behavior)
  await page.locator('label.drawer-overlay').click();
  
  await page.waitForTimeout(500); // Wait for animation

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
    await page.waitForTimeout(200);
    
    // Press Left Down
    await page.mouse.down();
    await page.waitForTimeout(200);
    
    // Drag slowly
    await page.mouse.move(endX, endY, { steps: 20 });
    await page.waitForTimeout(200);
    
    // Release Left Up
    await page.mouse.up();
    await page.waitForTimeout(500); // Wait for modal animation
  }

  // 4. Assert Modal Appears
  // The header is "CREATE NO-FLY ZONE" inside an h3
  const modalHeader = page.getByText('CREATE NO-FLY ZONE');
  await expect(modalHeader).toBeVisible({ timeout: 5000 });
});
