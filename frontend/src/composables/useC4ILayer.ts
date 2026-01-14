import { ref, reactive } from 'vue';
import L from 'leaflet';
import { c4iService, type NoFlyZone } from '../services/C4IService';

export function useC4ILayer(map: any) {
    const drawnItems = new L.FeatureGroup();
    const showNewZoneModal = ref(false);
    const isEditingGeometry = ref(false);
    const tempLayer = ref<L.Layer | null>(null);
    let drawControlInstance: any = null;

    const newZoneForm = reactive<{
        id?: string;
        name: string;
        minAltitude: number;
        maxAltitude: number;
    }>({
        name: '',
        minAltitude: 0,
        maxAltitude: 10000
    });

    const initializeDrawControls = (leafletMap: L.Map) => {
        leafletMap.addLayer(drawnItems);
        
        // @ts-ignore
        const drawControl = new L.Control.Draw({
            draw: {
                polyline: false,
                circle: false,
                marker: false,
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
            tempLayer.value = layer;
            leafletMap.addLayer(layer);
            
            // Reset form
            newZoneForm.id = undefined;
            newZoneForm.name = '';
            newZoneForm.minAltitude = 0;
            newZoneForm.maxAltitude = 10000;
            
            showNewZoneModal.value = true;
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
                if (id) {
                    const geoJson = layer.toGeoJSON();
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
            });
        });

        // 4. Deleted (Via toolbar)
        leafletMap.on(L.Draw.Event.DELETED, async (e: any) => {
            const layers = e.layers;
            layers.eachLayer(async (layer: any) => {
                const id = (layer as any).feature?.properties?.id;
                if (id) {
                    try {
                        await c4iService.delete(id);
                        console.log(`Zone ${id} deleted`);
                    } catch (err) {
                        console.error(`Failed to delete zone ${id}`, err);
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
                        maxAltitude: zone.maxAltitude
                    });
                }
            });
        } catch (e) {
            console.error("Failed to load No Fly Zones", e);
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
            
            // Edit Click Listener
            sourceLayer.on('click', () => {
                if (!isEditingGeometry.value) return; 

                tempLayer.value = sourceLayer; 
                newZoneForm.id = properties.id;
                newZoneForm.name = properties.name;
                newZoneForm.minAltitude = properties.minAltitude;
                newZoneForm.maxAltitude = properties.maxAltitude;
                showNewZoneModal.value = true;
            });

            drawnItems.addLayer(sourceLayer);
        }
    };

    const handleSaveZone = async () => {
        if (!tempLayer.value) return;

        const layer = tempLayer.value as any;
        const geoJson = layer.toGeoJSON();
        // Handle cases where toGeoJSON returns a Feature or a raw Geometry
        const geometry = geoJson.type === 'Feature' ? geoJson.geometry : geoJson;

        const zoneData: NoFlyZone = {
            id: newZoneForm.id,
            name: newZoneForm.name,
            minAltitude: newZoneForm.minAltitude,
            maxAltitude: newZoneForm.maxAltitude,
            isActive: true,
            geometry: geometry
        };

        try {
            if (newZoneForm.id) {
                // UPDATE
                await c4iService.update(newZoneForm.id, zoneData);
                
                // Update visuals
                if (layer.feature && layer.feature.properties) {
                    layer.feature.properties.name = zoneData.name;
                    layer.feature.properties.minAltitude = zoneData.minAltitude;
                    layer.feature.properties.maxAltitude = zoneData.maxAltitude;
                    
                    const newContent = `<strong>${zoneData.name}</strong><br>Alt: ${zoneData.minAltitude}-${zoneData.maxAltitude}ft`;
                    layer.setPopupContent(newContent);
                }
                
                // Close Edit Mode
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
                    maxAltitude: saved.maxAltitude
                });
                
                if(map.value) map.value.removeLayer(layer);
            }

            showNewZoneModal.value = false;
            tempLayer.value = null;
            newZoneForm.id = undefined;
            newZoneForm.name = '';
        } catch (e) {
            console.error("Save Zone Error Details:", e);
            alert('Failed to save zone: ' + (e instanceof Error ? e.message : String(e)));
        }
    };

    const handleCancelZone = () => {
        if (!newZoneForm.id && tempLayer.value && map.value) {
            map.value.removeLayer(tempLayer.value as L.Layer);
        }
        showNewZoneModal.value = false;
        tempLayer.value = null;
        newZoneForm.id = undefined;
    };

    const handleDeleteZone = async () => {
        if (!newZoneForm.id || !tempLayer.value) return;
        
        try {
            await c4iService.delete(newZoneForm.id);
            drawnItems.removeLayer(tempLayer.value as L.Layer);
            showNewZoneModal.value = false;
            tempLayer.value = null;
        } catch (e) {
            alert('Failed to delete zone');
            console.error(e);
        }
    };

    return {
        showNewZoneModal,
        newZoneForm,
        initializeDrawControls,
        loadNoFlyZones,
        handleSaveZone,
        handleCancelZone,
        handleDeleteZone
    };
}
