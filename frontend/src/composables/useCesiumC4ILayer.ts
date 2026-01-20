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

    const selectedEntityId = ref<string | null>(null);
    let vertexHandles: Cesium.Entity[] = [];

    const clearVertexHandles = () => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;
        vertexHandles.forEach(h => currentViewer.entities.remove(h));
        vertexHandles = [];
    };

    const showVertexHandles = (zone: NoFlyZone) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;
        clearVertexHandles();

        const coords = zone.geometry.coordinates[0];
        // Skip last point if it's a duplicate of the first (closed loop)
        const pointCount = coords.length > 1 && 
            coords[0][0] === coords[coords.length-1][0] && 
            coords[0][1] === coords[coords.length-1][1] 
            ? coords.length - 1 : coords.length;

        for (let i = 0; i < pointCount; i++) {
            const handle = currentViewer.entities.add({
                position: new Cesium.CallbackProperty(() => {
                    return Cesium.Cartesian3.fromDegrees(coords[i][0], coords[i][1], zone.maxAltitude * FEET_TO_METERS);
                }, false) as any,
                point: {
                    pixelSize: 10,
                    color: Cesium.Color.YELLOW,
                    outlineColor: Cesium.Color.BLACK,
                    outlineWidth: 2,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY,
                    heightReference: Cesium.HeightReference.NONE
                },
                properties: { isVertexHandle: true, zoneId: zone.id, vertexIndex: i }
            });
            vertexHandles.push(handle);
        }
    };

    const createPointEntity = (point: Point) => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        try {
            const isHome = (point.type === PointType.Home || (point as any).type === 0);
            
            let cachedHeight = currentViewer.scene.globe.getHeight(Cesium.Cartographic.fromDegrees(point.location.coordinates[0], point.location.coordinates[1])) || 0;
            const carto = Cesium.Cartographic.fromDegrees(point.location.coordinates[0], point.location.coordinates[1]);
            Cesium.sampleTerrainMostDetailed(currentViewer.terrainProvider, [carto]).then((samples) => {
                if (samples && samples[0] && samples[0].height !== undefined) {
                    cachedHeight = samples[0].height;
                }
            });

            if (entityMap.has(point.id!)) {
                currentViewer.entities.remove(entityMap.get(point.id!)!);
                unregisterLabel(point.id!);            }

            const position = new Cesium.CallbackProperty(() => {
                return Cesium.Cartesian3.fromDegrees(point.location.coordinates[0], point.location.coordinates[1], 0);
            }, false);

            // Create Unified Billboard Entity (Icon - High Visibility)
            const entity = currentViewer.entities.add({
                id: point.id,
                position: position as any,
                billboard: {
                    image: isHome ? '/antena.png' : '/target.Png',
                    scale: 0.32, 
                    verticalOrigin: isHome ? Cesium.VerticalOrigin.BOTTOM : Cesium.VerticalOrigin.CENTER,
                    horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
                    // FIRMLY ATTACHED TO GROUND
                    heightReference: Cesium.HeightReference.CLAMP_TO_GROUND,
                    disableDepthTestDistance: Number.POSITIVE_INFINITY,
                    eyeOffset: new Cesium.Cartesian3(0, 0, 100.0),
                    scaleByDistance: new Cesium.NearFarScalar(1.0e3, 0.32, 1.0e7, 0.15)
                },
                properties: { isPoint: true, type: point.type }
            });

            const yOffsetBase = isHome ? 60 : 30; // Closer connection for the target icon
            registerLabel(point.id!, point.name.toUpperCase(), isHome ? 'home' : 'target', () => {
                return Cesium.Cartesian3.fromDegrees(point.location.coordinates[0], point.location.coordinates[1], cachedHeight);
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

        if (entityMap.has(zone.id!)) {
            currentViewer.entities.remove(entityMap.get(zone.id!)!);
            unregisterLabel(zone.id!);
        }

        const entity = currentViewer.entities.add({
            id: zone.id,
            name: zone.name,
            polygon: {
                hierarchy: new Cesium.CallbackProperty(() => {
                    return new Cesium.PolygonHierarchy(Cesium.Cartesian3.fromDegreesArray(zone.geometry.coordinates[0].flat()));
                }, false) as any,
                height: zone.minAltitude * FEET_TO_METERS,
                extrudedHeight: zone.maxAltitude * FEET_TO_METERS,
                material: new Cesium.StripeMaterialProperty({
                    evenColor: Cesium.Color.RED.withAlpha(0.25),
                    oddColor: Cesium.Color.RED.withAlpha(0.1),
                    repeat: 80,
                    offset: new Cesium.CallbackProperty(() => (performance.now() / 30000) % 1, false),
                    orientation: Cesium.StripeOrientation.VERTICAL
                }),
                outline: true,
                outlineColor: Cesium.Color.RED.withAlpha(0.2),
                outlineWidth: 1
            }
        });

        registerLabel(zone.id!, zone.name.toUpperCase(), 'zone', () => {
            const currentPositions = Cesium.Cartesian3.fromDegreesArray(zone.geometry.coordinates[0].flat());
            if (currentPositions.length === 0) return undefined;
            const currentCenter = Cesium.BoundingSphere.fromPoints(currentPositions).center;
            const currentCarto = Cesium.Cartographic.fromCartesian(currentCenter);
            return Cesium.Cartesian3.fromRadians(
                currentCarto.longitude, currentCarto.latitude, (zone.maxAltitude * FEET_TO_METERS) + 5
            );
        });

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

    const toggleEditMode = () => {
        isEditing.value = !isEditing.value;
        if (isEditing.value) {
            enableEditInteraction();
        } else {
            disableEditInteraction();
        }
    };

    const enableEditInteraction = () => {
        const currentViewer = viewer.value;
        if (!currentViewer) return;

        if (eventHandler) eventHandler.destroy();
        eventHandler = new Cesium.ScreenSpaceEventHandler(currentViewer.scene.canvas);

        let draggedEntity: Cesium.Entity | null = null;
        let isDragging = false;
        let draggedVertexIndex: number | null = null;

        eventHandler.setInputAction((click: any) => {
            const pickedObject = currentViewer.scene.pick(click.position);
            if (Cesium.defined(pickedObject) && pickedObject.id) {
                const entity = pickedObject.id;
                
                // 1. Prioritize Vertex Handle
                if (entity.properties && entity.properties.isVertexHandle) {
                    draggedEntity = entity;
                    draggedVertexIndex = entity.properties.vertexIndex.getValue();
                    isDragging = true;
                    currentViewer.scene.screenSpaceCameraController.enableInputs = false;
                    return;
                }

                // 2. Entity selection/drag
                draggedEntity = entity;
                isDragging = true;
                currentViewer.scene.screenSpaceCameraController.enableInputs = false;

                // Toggle handles based on selection
                if (entity.id !== selectedEntityId.value) {
                    selectedEntityId.value = entity.id;
                    const zone = zones.value.find(z => z.id === entity.id);
                    if (zone) showVertexHandles(zone);
                    else clearVertexHandles();
                }
            } else {
                // Clicked empty space
                selectedEntityId.value = null;
                clearVertexHandles();
            }
        }, Cesium.ScreenSpaceEventType.LEFT_DOWN);

        eventHandler.setInputAction((movement: any) => {
            if (isDragging && draggedEntity) {
                const newPos = pickPosition(movement.endPosition);
                if (newPos) {
                    const cartographic = Cesium.Cartographic.fromCartesian(newPos);
                    const lng = Cesium.Math.toDegrees(cartographic.longitude);
                    const lat = Cesium.Math.toDegrees(cartographic.latitude);

                    if (draggedVertexIndex !== null && draggedEntity.properties) {
                        const zoneId = draggedEntity.properties.zoneId.getValue();
                        const zone = zones.value.find(z => z.id === zoneId);
                        if (zone) {
                            const coords = zone.geometry.coordinates[0];
                            // Mutate array directly so CallbackProperty sees it
                            if (coords[draggedVertexIndex]) {
                                coords[draggedVertexIndex][0] = lng;
                                coords[draggedVertexIndex][1] = lat;
                            }
                            
                            // Keep loop closed
                            if (draggedVertexIndex === 0 && coords.length > 1) {
                                if (coords[coords.length-1]) {
                                    coords[coords.length-1][0] = lng;
                                    coords[coords.length-1][1] = lat;
                                }
                            }
                        }
                    } else if (draggedEntity.properties && draggedEntity.properties.isPoint) {
                        const point = points.value.find(p => p.id === draggedEntity!.id);
                        if (point) {
                            // Mutate array directly
                            point.location.coordinates[0] = lng;
                            point.location.coordinates[1] = lat;
                        }
                    } else if (draggedEntity.id) {
                        // Handle Zone movement (Shift all coordinates)
                        const zone = zones.value.find(z => z.id === draggedEntity!.id);
                        if (zone) {
                            const currentPositions = Cesium.Cartesian3.fromDegreesArray(zone.geometry.coordinates[0].flat());
                            const center = Cesium.BoundingSphere.fromPoints(currentPositions).center;
                            const centerCarto = Cesium.Cartographic.fromCartesian(center);
                            
                            const dLng = lng - Cesium.Math.toDegrees(centerCarto.longitude);
                            const dLat = lat - Cesium.Math.toDegrees(centerCarto.latitude);

                            zone.geometry.coordinates[0].forEach((c: number[]) => {
                                if (c && c.length >= 2 && typeof c[0] === 'number' && typeof c[1] === 'number') {
                                    c[0] += dLng;
                                    c[1] += dLat;
                                }
                            });
                        }
                    }
                }
            }
        }, Cesium.ScreenSpaceEventType.MOUSE_MOVE);

        eventHandler.setInputAction(async () => {
            if (isDragging && draggedEntity) {
                const entityId = draggedEntity.id;
                if (draggedVertexIndex !== null && draggedEntity.properties) {
                    const zoneId = draggedEntity.properties.zoneId.getValue();
                    const zone = zones.value.find(z => z.id === zoneId);
                    if (zone) await c4iService.update(zoneId, zone);
                } else if (draggedEntity.properties && draggedEntity.properties.isPoint) {
                    const point = points.value.find(p => p.id === entityId);
                    if (point) await c4iService.updatePoint(entityId, point);
                } else {
                    const zone = zones.value.find(z => z.id === entityId);
                    if (zone) await c4iService.update(entityId, zone);
                }
            }
            isDragging = false;
            draggedEntity = null;
            draggedVertexIndex = null;
            currentViewer.scene.screenSpaceCameraController.enableInputs = true; // Unlock camera
        }, Cesium.ScreenSpaceEventType.LEFT_UP);

        // Metadata Edit on Double Click
        eventHandler.setInputAction((click: any) => {
            const pickedObject = currentViewer.scene.pick(click.position);
            if (Cesium.defined(pickedObject) && pickedObject.id) {
                const entity = pickedObject.id;
                const isPoint = entity.properties && entity.properties.isPoint;
                
                if (isPoint) {
                    const point = points.value.find(p => p.id === entity.id);
                    if (point) {
                        newPointForm.value = { id: point.id, name: point.name, type: point.type };
                        tempMarkerPosition = { lng: point.location.coordinates[0], lat: point.location.coordinates[1] };
                        showPointModal.value = true;
                    }
                } else {
                    const zone = zones.value.find(z => z.id === entity.id);
                    if (zone) {
                        newZoneForm.value = { id: zone.id, name: zone.name, minAltitude: zone.minAltitude, maxAltitude: zone.maxAltitude };
                        showNewZoneModal.value = true;
                    }
                }
            }
        }, Cesium.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
    };

    const disableEditInteraction = () => {
        stopDrawingInteraction();
    };

    const handleSavePoint = async () => {
        if (!tempMarkerPosition) return;
        const pointData: Point = {
            id: newPointForm.value.id,
            name: newPointForm.value.name,
            type: newPointForm.value.type,
            location: { type: 'Point', coordinates: [tempMarkerPosition.lng, tempMarkerPosition.lat] }
        };
        try {
            if (pointData.id) {
                // UPDATE
                await c4iService.updatePoint(pointData.id, pointData);
                const idx = points.value.findIndex(p => p.id === pointData.id);
                if (idx !== -1) points.value[idx] = pointData;
                createPointEntity(pointData); // Refresh visual
            } else {
                // CREATE
                const saved = await c4iService.createPoint(pointData);
                points.value.push(saved);
                createPointEntity(saved);
            }
            showPointModal.value = false;
            tempMarkerPosition = null;
            clearDrawingVisuals();
        } catch (e) { console.error(e); }
    };

    const handleSaveZone = async () => {
        const isUpdate = !!newZoneForm.value.id;
        
        let coords: number[][] = [];
        if (!isUpdate) {
            if (lastDrawingPositions.length < 2) return;
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
        } else {
            // Keep existing geometry on metadata update
            const existingZone = zones.value.find(z => z.id === newZoneForm.value.id);
            if (existingZone) coords = existingZone.geometry.coordinates[0];
        }

        const zoneData: NoFlyZone = {
            id: newZoneForm.value.id,
            name: newZoneForm.value.name,
            minAltitude: newZoneForm.value.minAltitude,
            maxAltitude: newZoneForm.value.maxAltitude,
            isActive: true,
            geometry: { type: 'Polygon', coordinates: [coords] }
        };

        try {
            if (isUpdate) {
                // UPDATE
                await c4iService.update(zoneData.id!, zoneData);
                const idx = zones.value.findIndex(z => z.id === zoneData.id);
                if (idx !== -1) zones.value[idx] = zoneData;
                createZoneEntity(zoneData); // Refresh visual
            } else {
                // CREATE
                const saved = await c4iService.create(zoneData);
                zones.value.push(saved);
                createZoneEntity(saved);
            }
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
        toggleEditMode,
        saveEdits: () => { 
            isEditing.value = false; 
            selectedEntityId.value = null;
            clearVertexHandles();
            disableEditInteraction(); 
        },
        cancelEdits: () => { 
            isEditing.value = false; 
            selectedEntityId.value = null;
            clearVertexHandles();
            disableEditInteraction(); 
        },
        handleDeleteZone: async () => {
            if (newZoneForm.value.id) { await deleteEntity(newZoneForm.value.id, false); showNewZoneModal.value = false; }
        }
    };
}