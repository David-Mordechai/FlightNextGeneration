import { ref, onMounted, onUnmounted } from 'vue';

export function useVoiceComms() {
  const isMuted = ref(false); 
  const isSpeaking = ref(false);
  const voiceStatus = ref<'loading' | 'ready' | 'error'>('loading');
  const synth = window.speechSynthesis;
  const voice = ref<SpeechSynthesisVoice | null>(null);
  
  // Queue system
  const messageQueue: string[] = [];

  // Initialize voices
  const initVoice = () => {
    const voices = synth.getVoices();
    if (voices.length === 0) {
        if (voiceStatus.value === 'loading') {
            setTimeout(initVoice, 1000);
        } else {
            voiceStatus.value = 'error';
        }
        return;
    }

    voiceStatus.value = 'ready';

    // Priority: Male voices first for "Military Co-pilot" feel
    const selectedVoice = voices.find(v => v.name.includes('Male') && v.lang.startsWith('en')) ||
                          voices.find(v => v.name.includes('David')) || 
                          voices.find(v => v.name.includes('Google US English')) ||
                          voices.find(v => v.name.toLowerCase().includes('zira')) || 
                          voices.find(v => v.lang === 'en-US') ||
                          voices[0]; 
    
    voice.value = selectedVoice || null;
    console.log('Voice selected:', voice.value?.name);
  };

  const processQueue = () => {
    if (isMuted.value || messageQueue.length === 0 || isSpeaking.value) return; 
    
    const text = messageQueue.shift();
    if (!text) return;
    
    isSpeaking.value = true;
    
    setTimeout(() => { attemptSpeak(text, true); }, 50); 
  };

  const attemptSpeak = (text: string, allowRetry: boolean) => {
    const utterance = new SpeechSynthesisUtterance(text);
    if (voice.value) {
        utterance.voice = voice.value;
        utterance.pitch = 0.9; 
        utterance.rate = 1.3; // Updated to 1.3
    } else {
        utterance.pitch = 1.0;
        utterance.rate = 1.3; // Updated to 1.3
    }
    utterance.volume = 1.0;
    utterance.onend = () => { isSpeaking.value = false; processQueue(); };
    utterance.onerror = (e) => {
        // Avoid double-triggering if we already handled this via catch block
        if (!isSpeaking.value) return; 

        if (e.error === 'synthesis-failed' || e.error === 'voice-unavailable') {
             console.log(`Local TTS system unresponsive (${e.error}). Switching to Cloud Relay.`);
             playCloudTTS(text);
             return;
        }
        console.warn('Speech synthesis error:', e.error);
        if (allowRetry && voice.value !== null) {
            console.log('Falling back to system default voice...');
            voice.value = null;
            attemptSpeak(text, false);
            return;
        }
        console.log('Local speech failed. Attempting Cloud TTS fallback...');
        playCloudTTS(text);
    };
    try { 
        // Ensure flag is true BEFORE calling speak
        isSpeaking.value = true;
        synth.speak(utterance); 
    } catch (err) { 
        console.error('Synth immediate error:', err);
        // If speak throws immediately, onerror might NOT fire, so we handle it here
        // But we must reset flag so onerror doesn't double-fire if it does happen
        isSpeaking.value = false; 
        playCloudTTS(text); 
    }
  };

  const playCloudTTS = async (text: string) => {
    const a = document.createElement('audio');
    if (a.canPlayType('audio/mpeg') === '') {
        playDataBurst();
        return;
    }
    try {
        const safeText = encodeURIComponent(text.substring(0, 200));
        const url = `http://localhost:5135/api/tts?text=${safeText}`;
        const audio = new Audio(url);
        audio.playbackRate = 1.3; // Updated to 1.3
        audio.volume = 1.0;
        audio.onended = () => { isSpeaking.value = false; processQueue(); };
        audio.onerror = () => { playDataBurst(); };
        isSpeaking.value = true;
        await audio.play();
    } catch (e) {
        playDataBurst();
    }
  };

  const playDataBurst = () => {
    try {
      const AudioContext = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioContext) { isSpeaking.value = false; processQueue(); return; }
      const ctx = new AudioContext();
      const t = ctx.currentTime;
      const tones = 5;
      const duration = 0.08;
      for (let i = 0; i < tones; i++) {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = i % 2 === 0 ? 'square' : 'sawtooth';
        osc.frequency.setValueAtTime(800 + Math.random() * 1200, t + (i * duration));
        gain.gain.setValueAtTime(0.05, t + (i * duration));
        gain.gain.exponentialRampToValueAtTime(0.001, t + (i * duration) + duration - 0.01);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(t + (i * duration));
        osc.stop(t + (i * duration) + duration);
      }
      setTimeout(() => { isSpeaking.value = false; processQueue(); }, (tones * duration * 1000) + 100);
    } catch (e) { isSpeaking.value = false; processQueue(); }
  };

  const speak = (text: string) => {
    if (isMuted.value) return;

    // 1. Clean Markdown
    let cleanText = text.replace(/(\*\*|__)(.*?)\1/g, '$2');
    cleanText = cleanText.replace(/(\*|_)(.*?)\1/g, '$2');
    cleanText = cleanText.replace(/^#+\s+/gm, '');
    cleanText = cleanText.replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1');
    cleanText = cleanText.replace(/```[\s\S]*?```/g, 'Code block omitted.');
    cleanText = cleanText.replace(/`([^`]+)`/g, '$1');

    // 2. Chunking Logic (Fixes Google TTS cutoff)
    // Split by punctuation first
    const chunks = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];
    
    chunks.forEach(chunk => {
        let remaining = chunk.trim();
        while (remaining.length > 0) {
            // Force split if still too long (Google limit ~200)
            if (remaining.length > 180) {
                const splitIndex = remaining.lastIndexOf(' ', 180);
                // If no space found, force split at 180
                const safeSplit = splitIndex > 0 ? splitIndex : 180;
                
                const part = remaining.substring(0, safeSplit);
                messageQueue.push(part.trim());
                remaining = remaining.substring(part.length).trim();
            } else {
                messageQueue.push(remaining);
                remaining = '';
            }
        }
    });

    processQueue();
  };

  const toggleMute = () => {
    isMuted.value = !isMuted.value;
    if (isMuted.value) { synth.cancel(); messageQueue.length = 0; }
  };

  onMounted(() => {
    synth.cancel();
    initVoice();
    if (synth.onvoiceschanged !== undefined) {
      synth.onvoiceschanged = initVoice;
    }
  });

  onUnmounted(() => { synth.cancel(); });

  return { isMuted, isSpeaking, voiceStatus, toggleMute, speak };
}