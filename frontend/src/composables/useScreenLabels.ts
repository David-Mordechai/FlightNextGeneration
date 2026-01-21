import { ref } from 'vue';
import * as Cesium from 'cesium';

export interface ScreenLabel {
    id: string;
    name: string;
    subLabel?: string; // New field for ETA/Dist
    x: number;
    y: number;
    visible: boolean;
    type: 'home' | 'target' | 'zone' | 'uav';
    noBackground?: boolean; 
}

// Global state for labels to be shared across composables
const screenLabels = ref<ScreenLabel[]>([]);
const trackedEntities = new Map<string, {
    positionCallback: () => Cesium.Cartesian3 | undefined;
    name: string;
    subLabel?: string; // Stored here
    type: 'home' | 'target' | 'zone' | 'uav';
    yOffset?: number;
    noBackground?: boolean;
}>();

let lastUpdateTime = 0;
const UPDATE_INTERVAL = 33; // ~30 FPS
let updateListener: ((scene: Cesium.Scene, time: Cesium.JulianDate) => void) | null = null;

export function useScreenLabels() {
    
    const registerLabel = (id: string, name: string, type: 'home' | 'target' | 'zone' | 'uav', positionCallback: () => Cesium.Cartesian3 | undefined, options: { yOffset?: number, noBackground?: boolean, subLabel?: string } = {}) => {
        trackedEntities.set(id, { name, type, positionCallback, ...options });
    };

    const unregisterLabel = (id: string) => {
        trackedEntities.delete(id);
    };

    const updateScreenLabels = (scene: Cesium.Scene) => {
        const now = performance.now();
        if (now - lastUpdateTime < UPDATE_INTERVAL) return;
        lastUpdateTime = now;

        const newLabels: ScreenLabel[] = [];
        
        trackedEntities.forEach((data, id) => {
            try {
                const pos = data.positionCallback();
                if (pos) {
                    // Fast screen check: avoid projecting if clearly behind camera
                    const screenPos = Cesium.SceneTransforms.worldToWindowCoordinates(scene, pos);
                    if (screenPos) {
                        newLabels.push({
                            id,
                            name: data.name,
                            subLabel: data.subLabel, // Pass through
                            x: screenPos.x,
                            y: screenPos.y - (data.yOffset || 0),
                            visible: true,
                            type: data.type,
                            noBackground: data.noBackground
                        });
                    }
                }
            } catch (e) {
                // Silently ignore transient rendering errors during HMR
            }
        });
        screenLabels.value = newLabels;
    };

    const initializeLabelSystem = (viewer: Cesium.Viewer) => {
        if (updateListener) {
            viewer.scene.postRender.removeEventListener(updateListener);
        }
        
        updateListener = (_scene: Cesium.Scene, _time: Cesium.JulianDate) => {
            updateScreenLabels(viewer.scene);
        };
        
        viewer.scene.postRender.addEventListener(updateListener);
    };

    const destroyLabelSystem = (viewer: Cesium.Viewer) => {
        if (updateListener) {
            viewer.scene.postRender.removeEventListener(updateListener);
            updateListener = null;
        }
    };

    return {
        screenLabels,
        registerLabel,
        unregisterLabel,
        initializeLabelSystem,
        destroyLabelSystem
    };
}
