import { ref } from 'vue';
import L from 'leaflet';
import { c4iService, type NoFlyZone, type Point, PointType } from '../services/C4IService';

export function useC4ILayer(map: any) {
    const drawnItems = new L.FeatureGroup();
    
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
    const tempLayer = ref<L.Layer | null>(null);
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
                        // It's a Point
                         console.log('Point update not fully implemented via drag yet, pending requirement');
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

    const loadNoFlyZones = async () => {
        try {
            const zones = await c4iService.getAll();
            zones.forEach(zone => {
                if (zone.geometry) {
                    const layer = L.geoJSON(zone.geometry, {
                        style: { color: '#ff0000', weight: 2, fillOpacity: 0.2 }
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
            const points = await c4iService.getAllPoints();
            points.forEach(point => {
                if (point.location) {
                    const iconUrl = point.type === PointType.Home ? '/home.svg' : '/target.svg';
                    const customIcon = L.icon({
                        iconUrl: iconUrl,
                        iconSize: [32, 32],
                        iconAnchor: [16, 16],
                        popupAnchor: [0, -16]
                    });

                    // Create marker directly
                    const lat = point.location.coordinates[1];
                    const lng = point.location.coordinates[0];
                    const layer = L.marker([lat, lng], { icon: customIcon });

                    // Add properties
                    // @ts-ignore
                    layer.feature = {
                        type: 'Feature',
                        properties: {
                            id: point.id,
                            name: point.name,
                            type: point.type,
                            isPoint: true
                        }
                    };

                    layer.bindPopup(`<strong>${point.name}</strong><br>${point.type === PointType.Home ? 'Home' : 'Target'}`);
                    layer.bindTooltip(point.name, { 
                        permanent: true, 
                        direction: 'right', 
                        className: 'entity-label'
                    });
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
                direction: 'right', 
                className: 'entity-label'
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
                const savedLayer = L.geoJSON(saved.geometry, {
                    style: { color: '#ff0000', weight: 2, fillOpacity: 0.2 }
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

    const handleDeleteZone = async () => {
        if (!newZoneForm.value.id || !tempLayer.value) return;
        
        try {
            await c4iService.delete(newZoneForm.value.id);
            drawnItems.removeLayer(tempLayer.value as L.Layer);
            showNewZoneModal.value = false;
            tempLayer.value = null;
        } catch (e) {
            alert('Failed to delete zone');
            console.error(e);
        }
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
            name: newPointForm.value.name,
            type: newPointForm.value.type,
            location: {
                type: 'Point',
                coordinates: [latLng.lng, latLng.lat]
            }
        };

        try {
            const saved = await c4iService.createPoint(pointData);
            
            // Remove temporary drawing layer
            if (map.value) map.value.removeLayer(layer);

            // Create fresh marker from saved data
            const lat = saved.location.coordinates[1];
            const lng = saved.location.coordinates[0];
            const iconUrl = saved.type === PointType.Home ? '/home.svg' : '/target.svg';
            
            const customIcon = L.icon({
                iconUrl: iconUrl,
                iconSize: [32, 32],
                iconAnchor: [16, 16],
                popupAnchor: [0, -16]
            });

            const newMarker = L.marker([lat, lng], { icon: customIcon });

            // Set Properties
             // @ts-ignore
            newMarker.feature = {
                type: 'Feature',
                properties: {
                    id: saved.id,
                    name: saved.name,
                    type: saved.type,
                    isPoint: true
                }
            };
            
            newMarker.bindPopup(`<strong>${saved.name}</strong><br>${saved.type === PointType.Home ? 'Home' : 'Target'}`);
            newMarker.bindTooltip(saved.name, { 
                permanent: true, 
                direction: 'right', 
                className: 'entity-label'
            });
            
            drawnItems.addLayer(newMarker);
            
            showPointModal.value = false;
            tempLayer.value = null;

            // Force map update to avoid floating markers
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

    return {
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
