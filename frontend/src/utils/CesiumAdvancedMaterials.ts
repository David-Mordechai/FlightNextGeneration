import * as Cesium from 'cesium';

/**
 * Robustly registers custom materials for a specific Cesium context.
 * This avoids "object does not belong to this context" errors by ensuring
 * that shader programs and textures are not shared between multiple viewers.
 */

// Track initialization per context instance to avoid redundant registration
const initializedContexts = new WeakSet<Cesium.Scene>();

export function registerCustomMaterials(viewer: Cesium.Viewer | null, contextId: string) {
    if (!viewer || !viewer.scene) return;
    
    const scene = viewer.scene;
    if (initializedContexts.has(scene)) return;

    // Use unique material names per context to be absolutely safe
    const polylineFlowName = `PolylineFlow_${contextId}`;
    const tacticalBeamName = `TacticalBeamV3_${contextId}`;

    // Register PolylineFlow
    // @ts-ignore
    Cesium.Material._materialCache.addMaterial(polylineFlowName, {
        fabric: {
            type: polylineFlowName,
            uniforms: {
                flowColor: new Cesium.Color(1.0, 1.0, 1.0, 1.0),
                image: '',
                speed: 1,
                time: 0
            },
            source: `
                czm_material czm_getMaterial(czm_materialInput materialInput)
                {
                    czm_material material = czm_getDefaultMaterial(materialInput);
                    vec2 st = materialInput.st;
                    float time = time * speed;
                    float s = fract(st.s - time);
                    vec4 colorImage = texture(image, vec2(s, 0.5));
                    material.alpha = colorImage.a * flowColor.a;
                    material.diffuse = flowColor.rgb * colorImage.rgb;
                    material.emission = material.diffuse * 1.5;
                    return material;
                }
            `
        },
        translucent: () => true
    });

    // Register TacticalBeam
    // @ts-ignore
    Cesium.Material._materialCache.addMaterial(tacticalBeamName, {
        fabric: {
            type: tacticalBeamName,
            uniforms: {
                beamColor: new Cesium.Color(0.0, 0.95, 1.0, 1.0),
                speed: 0.3,
                time: 0
            },
            source: `
                czm_material czm_getMaterial(czm_materialInput materialInput)
                {
                    czm_material material = czm_getDefaultMaterial(materialInput);
                    vec2 st = materialInput.st;
                    float shimmer = fract(st.s - time * speed);
                    float shimmerMask = smoothstep(0.0, 0.5, shimmer) * smoothstep(1.0, 0.5, shimmer);
                    float edgeMask = 1.0 - abs(st.t - 0.5) * 2.0;
                    edgeMask = pow(edgeMask, 2.0);
                    float alpha = (0.6 + shimmerMask * 0.4);
                    alpha *= edgeMask * beamColor.a;
                    material.diffuse = beamColor.rgb;
                    material.alpha = alpha;
                    material.emission = beamColor.rgb * (0.5 + shimmerMask * 0.5);
                    return material;
                }
            `
        },
        translucent: () => true
    });

    initializedContexts.add(scene);
}

export class PolylineFlowMaterialProperty {
    private _definitionChanged = new Cesium.Event();
    private _color: Cesium.Color;
    private _speed: number;
    private _gradientImage: string;
    private _contextId: string;

    constructor(options: { color?: Cesium.Color, speed?: number, contextId: string }) {
        this._color = options.color || Cesium.Color.CYAN;
        this._speed = options.speed || 1.0;
        this._contextId = options.contextId;
        this._gradientImage = this.generateGradientImage();
    }

    get isConstant() { return false; }
    get definitionChanged() { return this._definitionChanged; }
    getType() { return `PolylineFlow_${this._contextId}`; }

    getValue(_time: Cesium.JulianDate, result: any) {
        if (!Cesium.defined(result)) result = {};
        result.flowColor = this._color;
        result.speed = this._speed;
        result.image = this._gradientImage;
        result.time = performance.now() / 1000.0;
        return result;
    }

    equals(other: any) {
        return this === other || (other instanceof PolylineFlowMaterialProperty && this._contextId === other._contextId);
    }

    private generateGradientImage() {
        const canvas = document.createElement('canvas');
        canvas.width = 256; canvas.height = 1;
        const ctx = canvas.getContext('2d');
        if (!ctx) return '';
        const grd = ctx.createLinearGradient(0, 0, 256, 0);
        grd.addColorStop(0, 'rgba(255, 255, 255, 0)');
        grd.addColorStop(0.5, 'rgba(255, 255, 255, 0.2)');
        grd.addColorStop(1, 'rgba(255, 255, 255, 1.0)');
        ctx.fillStyle = grd; ctx.fillRect(0, 0, 256, 1);
        return canvas.toDataURL();
    }
}

export class TacticalBeamMaterialProperty {
    private _definitionChanged = new Cesium.Event();
    private _color: Cesium.Color;
    private _speed: number;
    private _contextId: string;

    constructor(options: { color?: Cesium.Color, speed?: number, contextId: string }) {
        this._color = options.color || Cesium.Color.fromCssColorString('#00F2FF');
        this._speed = options.speed || 2.0;
        this._contextId = options.contextId;
    }

    get isConstant() { return false; }
    get definitionChanged() { return this._definitionChanged; }
    getType() { return `TacticalBeamV3_${this._contextId}`; }

    getValue(_time: Cesium.JulianDate, result: any) {
        if (!Cesium.defined(result)) result = {};
        result.beamColor = this._color;
        result.speed = this._speed;
        result.time = performance.now() / 1000.0;
        return result;
    }

    equals(other: any) {
        return this === other || (other instanceof TacticalBeamMaterialProperty && this._contextId === other._contextId);
    }
}
