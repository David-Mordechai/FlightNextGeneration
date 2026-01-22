class RecorderProcessor extends AudioWorkletProcessor {
    process(inputs, outputs, parameters) {
        const input = inputs[0];
        if (input && input.length > 0) {
            // input[0] is the first channel
            // We need to copy it because the buffer is reused
            const channelData = input[0];
            this.port.postMessage(channelData);
        }
        return true; // Keep processor alive
    }
}

registerProcessor('recorder-processor', RecorderProcessor);