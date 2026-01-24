<script setup lang="ts">
import { onMounted, ref, shallowRef } from 'vue';
import * as Cesium from 'cesium';
import "cesium/Build/Cesium/Widgets/widgets.css";
import { useCesiumSimulatedEntities } from './composables/useCesiumSimulatedEntities';

const container = ref<HTMLElement | null>(null);
const viewer = shallowRef<Cesium.Viewer | null>(null);
const { createSimulatedEntities } = useCesiumSimulatedEntities();
const FEET_TO_METERS = 0.3048;

onMounted(async () => {
    if (!container.value) return;

    try {
        const v = new Cesium.Viewer(container.value, {
            terrainProvider: await Cesium.createWorldTerrainAsync(),
            animation: false,
            baseLayerPicker: false,
            fullscreenButton: false,
            geocoder: false,
            homeButton: false,
            infoBox: false,
            sceneModePicker: false,
            selectionIndicator: false,
            timeline: false,
            navigationHelpButton: false,
            scene3DOnly: true,
            useBrowserRecommendedResolution: true,
            requestRenderMode: true,
            maximumRenderTimeChange: 0.033,
            contextOptions: {
                webgl: {
                    failIfMajorPerformanceCaveat: false,
                }
            }
        });

        // Enable Fog for realistic visibility (Hides distant objects beyond ~20km)
        v.scene.fog.enabled = true;
        v.scene.fog.density = 0.00025; 
        v.scene.globe.showGroundAtmosphere = true;
        v.scene.highDynamicRange = false;
        (v.cesiumWidget.creditContainer as HTMLElement).style.display = 'none';

        const esri = await Cesium.ArcGisMapServerImageryProvider.fromUrl(
            'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer'
        );
        v.imageryLayers.addImageryProvider(esri);

        // Load Simulated Entities (Isolated context)
        createSimulatedEntities(v, false, '?v=pip_isolated', 20000);

        viewer.value = v;
        const currentFov = ref(5.0); // Default Vertical FOV

        // Listen for telemetry from parent
        window.addEventListener('message', (event) => {
            const data = event.data;
            
            // Handle Zoom
            if (data.type === 'UPDATE_FOV') {
                currentFov.value = data.payload;
            }

            if (data.type === 'UPDATE_CAMERA' && v && !v.isDestroyed()) {
                const { lat, lng, altitude, yaw, pitch } = data.payload;
                
                const position = Cesium.Cartesian3.fromDegrees(lng, lat, altitude * FEET_TO_METERS);
                
                // Force FOV
                if (v.camera.frustum instanceof Cesium.PerspectiveFrustum) {
                    v.camera.frustum.fov = Cesium.Math.toRadians(currentFov.value);
                }

                v.camera.setView({
                    destination: position,
                    orientation: {
                        heading: Cesium.Math.toRadians(yaw),
                        pitch: Cesium.Math.toRadians(pitch),
                        roll: 0.0
                    }
                });
                
                v.scene.requestRender();
            }
        });

        // Notify parent we are ready
        window.parent.postMessage({ type: 'PIP_READY' }, '*');

    } catch (e) {
        console.error("PiP Viewer Init Failed", e);
    }
});
</script>

<template>
    <div ref="container" class="w-full h-full bg-black"></div>
</template>