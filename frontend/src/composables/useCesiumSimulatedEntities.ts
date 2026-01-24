import * as Cesium from 'cesium';

export function useCesiumSimulatedEntities() {
    
    const createSimulatedEntities = (targetViewer: Cesium.Viewer | null, includeLabels: boolean = true, uriSuffix: string = '', maxViewDistance?: number) => {
        if (!targetViewer) return;

        // Display Condition (Visibility Limit)
        const displayCondition = maxViewDistance 
            ? new Cesium.DistanceDisplayCondition(0.0, maxViewDistance) 
            : undefined;

        // Supply Truck (The "Track")
        const groundEntities = [
            {
                name: 'Supply Truck',
                lat: 31.5632,
                lng: 34.5435,
                modelUri: '/GroundVehicle.glb' + uriSuffix,
                scale: 4,
                type: 'vehicle',
                dim: { x: 8, y: 3, z: 4 }
            }
        ];

        const createdEntities = groundEntities.map(data => {
            const entityConfig: any = {
                name: data.name,
                position: Cesium.Cartesian3.fromDegrees(data.lng, data.lat, 0),
            };

            if (includeLabels) {
                entityConfig.label = {
                    text: data.name,
                    font: '10px monospace',
                    fillColor: Cesium.Color.WHITE,
                    outlineColor: Cesium.Color.BLACK,
                    outlineWidth: 2,
                    style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                    verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                    pixelOffset: new Cesium.Cartesian2(0, -15),
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY,
                    scaleByDistance: new Cesium.NearFarScalar(500, 1.0, 5000, 0.0)
                };
            }

            if (data.modelUri) {
                const separator = data.modelUri.includes('?') ? '&' : '?';
                const cacheBuster = `${separator}cb=${Math.random().toString(36).substring(7)}`;
                entityConfig.model = {
                    uri: data.modelUri + cacheBuster,
                    minimumPixelSize: 32,
                    scale: data.scale || 1.0,
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                    distanceDisplayCondition: displayCondition
                };
            } 

            return targetViewer.entities.add(entityConfig);
        });
        
        return createdEntities;
    };

    return {
        createSimulatedEntities
    };
}