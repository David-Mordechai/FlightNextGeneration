import { ref, shallowRef } from 'vue';
import L from 'leaflet';
import { c4iService, type NoFlyZone, type Point, PointType } from '../services/C4IService';
import { signalRService } from '../services/SignalRService';

export function useC4ILayer(map: any) {
    const drawnItems = new L.FeatureGroup();
    
    // Data State
    const zones = ref<NoFlyZone[]>([]);
    const points = ref<Point[]>([]);

    // No Fly Zone State
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

    // Point State
    const showPointModal = ref(false);
    const newPointForm = ref<{
        id?: string;
        name: string;
        type: PointType;
    }>({
        name: '',
        type: PointType.Home
    });

    const isEditingGeometry = ref(false);
    const isEditing = ref(false);
    const tempLayer = shallowRef<L.Layer | null>(null);
    let drawControlInstance: any = null;

    const initializeDrawControls = (leafletMap: L.Map) => {
        leafletMap.addLayer(drawnItems);
        
        // @ts-ignore
        const drawControl = new L.Control.Draw({
            draw: {
                polyline: false,
                circle: false,
                marker: {},
                circlemarker: false,
                polygon: {
                    allowIntersection: false,
                    showArea: false
                },
                rectangle: {
                    showArea: false,
                    shapeOptions: {}
                }
            },
            edit: {
                featureGroup: drawnItems,
                remove: true
            }
        });
        leafletMap.addControl(drawControl);
        drawControlInstance = drawControl;

        // --- Event Listeners ---

        // 1. Created
        leafletMap.on(L.Draw.Event.CREATED, (e: any) => {
            const layer = e.layer;
            const type = e.layerType;
            
            tempLayer.value = layer;
            leafletMap.addLayer(layer);

            if (type === 'marker') {
                // Point Created
                newPointForm.value = {
                    id: undefined,
                    name: '',
                    type: PointType.Home
                };
                showPointModal.value = true;
            } else {
                // No Fly Zone Created
                newZoneForm.value = {
                    id: undefined,
                    name: '',
                    minAltitude: 0,
                    maxAltitude: 10000
                };
                showNewZoneModal.value = true;
            }
        });

        // 2. Edit Start/Stop (Geometry Mode)
        leafletMap.on('draw:editstart', () => {
            isEditingGeometry.value = true;
            leafletMap.closePopup(); 
        });

        leafletMap.on('draw:editstop', () => {
            isEditingGeometry.value = false;
        });

        // 3. Edited (Geometry changes saved via toolbar)
        leafletMap.on(L.Draw.Event.EDITED, async (e: any) => {
            const layers = e.layers;
            layers.eachLayer(async (layer: any) => {
                const id = (layer as any).feature?.properties?.id;
                // Currently only supporting geometry updates for NoFlyZones via this event
                // Points are just points, moving them is supported but we need to check the type
                if (id) {
                     // Determine if it's a zone or a point based on geometry type
                     const geoJson = layer.toGeoJSON();
                     if (geoJson.geometry.type === 'Point') {
                        // It's a Point - Handle Drag Update
                        const latLng = layer.getLatLng();
                        const currentPoint = points.value.find(p => p.id === id);
                        
                        if (currentPoint) {
                            const updatedPoint: Point = {
                                ...currentPoint,
                                location: {
                                    type: 'Point',
                                    coordinates: [latLng.lng, latLng.lat]
                                }
                            };

                            try {
                                await c4iService.updatePoint(id, updatedPoint);
                                // Update local state
                                const idx = points.value.findIndex(p => p.id === id);
                                if (idx !== -1) points.value[idx] = updatedPoint;
                                console.log(`Point ${id} updated location`);
                            } catch (err) {
                                console.error(`Failed to update point ${id}`, err);
                                // Revert position on error? (Optional, but complex to implement without reload)
                                alert(`Failed to move point: ${currentPoint.name}`);
                            }
                        }
                     } else {
                        // It's a NoFlyZone
                        const updatedZone: NoFlyZone = {
                            id: id,
                            name: (layer as any).feature?.properties?.name || 'Updated Zone',
                            minAltitude: (layer as any).feature?.properties?.minAltitude || 0,
                            maxAltitude: (layer as any).feature?.properties?.maxAltitude || 10000,
                            isActive: true,
                            geometry: geoJson.geometry
                        };
                        
                        try {
                            await c4iService.update(id, updatedZone);
                            console.log(`Zone ${id} updated geometry`);
                        } catch (err) {
                            console.error(`Failed to update zone ${id}`, err);
                        }
                     }
                }
            });
        });

        // 4. Deleted (Via toolbar)
        leafletMap.on(L.Draw.Event.DELETED, async (e: any) => {
            const layers = e.layers;
            layers.eachLayer(async (layer: any) => {
                const id = (layer as any).feature?.properties?.id;
                const isPoint = (layer as any).feature?.properties?.isPoint;
                
                if (id) {
                    try {
                        if (isPoint) {
                            await c4iService.deletePoint(id);
                            console.log(`Point ${id} deleted`);
                        } else {
                            await c4iService.delete(id);
                            console.log(`Zone ${id} deleted`);
                        }
                    } catch (err) {
                        console.error(`Failed to delete entity ${id}`, err);
                    }
                }
            });
        });
    };

    const createMarkerFromPoint = (point: Point) => {
        const lat = point.location.coordinates[1];
        const lng = point.location.coordinates[0];
        
        let customIcon;

        if (point.type === PointType.Home) {
            // Home Icon (House/Base)
            const homeSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/></svg>`;
            
            customIcon = L.divIcon({
                className: 'home-marker',
                html: `
                    <div class="home-marker-container">
                        <div class="home-marker-ring"></div>
                        <div class="home-marker-icon">${homeSvg}</div>
                    </div>
                `,
                iconSize: [40, 40],
                iconAnchor: [20, 20],
                popupAnchor: [0, -20]
            });
        } else {
            // Target Icon (Crosshair)
            const targetSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9v-2h2v-2h2v2h2v2h-2v2h-2v-2z"/></svg>`;
            // Alternative Target: Simple Dot in Circle
            const simpleTargetSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8"/></svg>`;

            customIcon = L.divIcon({
                className: 'target-marker',
                html: `
                    <div class="target-marker-container">
                        <div class="target-ring-outer"></div>
                        <div class="target-ring-inner"></div>
                        <div class="target-marker-icon">${simpleTargetSvg}</div>
                    </div>
                `,
                iconSize: [40, 40],
                iconAnchor: [20, 20],
                popupAnchor: [0, -20]
            });
        }

        const marker = L.marker([lat, lng], { 
            icon: customIcon,
            zIndexOffset: 500 // Lower priority than UAV
        });

        // @ts-ignore
        marker.feature = {
            type: 'Feature',
            properties: {
                id: point.id,
                name: point.name,
                type: point.type,
                isPoint: true
            }
        };

        marker.bindPopup(`<strong>${point.name}</strong><br>${point.type === PointType.Home ? 'Home' : 'Target'}`);
        marker.bindTooltip(point.name, { 
            permanent: true, 
            direction: 'top', 
            offset: [0, -20],
            className: 'entity-label',
            pane: 'shadowPane' // Render below markers
        });

        marker.on('click', () => {
                if (!isEditingGeometry.value) return;

                tempLayer.value = marker;
                newPointForm.value = {
                    id: point.id,
                    name: point.name,
                    type: point.type
                };
                showPointModal.value = true;
        });
        
        return marker;
    };

    const loadNoFlyZones = async () => {
        try {
            const fetchedZones = await c4iService.getAll();
            zones.value = fetchedZones;
            fetchedZones.forEach(zone => {
                if (zone.geometry) {
                    const layer = L.geoJSON(zone.geometry, {
                        style: { 
                            color: '#ef4444', 
                            weight: 2, 
                            fillOpacity: 0.1,
                            className: 'neon-zone-path'
                        }
                    });
                    
                    addNonGroupLayers(layer, {
                        id: zone.id,
                        name: zone.name,
                        minAltitude: zone.minAltitude,
                        maxAltitude: zone.maxAltitude,
                        isPoint: false
                    });
                }
            });
        } catch (e) {
            console.error("Failed to load No Fly Zones", e);
        }
    };
    
    const loadPoints = async () => {
        try {
            const fetchedPoints = await c4iService.getAllPoints();
            points.value = fetchedPoints;
            fetchedPoints.forEach(point => {
                if (point.location) {
                    const layer = createMarkerFromPoint(point);
                    drawnItems.addLayer(layer);
                }
            });
        } catch (e) {
            console.error("Failed to load Points", e);
        }
    };

    // Helper to unwrap GeoJSON layers
    const addNonGroupLayers = (sourceLayer: any, properties: any) => {
        if (sourceLayer instanceof L.LayerGroup) {
            sourceLayer.eachLayer((childLayer: any) => {
                addNonGroupLayers(childLayer, properties);
            });
        } else {
            sourceLayer.feature = sourceLayer.feature || {};
            sourceLayer.feature.type = 'Feature';
            sourceLayer.feature.properties = properties;
            
            sourceLayer.bindPopup(`<strong>${properties.name}</strong><br>Alt: ${properties.minAltitude}-${properties.maxAltitude}ft`);
            sourceLayer.bindTooltip(properties.name, { 
                permanent: true, 
                direction: 'top', 
                offset: [0, -20],
                className: 'entity-label',
                pane: 'shadowPane' // Render below markers
            });
            
            // Edit Click Listener
            sourceLayer.on('click', () => {
                if (!isEditingGeometry.value) return; 

                tempLayer.value = sourceLayer; 
                newZoneForm.value = {
                    id: properties.id,
                    name: properties.name,
                    minAltitude: properties.minAltitude,
                    maxAltitude: properties.maxAltitude
                };
                showNewZoneModal.value = true;
            });

            drawnItems.addLayer(sourceLayer);
        }
    };

    // --- No Fly Zone Handlers ---

    const handleSaveZone = async () => {
        if (!tempLayer.value) {
            console.error("handleSaveZone: tempLayer is null");
            return;
        }

        const layer = tempLayer.value as any;
        let geoJson;
        try {
            geoJson = layer.toGeoJSON();
        } catch (err) {
            console.error("Error converting layer to GeoJSON:", err);
            alert("Error processing shape data.");
            return;
        }

        const geometry = geoJson.type === 'Feature' ? geoJson.geometry : geoJson;

        const zoneData: NoFlyZone = {
            id: newZoneForm.value.id,
            name: newZoneForm.value.name,
            minAltitude: newZoneForm.value.minAltitude,
            maxAltitude: newZoneForm.value.maxAltitude,
            isActive: true,
            geometry: geometry
        };

        try {
            if (newZoneForm.value.id) {
                // UPDATE
                await c4iService.update(newZoneForm.value.id, zoneData);
                
                // Update local state
                const idx = zones.value.findIndex(z => z.id === newZoneForm.value.id);
                if (idx !== -1) zones.value[idx] = { ...zoneData, id: newZoneForm.value.id }; // ensure ID is set

                // Update visuals
                if (layer.feature && layer.feature.properties) {
                    layer.feature.properties.name = zoneData.name;
                    layer.feature.properties.minAltitude = zoneData.minAltitude;
                    layer.feature.properties.maxAltitude = zoneData.maxAltitude;
                    
                    const newContent = `<strong>${zoneData.name}</strong><br>Alt: ${zoneData.minAltitude}-${zoneData.maxAltitude}ft`;
                    layer.setPopupContent(newContent);
                    layer.setTooltipContent(zoneData.name);
                }
                
                 if (drawControlInstance && isEditingGeometry.value) {
                    // @ts-ignore
                    drawControlInstance._toolbars.edit.disable(); 
                }
            } else {
                // CREATE
                const saved = await c4iService.create(zoneData);
                zones.value.push(saved);

                const savedLayer = L.geoJSON(saved.geometry, {
                    style: { 
                        color: '#ef4444', 
                        weight: 2, 
                        fillOpacity: 0.1,
                        className: 'neon-zone-path'
                    }
                });
                
                addNonGroupLayers(savedLayer, {
                    id: saved.id,
                    name: saved.name,
                    minAltitude: saved.minAltitude,
                    maxAltitude: saved.maxAltitude,
                    isPoint: false
                });
                
                if(map.value) map.value.removeLayer(layer);
            }

            showNewZoneModal.value = false;
            tempLayer.value = null;
            setTimeout(() => { if (map.value) map.value.invalidateSize(); }, 100);
        } catch (e) {
            console.error("Save Zone Error Details:", e);
            alert('Failed to save zone: ' + (e instanceof Error ? e.message : String(e)));
        }
    };

    const handleCancelZone = () => {
        if (!newZoneForm.value.id && tempLayer.value && map.value) {
            map.value.removeLayer(tempLayer.value as L.Layer);
        }
        showNewZoneModal.value = false;
        tempLayer.value = null;
        newZoneForm.value.id = undefined;
        setTimeout(() => { if (map.value) map.value.invalidateSize(); }, 100);
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
            
            // Remove from Map
            drawnItems.eachLayer((layer: any) => {
                if (layer.feature?.properties?.id === id) {
                    drawnItems.removeLayer(layer);
                }
            });
            console.log(`Deleted entity ${id}`);
        } catch (e) {
            console.error("Failed to delete entity", e);
            alert("Failed to delete entity");
        }
    };

    const handleDeleteZone = async () => {
        if (!newZoneForm.value.id || !tempLayer.value) return;
        await deleteEntity(newZoneForm.value.id, false);
        showNewZoneModal.value = false;
        tempLayer.value = null;
    };

    const startDrawing = (type: 'marker' | 'polygon' | 'rectangle') => {
        console.log(`Starting drawing mode: ${type}`);
        if (!map.value) return;
        
        // @ts-ignore
        let handler;
        if (type === 'marker') {
             // @ts-ignore
            handler = new L.Draw.Marker(map.value);
        } else if (type === 'polygon') {
             // @ts-ignore
            handler = new L.Draw.Polygon(map.value, {
                allowIntersection: false,
                showArea: false,
                shapeOptions: { color: '#ff0000' }
            });
        } else if (type === 'rectangle') {
             // @ts-ignore
            handler = new L.Draw.Rectangle(map.value, {
                showArea: false,
                shapeOptions: { color: '#ff0000' }
            });
        }

        if (handler) {
            handler.enable();
        }
    };

    const toggleEditMode = () => {
        if (!drawControlInstance) {
            console.error("Draw control not initialized");
            return;
        }
        
        // @ts-ignore
        const editToolbar = drawControlInstance._toolbars.edit;
        const editHandler = editToolbar._modes.edit.handler;
        
        if (!editHandler) return;

        if (editHandler.enabled()) {
            editHandler.disable();
            isEditing.value = false;
            isEditingGeometry.value = false;
        } else {
            editHandler.enable();
            isEditing.value = true;
            isEditingGeometry.value = true;
            console.log("Edit mode enabled. Click entities to edit metadata or drag vertices to edit shape.");
        }
    };

    const saveEdits = () => {
        console.log("Saving changes...");
        if (!drawControlInstance) return;
        // @ts-ignore
        const editToolbar = drawControlInstance._toolbars.edit;
        
        try {
            // Leaflet.Draw internal save triggers EDITED event
            if (typeof editToolbar.save === 'function') editToolbar.save();
            else if (typeof editToolbar._save === 'function') editToolbar._save();
            
            // Ensure handler is disabled
            editToolbar._modes.edit.handler.disable();
        } catch (e) {
            console.error("Error during save:", e);
        }
        
        isEditing.value = false;
        isEditingGeometry.value = false;
    };

    const cancelEdits = () => {
        console.log("Cancelling changes...");
        if (!drawControlInstance) return;
        // @ts-ignore
        const editToolbar = drawControlInstance._toolbars.edit;
        
        try {
            if (typeof editToolbar.revert === 'function') editToolbar.revert();
            else if (typeof editToolbar._revert === 'function') editToolbar._revert();
            
            editToolbar._modes.edit.handler.disable();
        } catch (e) {
            console.error("Error during revert:", e);
        }
        
        isEditing.value = false;
        isEditingGeometry.value = false;
    };


    // --- Point Handlers ---

    const handleSavePoint = async () => {
        if (!tempLayer.value) return;

        const layer = tempLayer.value as L.Marker;
        const latLng = layer.getLatLng();

        const pointData: Point = {
            id: newPointForm.value.id,
            name: newPointForm.value.name,
            type: newPointForm.value.type,
            location: {
                type: 'Point',
                coordinates: [latLng.lng, latLng.lat]
            }
        };

        try {
            let savedPoint: Point;

            if (newPointForm.value.id) {
                // UPDATE
                await c4iService.updatePoint(newPointForm.value.id, pointData);
                savedPoint = pointData; 

                // Update Local State
                const idx = points.value.findIndex(p => p.id === pointData.id);
                if (idx !== -1) points.value[idx] = savedPoint;

                // Safely remove old marker
                try {
                    // @ts-ignore
                    if (layer.editing && layer.editing.enabled()) {
                        // @ts-ignore
                        layer.editing.disable();
                    }
                    drawnItems.removeLayer(layer);
                } catch (cleanupError) {
                    // Ignored: Leaflet Draw internal error during cleanup
                }
            } else {
                // CREATE
                savedPoint = await c4iService.createPoint(pointData);
                points.value.push(savedPoint);
                
                // Remove temporary drawing layer (blue pin)
                if (map.value) map.value.removeLayer(layer);
            }

            // Create and Add the new/updated marker
            const newMarker = createMarkerFromPoint(savedPoint);
            drawnItems.addLayer(newMarker);
            
            showPointModal.value = false;
            tempLayer.value = null;

            // Force map update
            setTimeout(() => {
                if (map.value) map.value.invalidateSize();
            }, 100);

        } catch (e) {
            alert('Failed to save point');
            console.error(e);
        }
    };

    const handleCancelPoint = () => {
        if (tempLayer.value && map.value) {
            map.value.removeLayer(tempLayer.value);
        }
        showPointModal.value = false;
        tempLayer.value = null;
        setTimeout(() => { if (map.value) map.value.invalidateSize(); }, 100);
    };

    const handleEntityUpdate = (update: { entityType: string; changeType: string; data: any }) => {
        console.log("Received Entity Update:", update);
        
        if (update.entityType === 'Point') {
            if (update.changeType === 'Created') {
                const newPoint = update.data as Point;
                points.value.push(newPoint);
                if (newPoint.location) {
                    const layer = createMarkerFromPoint(newPoint);
                    drawnItems.addLayer(layer);
                }
            } else if (update.changeType === 'Deleted') {
                const id = update.data.id || update.data.Id;
                points.value = points.value.filter(p => p.id !== id);
                drawnItems.eachLayer((layer: any) => {
                    if (layer.feature?.properties?.id === id) {
                        drawnItems.removeLayer(layer);
                    }
                });
            }
        } else if (update.entityType === 'NoFlyZone') {
             if (update.changeType === 'Created') {
                const newZone = update.data as NoFlyZone;
                zones.value.push(newZone);
                if (newZone.geometry) {
                    const layer = L.geoJSON(newZone.geometry, {
                        style: { color: '#ff0000', weight: 2, fillOpacity: 0.2 }
                    });
                    addNonGroupLayers(layer, {
                        id: newZone.id,
                        name: newZone.name,
                        minAltitude: newZone.minAltitude,
                        maxAltitude: newZone.maxAltitude,
                        isPoint: false
                    });
                }
            } else if (update.changeType === 'Deleted') {
                const id = update.data.id || update.data.Id;
                zones.value = zones.value.filter(z => z.id !== id);
                drawnItems.eachLayer((layer: any) => {
                    if (layer.feature?.properties?.id === id) {
                        drawnItems.removeLayer(layer);
                    }
                });
            }
        }
    };

    const initializeRealtimeUpdates = () => {
        signalRService.onEntityUpdate(handleEntityUpdate);
    };

    return {
        // Data
        zones,
        points,
        deleteEntity,
        initializeRealtimeUpdates,

        // NoFlyZone exports
        showNewZoneModal,
        newZoneForm,
        handleSaveZone,
        handleCancelZone,
        handleDeleteZone,

        // Point exports
        showPointModal,
        newPointForm,
        handleSavePoint,
        handleCancelPoint,

        // Common
        initializeDrawControls,
        loadNoFlyZones,
        loadPoints,
        startDrawing,
        toggleEditMode,
        saveEdits,
        cancelEdits,
        isEditing
    };
}
