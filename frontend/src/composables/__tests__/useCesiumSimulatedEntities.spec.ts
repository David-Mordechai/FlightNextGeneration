import { describe, it, expect, vi } from 'vitest';
import { useCesiumSimulatedEntities } from '../useCesiumSimulatedEntities';

// Mock Cesium
vi.mock('cesium', () => {
  return {
    Color: {
      OLIVE: { withAlpha: () => ({}) },
      SLATEGRAY: { withAlpha: () => ({}) },
      SADDLEBROWN: { withAlpha: () => ({}) },
      DARKBLUE: { withAlpha: () => ({}) },
      LIGHTGRAY: { withAlpha: () => ({}) },
      WHITE: { withAlpha: () => ({}) },
      BLACK: {},
    },
    Cartesian3: class { 
        static fromDegrees = vi.fn(() => ({}));
        constructor() { return {}; }
    },
    Cartesian2: class {
        constructor() { return {}; }
    },
    HeightReference: {
      CLAMP_TO_GROUND: 1,
      RELATIVE_TO_GROUND: 2,
    },
    LabelStyle: {
      FILL_AND_OUTLINE: 1,
    },
    VerticalOrigin: {
      BOTTOM: 1,
    },
    NearFarScalar: class {
        constructor() { return {}; }
    },
  };
});

describe('useCesiumSimulatedEntities', () => {
  it('should add entities to the viewer', () => {
    const { createSimulatedEntities } = useCesiumSimulatedEntities();
    
    const mockEntities = {
      add: vi.fn(),
    };
    
    const mockViewer = {
      entities: mockEntities,
    } as any;

    createSimulatedEntities(mockViewer);

    // Verify entities were added
    // 5 ground entities + Hangar Aircraft + Reaper UAV + Patrol Jet = 8 total
    expect(mockEntities.add).toHaveBeenCalledTimes(8);

    // Verify T-72 Tank uses the correct model
    // @ts-ignore
    const tankCall = mockEntities.add.mock.calls.find(call => call[0].name === 'T-72 Tank');
    if (tankCall) {
        expect(tankCall[0].model.uri).toContain('/GroundVehicle.glb');
    }
    
    // Verify Hangar Aircraft uses the original ORBITER4 model
    // @ts-ignore
    const hangarCall = mockEntities.add.mock.calls.find(call => call[0].name === 'Hangar Aircraft');
    if (hangarCall) {
        expect(hangarCall[0].model.uri).toContain('/ORBITER4.gltf');
    }
    
    // Verify Reaper UAV uses the new drone model
    // @ts-ignore
    const reaperCall = mockEntities.add.mock.calls.find(call => call[0].name === 'Reaper UAV');
    if (reaperCall) {
        expect(reaperCall[0].model.uri).toContain('/drone.glb');
    }
  });

  it('should handle null viewer gracefully', () => {
    const { createSimulatedEntities } = useCesiumSimulatedEntities();
    expect(() => createSimulatedEntities(null)).not.toThrow();
  });
});