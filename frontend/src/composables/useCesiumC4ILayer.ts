import { ref, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import { c4iService, type NoFlyZone, type Point, PointType } from '../services/C4IService';
import { signalRService } from '../services/SignalRService';

export function useCesiumC4ILayer(viewer: ShallowRef<Cesium.Viewer | null>) {
    const zones = ref<NoFlyZone[]>([]);
    const points = ref<Point[]>([]);
    const entityMap = new Map<string, Cesium.Entity>();

    const showNewZoneModal = ref(false);
    const newZoneForm = ref<{
        id?: string;
        name: string;
        minAltitude: number;
        maxAltitude: number;
    }>({
        name: '',
        minAltitude: 0,
        maxAltitude: 10000
    });

    const showPointModal = ref(false);
    const newPointForm = ref<{
        id?: string;
        name: string;
        type: PointType;
    }>({
        name: '',
        type: PointType.Home
    });

    const isEditing = ref(false);
    const FEET_TO_METERS = 0.3048;

    // Drawing state
    const activeDrawingType = ref<'marker' | 'polygon' | 'rectangle' | null>(null);
    let drawingPositions: Cesium.Cartesian3[] = [];
    let drawingEntity: Cesium.Entity | null = null;
    let eventHandler: Cesium.ScreenSpaceEventHandler | null = null;

    const createPointEntity = (point: Point) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        const coords = point.location.coordinates;
        const color = point.type === PointType.Home ? Cesium.Color.CORNFLOWERBLUE : Cesium.Color.RED;
        
        const position = Cesium.Cartesian3.fromDegrees(coords[0], coords[1], 0);

        const entity = currentViewer.entities.add({
            id: point.id,
            position: position,
            // Flat circle on the ground with the icon
            ellipse: {
                semiMajorAxis: new Cesium.CallbackProperty(() => {
                    if (!viewer.value) return 100;
                    return Math.max(100, viewer.value.camera.positionCartographic.height / 50);
                }, false),
                semiMinorAxis: new Cesium.CallbackProperty(() => {
                    if (!viewer.value) return 100;
                    return Math.max(100, viewer.value.camera.positionCartographic.height / 50);
                }, false),
                material: new Cesium.ImageMaterialProperty({
                    image: point.type === PointType.Home ? '/home.svg' : '/target.svg',
                    color: color,
                    transparent: true
                }),
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                classificationType: Cesium.ClassificationType.TERRAIN
            },
            label: {
                text: point.name,
                font: 'bold 12px monospace',
                fillColor: Cesium.Color.WHITE,
                outlineColor: Cesium.Color.BLACK,
                outlineWidth: 2,
                style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                pixelOffset: new Cesium.Cartesian2(0, -50),
                disableDepthTestDistance: Number.POSITIVE_INFINITY,
                heightReference: Cesium.HeightReference.CLAMP_TO_GROUND
            },
            properties: {
                isPoint: true,
                type: point.type
            }
        });

        entityMap.set(point.id!, entity);
        return entity;
    };

    const createZoneEntity = (zone: NoFlyZone) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        // GeoJSON coordinates are [lng, lat]
        const coords: number[][] = zone.geometry.type === 'Polygon' 
            ? zone.geometry.coordinates[0] 
            : [[zone.geometry.coordinates[0][0], zone.geometry.coordinates[0][1]], 
               [zone.geometry.coordinates[1][0], zone.geometry.coordinates[0][1]],
               [zone.geometry.coordinates[1][0], zone.geometry.coordinates[1][1]],
               [zone.geometry.coordinates[0][0], zone.geometry.coordinates[1][1]]];

        const positions = Cesium.Cartesian3.fromDegreesArray(coords.flat());

        const entity = currentViewer.entities.add({
            id: zone.id,
            name: zone.name,
            polygon: {
                hierarchy: positions,
                height: zone.minAltitude * FEET_TO_METERS,
                extrudedHeight: zone.maxAltitude * FEET_TO_METERS,
                material: new Cesium.StripeMaterialProperty({
                    evenColor: Cesium.Color.RED.withAlpha(0.5),
                    oddColor: Cesium.Color.RED.withAlpha(0.1),
                    repeat: 10,
                    orientation: Cesium.StripeOrientation.VERTICAL
                }),
                outline: true,
                outlineColor: Cesium.Color.RED,
                outlineWidth: 2
            },
            label: {
                text: zone.name,
                font: 'bold 12px monospace',
                fillColor: Cesium.Color.WHITE,
                outlineColor: Cesium.Color.BLACK,
                outlineWidth: 2,
                style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                heightReference: Cesium.HeightReference.RELATIVE_TO_GROUND,
                disableDepthTestDistance: Number.POSITIVE_INFINITY
            }
        });

        entityMap.set(zone.id!, entity);
        return entity;
    };

    const loadNoFlyZones = async () => {
        try {
            const fetchedZones = await c4iService.getAll();
            zones.value = fetchedZones;
            fetchedZones.forEach(createZoneEntity);
        } catch (e) {
            console.error("Failed to load No Fly Zones", e);
        }
    };

    const loadPoints = async () => {
        try {
            const fetchedPoints = await c4iService.getAllPoints();
            points.value = fetchedPoints;
            fetchedPoints.forEach(createPointEntity);
        } catch (e) {
            console.error("Failed to load Points", e);
        }
    };

    const startDrawing = (type: 'marker' | 'polygon' | 'rectangle') => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        stopDrawing();
        activeDrawingType.value = type;
        drawingPositions = [];

        eventHandler = new Cesium.ScreenSpaceEventHandler(currentViewer.scene.canvas);

        eventHandler.setInputAction((click: any) => {
            const position = currentViewer.scene.pickPosition(click.position);
            if (!Cesium.defined(position)) return;

            if (type === 'marker') {
                const cartographic = Cesium.Cartographic.fromCartesian(position);
                const lng = Cesium.Math.toDegrees(cartographic.longitude);
                const lat = Cesium.Math.toDegrees(cartographic.latitude);

                tempMarkerPosition = { lat, lng };
                showPointModal.value = true;
                stopDrawing();
            } else {
                drawingPositions.push(position);
                if (!drawingEntity) {
                    drawingEntity = currentViewer.entities.add({
                        polygon: {
                            hierarchy: new Cesium.CallbackProperty(() => new Cesium.PolygonHierarchy(drawingPositions), false),
                            material: Cesium.Color.RED.withAlpha(0.3)
                        }
                    });
                }
            }
        }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

        eventHandler.setInputAction((movement: any) => {
            if (drawingPositions.length > 0 && drawingEntity) {
                const position = currentViewer.scene.pickPosition(movement.endPosition);
                if (Cesium.defined(position)) {
                    const tempPositions = [...drawingPositions, position];
                    // @ts-ignore
                    drawingEntity.polygon.hierarchy = new Cesium.PolygonHierarchy(tempPositions);
                }
            }
        }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

        eventHandler.setInputAction(() => {
            if (drawingPositions.length > 2) {
                showNewZoneModal.value = true;
                // Capture current positions for saving
                lastDrawingPositions = [...drawingPositions];
                stopDrawing();
            }
        }, Cesium.ScreenSpaceEventType.RIGHT_CLICK);
    };

    let tempMarkerPosition: { lat: number, lng: number } | null = null;
    let lastDrawingPositions: Cesium.Cartesian3[] = [];

    const stopDrawing = () => {
        if (eventHandler) {
            eventHandler.destroy();
            eventHandler = null;
        }
        if (drawingEntity) {
            viewer.value?.entities.remove(drawingEntity);
            drawingEntity = null;
        }
        activeDrawingType.value = null;
    };

    const handleSavePoint = async () => {
        if (!tempMarkerPosition) return;

        const pointData: Point = {
            id: newPointForm.value.id,
            name: newPointForm.value.name,
            type: newPointForm.value.type,
            location: {
                type: 'Point',
                coordinates: [tempMarkerPosition.lng, tempMarkerPosition.lat]
            }
        };

        try {
            const saved = await c4iService.createPoint(pointData);
            points.value.push(saved);
            createPointEntity(saved);
            showPointModal.value = false;
            tempMarkerPosition = null;
        } catch (e) {
            console.error(e);
        }
    };

    const handleSaveZone = async () => {
        if (lastDrawingPositions.length < 3) return;

        const coords = lastDrawingPositions.map(p => {
            const carto = Cesium.Cartographic.fromCartesian(p);
            return [Cesium.Math.toDegrees(carto.longitude), Cesium.Math.toDegrees(carto.latitude)];
        });
        if (coords.length > 0) {
            coords.push(coords[0] as number[]); // Close loop
        }

        const zoneData: NoFlyZone = {
            name: newZoneForm.value.name,
            minAltitude: newZoneForm.value.minAltitude,
            maxAltitude: newZoneForm.value.maxAltitude,
            isActive: true,
            geometry: {
                type: 'Polygon',
                coordinates: [coords]
            }
        };

        try {
            const saved = await c4iService.create(zoneData);
            zones.value.push(saved);
            createZoneEntity(saved);
            showNewZoneModal.value = false;
            lastDrawingPositions = [];
        } catch (e) {
            console.error(e);
        }
    };

    const handleCancelPoint = () => {
        showPointModal.value = false;
        tempMarkerPosition = null;
    };

    const handleCancelZone = () => {
        showNewZoneModal.value = false;
        lastDrawingPositions = [];
    };

    const deleteEntity = async (id: string, isPoint: boolean) => {
        try {
            if (isPoint) {
                await c4iService.deletePoint(id);
                points.value = points.value.filter(p => p.id !== id);
            } else {
                await c4iService.delete(id);
                zones.value = zones.value.filter(z => z.id !== id);
            }
            const entity = entityMap.get(id);
            if (entity) {
                viewer.value?.entities.remove(entity);
                entityMap.delete(id);
            }
        } catch (e) {
            console.error(e);
        }
    };

    const initializeRealtimeUpdates = () => {
        signalRService.onEntityUpdate((update) => {
            if (update.entityType === 'Point') {
                if (update.changeType === 'Created') {
                    createPointEntity(update.data);
                } else if (update.changeType === 'Deleted') {
                    const id = update.data.id || update.data.Id;
                    const entity = entityMap.get(id);
                    if (entity) {
                        viewer.value?.entities.remove(entity);
                        entityMap.delete(id);
                    }
                }
            } else if (update.entityType === 'NoFlyZone') {
                if (update.changeType === 'Created') {
                    createZoneEntity(update.data);
                } else if (update.changeType === 'Deleted') {
                    const id = update.data.id || update.data.Id;
                    const entity = entityMap.get(id);
                    if (entity) {
                        viewer.value?.entities.remove(entity);
                        entityMap.delete(id);
                    }
                }
            }
        });
    };

    return {
        zones,
        points,
        showNewZoneModal,
        newZoneForm,
        showPointModal,
        newPointForm,
        isEditing,
        loadNoFlyZones,
        loadPoints,
        startDrawing,
        handleSavePoint,
        handleSaveZone,
        handleCancelPoint,
        handleCancelZone,
        deleteEntity,
        initializeRealtimeUpdates,
        toggleEditMode: () => { isEditing.value = !isEditing.value; },
        saveEdits: () => { isEditing.value = false; },
        cancelEdits: () => { isEditing.value = false; },
        handleDeleteZone: async () => {
            if (newZoneForm.value.id) {
                await deleteEntity(newZoneForm.value.id, false);
                showNewZoneModal.value = false;
            }
        }
    };
}
