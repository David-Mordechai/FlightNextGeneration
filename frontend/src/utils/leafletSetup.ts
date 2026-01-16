import L from 'leaflet';

export async function setupLeafletPatches() {
    // 1. Icon Fixes
    delete (L.Icon.Default.prototype as any)._getIconUrl;
    L.Icon.Default.mergeOptions({
        iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png',
        iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png',
        shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
    });

    // 2. Leaflet-Draw Imports and Patches
    // Dynamic import to ensure order regarding previous patches if any
    await import('leaflet-draw/dist/leaflet.draw.css');
    await import('leaflet-draw');

    // Patch readableArea bug (fixes Rectangle crash)
    if ((L as any).GeometryUtil) {
        (L as any).GeometryUtil.readableArea = (area: number, isMetric: boolean) => {
            return area.toFixed(2) + (isMetric ? ' m²' : ' sq ft');
        };
    }
}

export const createUavIcon = (heading: number, altitude: number) => {
    const scale = Math.max(0.3, Math.min(1.8, 3000 / altitude));
    return L.divIcon({
        className: 'uav-marker-container',
        html: `<img src="/Orbiter3.png" class="uav-icon" style="transform: rotate(${heading}deg) scale(${scale}); width: 64px; height: 64px;" />`,
        iconSize: [64, 64],
        iconAnchor: [32, 32],
        popupAnchor: [0, -32]
    });
};
