export interface NoFlyZone {
    id?: string;
    name: string;
    geometry: any; // GeoJSON
    minAltitude: number;
    maxAltitude: number;
    isActive: boolean;
}

const API_URL = 'http://localhost:5293/api/noflyzones';

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
    }
};
