// Console suppression for "Deprecated use of _flat" warning from leaflet-draw
const originalWarn = console.warn;
console.warn = (...args: any[]) => {
    if (args.length > 0 && typeof args[0] === 'string' && args[0].includes('Deprecated use of _flat')) {
        return; // Suppress this specific warning
    }
    originalWarn.apply(console, args);
};

