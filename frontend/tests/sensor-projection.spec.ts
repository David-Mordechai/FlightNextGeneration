import { test, expect } from '@playwright/test';

test('Sensor Projection Clamping to 20km', async ({ page }) => {
  // Enable Console Logs
  page.on('console', msg => console.log(`BROWSER: ${msg.text()}`));
  page.on('pageerror', exception => console.log(`PAGE ERROR: ${exception}`));

  // 1. Navigate to App
  await page.goto('http://localhost:5173');

  // Wait for Map
  const mapWidget = page.locator('.cesium-widget').first();
  await expect(mapWidget).toBeVisible({ timeout: 15000 });

  console.log('Map widget visible. Waiting for testViewer...');

  // 2. Wait for Cesium Viewer to be exposed
  try {
      await page.waitForFunction(() => (window as any).testViewer !== undefined, null, { timeout: 10000 });
  } catch (e) {
      console.log('Timed out waiting for testViewer. Checking window state...');
      const windowState = await page.evaluate(() => ({
          hasCesium: !!(window as any).Cesium,
          hasViewer: !!(window as any).testViewer,
          bodyHtml: document.body.innerHTML.substring(0, 500)
      }));
      console.log('Window State:', windowState);
      throw e;
  }

  // 3. Verify Projection Clamping
  const result = await page.evaluate(async () => {
    const viewer = (window as any).testViewer;
    const MAX_ALLOWED_DIST = 20100.0; // 20km + 100m buffer
    const checkDuration = 5000;
    const startTime = performance.now();

    while (performance.now() - startTime < checkDuration) {
        const uav = viewer.entities.getById('UAV-100');
        const entities = viewer.entities.values;
        const polygonEntity = entities.find((e: any) => e.polygon && e.polygon.material); 

        if (uav && polygonEntity) {
            const time = viewer.clock.currentTime;
            const uavPos = uav.position?.getValue(time);
            const hierarchy = polygonEntity.polygon.hierarchy.getValue(time);
            
            if (uavPos && hierarchy && hierarchy.positions) {
                for (const point of hierarchy.positions) {
                    const dx = point.x - uavPos.x;
                    const dy = point.y - uavPos.y;
                    const dz = point.z - uavPos.z;
                    const dist = Math.sqrt(dx*dx + dy*dy + dz*dz);

                    if (dist > MAX_ALLOWED_DIST) {
                        return { 
                            success: false, 
                            error: `Point exceeds limit: ${dist.toFixed(1)}m > ${MAX_ALLOWED_DIST}m`,
                            dist: dist
                        };
                    }
                }
            }
        }
        await new Promise(r => setTimeout(r, 100));
    }
    return { success: true };
  });

  expect(result.success).toBe(true);
});
