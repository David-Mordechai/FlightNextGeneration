import * as Cesium from 'cesium';

export class PolylineFlowMaterialProperty {
    private _definitionChanged: Cesium.Event;
    private _color: Cesium.Color | undefined;
    private _speed: number | undefined;
    private _gradientImage: string;

    constructor(options: { color?: Cesium.Color, speed?: number } = {}) {
        this._definitionChanged = new Cesium.Event();
        this._color = options.color || Cesium.Color.CYAN;
        this._speed = options.speed || 1.0;
        this._gradientImage = this.generateGradientImage();
    }

    get isConstant() { return false; }
    get definitionChanged() { return this._definitionChanged; }
    get color() { return this._color; }
    get speed() { return this._speed; }

    getType(_time: Cesium.JulianDate) {
        return 'PolylineFlow';
    }

    getValue(_time: Cesium.JulianDate, result: any) {
        if (!Cesium.defined(result)) {
            result = {};
        }
        result.color = this._color;
        result.speed = this._speed;
        result.image = this._gradientImage;
        result.time = performance.now() / 1000.0; 
        return result;
    }

    equals(other: any) {
        return (this === other ||
            (other instanceof PolylineFlowMaterialProperty &&
             ((!!this._color && !!other._color && this._color.equals(other._color))) &&
             this._speed === other._speed));
    }

    // Generate a simple gradient trail image on the fly
    private generateGradientImage() {
        const canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 1;
        const ctx = canvas.getContext('2d');
        if (!ctx) return '';

        const grd = ctx.createLinearGradient(0, 0, 256, 0);
        // A trail that fades out
        grd.addColorStop(0, 'rgba(255, 255, 255, 0)');
        grd.addColorStop(0.5, 'rgba(255, 255, 255, 0.2)');
        grd.addColorStop(1, 'rgba(255, 255, 255, 1.0)');

        ctx.fillStyle = grd;
        ctx.fillRect(0, 0, 256, 1);
        return canvas.toDataURL();
    }
}

// Register the Material
export function registerCustomMaterials() {
    if ((Cesium.Material as any).PolylineFlowType) return; // Already registered

    (Cesium.Material as any).PolylineFlowType = 'PolylineFlow';
    (Cesium.Material as any)._materialCache.addMaterial('PolylineFlow', {
        fabric: {
            type: 'PolylineFlow',
            uniforms: {
                color: new Cesium.Color(1.0, 1.0, 1.0, 1.0),
                image: '',
                speed: 1,
                time: 0
            },
            source: `
                czm_material czm_getMaterial(czm_materialInput materialInput)
                {
                    czm_material material = czm_getDefaultMaterial(materialInput);
                    vec2 st = materialInput.st;
                    
                    // Animate the texture coordinates
                    float time = time * speed;
                    float s = fract(st.s - time);
                    
                    // Sample the gradient image
                    vec4 colorImage = texture(image, vec2(s, 0.5));
                    
                    // Multiply with base color
                    material.alpha = colorImage.a * color.a;
                    material.diffuse = color.rgb * colorImage.rgb;
                    
                    // Add a glow/emission effect
                    material.emission = material.diffuse * 1.5;
                    
                    return material;
                }
            `
        },
        translucent: function(_material: any) {
            return true;
        }
    });
}
