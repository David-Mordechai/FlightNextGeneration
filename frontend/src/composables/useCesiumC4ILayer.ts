import { ref, type ShallowRef } from 'vue';
import * as Cesium from 'cesium';
import { c4iService, type NoFlyZone, type Point, PointType } from '../services/C4IService';
import { signalRService } from '../services/SignalRService';
import { useScreenLabels } from './useScreenLabels';

export function useCesiumC4ILayer(viewer: ShallowRef<Cesium.Viewer | null>) {
    const zones = ref<NoFlyZone[]>([]);
    const points = ref<Point[]>([]);
    const entityMap = new Map<string, Cesium.Entity>();
    
    // Shared Label System
    const { registerLabel, unregisterLabel } = useScreenLabels();

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
    let tempMarkers: Cesium.Entity[] = []; 
    let eventHandler: Cesium.ScreenSpaceEventHandler | null = null;

    // Helper to generate crisp HUD marker images
    const createMarkerImage = (type: PointType, colorCss: string) => {
        const size = 128;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        if (!ctx) return '';

        ctx.shadowColor = 'rgba(0,0,0,0.4)';
        ctx.shadowBlur = 15;
        ctx.shadowOffsetY = 5;

        if (type === PointType.Home) {
            ctx.fillStyle = colorCss;
            ctx.strokeStyle = '#FFFFFF';
            ctx.lineWidth = 6;
            ctx.beginPath();
            ctx.arc(64, 50, 40, 0, Math.PI * 2);
            ctx.moveTo(64, 128); // Touch bottom
            ctx.lineTo(30, 70);
            ctx.lineTo(98, 70);
            ctx.lineTo(64, 128);
            ctx.fill();
            ctx.stroke();
            ctx.shadowColor = 'transparent';
            ctx.fillStyle = '#FFFFFF';
            const iconScale = 0.5;
            ctx.translate(64, 50);
            ctx.scale(iconScale, iconScale);
            ctx.translate(-64, -64);
            ctx.beginPath();
            ctx.moveTo(64, 30);
            ctx.lineTo(96, 60);
            ctx.lineTo(96, 98);
            ctx.lineTo(32, 98);
            ctx.lineTo(32, 60);
            ctx.fill();
        } else {
            ctx.strokeStyle = colorCss;
            ctx.lineWidth = 10;
            ctx.beginPath();
            ctx.arc(64, 64, 45, 0, Math.PI * 2);
            ctx.stroke();
            ctx.lineWidth = 8;
            ctx.beginPath();
            ctx.moveTo(64, 10); ctx.lineTo(64, 35);
            ctx.moveTo(64, 93); ctx.lineTo(64, 118);
            ctx.moveTo(10, 64); ctx.lineTo(35, 64);
            ctx.moveTo(93, 64); ctx.lineTo(118, 64);
            ctx.stroke();
            ctx.fillStyle = colorCss;
            ctx.beginPath();
            ctx.arc(64, 64, 8, 0, Math.PI * 2);
            ctx.fill();
        }
        return canvas.toDataURL();
    };

    const createPointEntity = (point: Point) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        try {
            const coords = point.location.coordinates;
            const isHome = (point.type === PointType.Home || (point as any).type === 0);
            const colorCss = isHome ? '#2563EB' : '#DC2626';
            // Inline color creation
            const markerImage = createMarkerImage(isHome ? PointType.Home : PointType.Target, colorCss);
            
            // Cache height to avoid sampling every frame
            let cachedHeight = currentViewer.scene.globe.getHeight(Cesium.Cartographic.fromDegrees(coords[0], coords[1])) || 0;
            
            // Sample terrain height once for the label position
            const carto = Cesium.Cartographic.fromDegrees(coords[0], coords[1]);
            Cesium.sampleTerrainMostDetailed(currentViewer.terrainProvider, [carto]).then((samples) => {
                if (samples && samples[0].height !== undefined) {
                    cachedHeight = samples[0].height;
                }
            });

            if (entityMap.has(point.id!)) {
                currentViewer.entities.remove(entityMap.get(point.id!)!);
                unregisterLabel(point.id!);
            }

            const entity = currentViewer.entities.add({
                id: point.id,
                position: Cesium.Cartesian3.fromDegrees(coords[0], coords[1], 0) as any, // Base position
                billboard: {
                    image: markerImage,
                    scale: 0.6,
                    verticalOrigin: isHome ? Cesium.VerticalOrigin.BOTTOM : Cesium.VerticalOrigin.CENTER,
                    horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY,
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND, 
                    scaleByDistance: new Cesium.NearFarScalar(1.0e2, 0.6, 8.0e6, 0.3) 
                },
                properties: { isPoint: true, type: point.type }
            });

            const yOffsetBase = isHome ? 50 : 35;
            
            registerLabel(point.id!, point.name.toUpperCase(), isHome ? 'home' : 'target', () => {
                // Return position with cached terrain height so label stays above ground
                return Cesium.Cartesian3.fromDegrees(coords[0], coords[1], cachedHeight);
            }, { yOffset: yOffsetBase });

            entityMap.set(point.id!, entity);
            return entity;
        } catch (error) {
            console.error("Error creating point entity:", error, point);
            return null;
        }
    };

    const createZoneEntity = (zone: NoFlyZone) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        const coords: number[][] = zone.geometry.type === 'Polygon' 
            ? zone.geometry.coordinates[0] 
            : [[zone.geometry.coordinates[0][0], zone.geometry.coordinates[0][1]], 
               [zone.geometry.coordinates[1][0], zone.geometry.coordinates[0][1]],
               [zone.geometry.coordinates[1][0], zone.geometry.coordinates[1][1]],
               [zone.geometry.coordinates[0][0], zone.geometry.coordinates[1][1]]];

        const positions = Cesium.Cartesian3.fromDegreesArray(coords.flat());
        const zoneMaterial = new Cesium.ColorMaterialProperty(Cesium.Color.RED.withAlpha(0.3));
        const loopCoords = [...coords];
        if (loopCoords.length > 0 && loopCoords[0]) loopCoords.push(loopCoords[0]);
        const flatPositions = loopCoords.flatMap(c => c ? [c[0], c[1], zone.maxAltitude * FEET_TO_METERS] : []) as number[];
        const wallRimPositions = Cesium.Cartesian3.fromDegreesArrayHeights(flatPositions);

        const entity = currentViewer.entities.add({
            id: zone.id,
            name: zone.name,
            polygon: {
                hierarchy: positions,
                height: zone.minAltitude * FEET_TO_METERS,
                extrudedHeight: zone.maxAltitude * FEET_TO_METERS,
                material: zoneMaterial,
                outline: true, 
                outlineColor: Cesium.Color.RED.withAlpha(0.7),
                outlineWidth: 1 
            },
            polyline: {
                positions: wallRimPositions, 
                width: 3,
                material: new Cesium.ColorMaterialProperty(Cesium.Color.RED.withAlpha(0.7))
            }
        });

        const center = Cesium.BoundingSphere.fromPoints(positions).center;
        const centerCartographic = Cesium.Cartographic.fromCartesian(center);
        const labelPos = Cesium.Cartesian3.fromRadians(
            centerCartographic.longitude, centerCartographic.latitude, (zone.maxAltitude * FEET_TO_METERS) + 5
        );
        
        registerLabel(zone.id!, zone.name.toUpperCase(), 'zone', () => labelPos);

        entityMap.set(zone.id!, entity);
        return entity;
    };

    const loadNoFlyZones = async () => {
        try {
            const fetchedZones = await c4iService.getAll();
            zones.value = fetchedZones;
            fetchedZones.forEach(createZoneEntity);
        } catch (e) { console.error(e); }
    };

    const loadPoints = async () => {
        try {
            const fetchedPoints = await c4iService.getAllPoints();
            points.value = fetchedPoints;
            fetchedPoints.forEach(createPointEntity);
        } catch (e) { console.error(e); }
    };

    const pickPosition = (windowPosition: Cesium.Cartesian2) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return null;
        const ray = currentViewer.camera.getPickRay(windowPosition);
        if (ray) {
            const position = currentViewer.scene.globe.pick(ray, currentViewer.scene);
            if (position) return position;
        }
        return currentViewer.scene.pickPosition(windowPosition);
    };

    const stopDrawingInteraction = () => {
        if (eventHandler) { eventHandler.destroy(); eventHandler = null; }
        activeDrawingType.value = null;
        if (viewer.value) {
            const controller = viewer.value.scene.screenSpaceCameraController;
            controller.enableRotate = true; controller.enableTranslate = true;
            controller.enableZoom = true; controller.enableTilt = true; controller.enableLook = true;
        }
    };

    const clearDrawingVisuals = () => {
        if (drawingEntity) { viewer.value?.entities.remove(drawingEntity); drawingEntity = null; }
        if (tempMarkers.length > 0) { tempMarkers.forEach(m => viewer.value?.entities.remove(m)); tempMarkers = []; }
    };

    const startDrawing = (type: 'marker' | 'polygon' | 'rectangle') => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;
        stopDrawingInteraction();
        clearDrawingVisuals();
        activeDrawingType.value = type;
        drawingPositions = [];
        const controller = currentViewer.scene.screenSpaceCameraController;
        controller.enableRotate = false; controller.enableTranslate = false;
        controller.enableZoom = false; controller.enableTilt = false; controller.enableLook = false;
        eventHandler = new Cesium.ScreenSpaceEventHandler(currentViewer.scene.canvas);

        if (type === 'rectangle') {
            eventHandler.setInputAction((click: any) => {
                const position = pickPosition(click.position);
                if (!Cesium.defined(position)) return;
                drawingPositions = [position!];
                drawingEntity = currentViewer.entities.add({
                    rectangle: {
                        coordinates: new Cesium.CallbackProperty(() => {
                            if (drawingPositions.length < 2) return undefined; 
                            return Cesium.Rectangle.fromCartesianArray(drawingPositions);
                        }, false),
                        material: Cesium.Color.RED.withAlpha(0.3),
                        outline: false
                    }
                });
            }, Cesium.ScreenSpaceEventType.LEFT_DOWN);
            eventHandler.setInputAction((movement: any) => {
                if (drawingPositions.length > 0 && drawingEntity) {
                    const position = pickPosition(movement.endPosition);
                    if (position) {
                        if (drawingPositions.length === 1) drawingPositions.push(position);
                        else drawingPositions[1] = position;
                    }
                }
            }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);
            eventHandler.setInputAction((click: any) => {
                if (drawingPositions.length >= 2) {
                    const position = pickPosition(click.position);
                    if (position) drawingPositions[1] = position;
                    showNewZoneModal.value = true;
                    lastDrawingPositions = [...drawingPositions];
                    stopDrawingInteraction();
                } else { stopDrawingInteraction(); clearDrawingVisuals(); }
            }, Cesium.ScreenSpaceEventType.LEFT_UP);
            return;
        }

        eventHandler.setInputAction((click: any) => {
            const position = pickPosition(click.position);
            if (!Cesium.defined(position)) return;
            if (type === 'marker') {
                const cartographic = Cesium.Cartographic.fromCartesian(position!);
                const lng = Cesium.Math.toDegrees(cartographic.longitude);
                const lat = Cesium.Math.toDegrees(cartographic.latitude);
                tempMarkerPosition = { lat, lng };
                showPointModal.value = true;
                stopDrawingInteraction();
            } else {
                if (drawingPositions.length > 2) {
                    const startPos = drawingPositions[0];
                    if (startPos) {
                        const startScr = currentViewer.scene.cartesianToCanvasCoordinates(startPos);
                        const endScr = currentViewer.scene.cartesianToCanvasCoordinates(position!);
                        if (startScr && endScr) {
                            if (Cesium.Cartesian2.distance(startScr, endScr) < 20) {
                                showNewZoneModal.value = true;
                                lastDrawingPositions = [...drawingPositions];
                                stopDrawingInteraction();
                                return;
                            }
                        }
                    }
                }
                drawingPositions.push(position!);
                const vertexMarker = currentViewer.entities.add({
                    position: position!,
                    point: { pixelSize: 8, color: Cesium.Color.RED, outlineColor: Cesium.Color.WHITE, outlineWidth: 1, disableDepthTestDistance: Number.POSITIVE_INFINITY }
                });
                tempMarkers.push(vertexMarker);
                if (!drawingEntity) {
                    drawingEntity = currentViewer.entities.add({
                        polygon: {
                            hierarchy: new Cesium.CallbackProperty(() => new Cesium.PolygonHierarchy(drawingPositions), false),
                            material: Cesium.Color.RED.withAlpha(0.3),
                            outline: false
                        }
                    });
                }
            }
        }, Cesium.ScreenSpaceEventType.LEFT_CLICK);

        eventHandler.setInputAction((movement: any) => {
            if (drawingPositions.length > 0 && drawingEntity) {
                const position = pickPosition(movement.endPosition);
                if (position) {
                    const tempPositions = [...drawingPositions, position];
                    // @ts-ignore
                    drawingEntity.polygon.hierarchy = new Cesium.PolygonHierarchy(tempPositions);
                }
            }
        }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

        eventHandler.setInputAction(() => {
            if (type === 'polygon' && drawingPositions.length > 2) {
                showNewZoneModal.value = true;
                lastDrawingPositions = [...drawingPositions];
                stopDrawingInteraction();
            }
        }, Cesium.ScreenSpaceEventType.RIGHT_CLICK);
    };

    let lastDrawingPositions: Cesium.Cartesian3[] = [];
    let tempMarkerPosition: { lat: number, lng: number } | null = null;

    window.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && activeDrawingType.value) {
            stopDrawingInteraction(); clearDrawingVisuals();
        }
    });

    const handleSavePoint = async () => {
        if (!tempMarkerPosition) return;
        const pointData: Point = {
            id: newPointForm.value.id,
            name: newPointForm.value.name,
            type: newPointForm.value.type,
            location: { type: 'Point', coordinates: [tempMarkerPosition.lng, tempMarkerPosition.lat] }
        };
        try {
            const saved = await c4iService.createPoint(pointData);
            points.value.push(saved);
            createPointEntity(saved);
            showPointModal.value = false;
            tempMarkerPosition = null;
            clearDrawingVisuals();
        } catch (e) { console.error(e); }
    };

    const handleSaveZone = async () => {
        if (lastDrawingPositions.length < 2) return;
        let coords: number[][] = [];
        if (lastDrawingPositions.length === 2) {
            const rect = Cesium.Rectangle.fromCartesianArray(lastDrawingPositions);
            const w = Cesium.Math.toDegrees(rect.west);
            const s = Cesium.Math.toDegrees(rect.south);
            const e = Cesium.Math.toDegrees(rect.east);
            const n = Cesium.Math.toDegrees(rect.north);
            coords = [[w, n], [e, n], [e, s], [w, s], [w, n]];
        } else {
            coords = lastDrawingPositions.map(p => {
                const c = Cesium.Cartographic.fromCartesian(p);
                return [Cesium.Math.toDegrees(c.longitude), Cesium.Math.toDegrees(c.latitude)];
            });
            if (coords.length > 0) coords.push(coords[0] as number[]);
        }
        const zoneData: NoFlyZone = {
            name: newZoneForm.value.name,
            minAltitude: newZoneForm.value.minAltitude,
            maxAltitude: newZoneForm.value.maxAltitude,
            isActive: true,
            geometry: { type: 'Polygon', coordinates: [coords] }
        };
        try {
            const saved = await c4iService.create(zoneData);
            zones.value.push(saved);
            createZoneEntity(saved);
            showNewZoneModal.value = false;
            lastDrawingPositions = [];
            clearDrawingVisuals();
        } catch (e) { console.error(e); }
    };

    const handleCancelPoint = () => { showPointModal.value = false; tempMarkerPosition = null; clearDrawingVisuals(); };
    const handleCancelZone = () => { showNewZoneModal.value = false; lastDrawingPositions = []; clearDrawingVisuals(); };

    const deleteEntity = async (id: string, isPoint: boolean) => {
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
            unregisterLabel(id);
        }
    };

    const initializeRealtimeUpdates = () => {
        signalRService.onEntityUpdate((update) => {
            if (update.entityType === 'Point') {
                if (update.changeType === 'Created') createPointEntity(update.data);
                else if (update.changeType === 'Deleted') {
                    const id = update.data.id || update.data.Id;
                    const entity = entityMap.get(id);
                    if (entity) { viewer.value?.entities.remove(entity); entityMap.delete(id); unregisterLabel(id); }
                }
            } else if (update.entityType === 'NoFlyZone') {
                if (update.changeType === 'Created') createZoneEntity(update.data);
                else if (update.changeType === 'Deleted') {
                    const id = update.data.id || update.data.Id;
                    const entity = entityMap.get(id);
                    if (entity) { viewer.value?.entities.remove(entity); entityMap.delete(id); unregisterLabel(id); }
                }
            }
        });
    };

    return {
        zones, points, 
        showNewZoneModal, newZoneForm, showPointModal, newPointForm, isEditing,
        loadNoFlyZones, loadPoints, startDrawing, handleSavePoint, handleSaveZone, handleCancelPoint, handleCancelZone, deleteEntity, initializeRealtimeUpdates,
        toggleEditMode: () => { isEditing.value = !isEditing.value; },
        saveEdits: () => { isEditing.value = false; },
        cancelEdits: () => { isEditing.value = false; },
        handleDeleteZone: async () => {
            if (newZoneForm.value.id) { await deleteEntity(newZoneForm.value.id, false); showNewZoneModal.value = false; }
        }
    };
}