export interface NoFlyZone {
    id?: string;
    name: string;
    geometry: any; // GeoJSON
    minAltitude: number;
    maxAltitude: number;
    isActive: boolean;
}

export const PointType = {
    Home: 0,
    Target: 1
} as const;

export type PointType = typeof PointType[keyof typeof PointType];

export interface Point {
    id?: string;
    name: string;
    location: any; // GeoJSON Point
    type: PointType;
}
const BASE_API_URL = 'http://localhost:5293/api';
const API_URL = `${BASE_API_URL}/noflyzones`;
const POINTS_API_URL = `${BASE_API_URL}/points`;

export const c4iService = {
    async getAll(): Promise<NoFlyZone[]> {
        const response = await fetch(API_URL);
        if (!response.ok) {
            throw new Error('Failed to fetch No Fly Zones');
        }
        return response.json();
    },

    async create(zone: NoFlyZone): Promise<NoFlyZone> {
        console.log('Creating NoFlyZone:', JSON.stringify(zone, null, 2));
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(zone)
        });
        if (!response.ok) {
            throw new Error('Failed to create No Fly Zone');
        }
        return response.json();
    },

    async update(id: string, zone: NoFlyZone): Promise<void> {
        const response = await fetch(`${API_URL}/${id}`, {
            method: 'PUT', // Assuming the backend supports PUT for updates
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(zone)
        });
        if (!response.ok) {
            throw new Error('Failed to update No Fly Zone');
        }
    },

    async delete(id: string): Promise<void> {
        const response = await fetch(`${API_URL}/${id}`, {
            method: 'DELETE'
        });
        if (!response.ok) {
            throw new Error('Failed to delete No Fly Zone');
        }
    },

    // Points
    async getAllPoints(): Promise<Point[]> {
        const response = await fetch(POINTS_API_URL);
        if (!response.ok) {
            throw new Error('Failed to fetch Points');
        }
        return response.json();
    },

    async createPoint(point: Point): Promise<Point> {
        const response = await fetch(POINTS_API_URL, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(point)
        });
        if (!response.ok) {
            throw new Error('Failed to create Point');
        }
        return response.json();
    },

    async deletePoint(id: string): Promise<void> {
        const response = await fetch(`${POINTS_API_URL}/${id}`, {
            method: 'DELETE'
        });
        if (!response.ok) {
            throw new Error('Failed to delete Point');
        }
    }
};
