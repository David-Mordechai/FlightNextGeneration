import { ref, onMounted, onUnmounted } from 'vue';

export interface VoiceCommsOptions {
  enableSquelch?: boolean;
}

export function useVoiceComms(options: VoiceCommsOptions = { enableSquelch: true }) {
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

  const playSquelch = () => {
    if (!options.enableSquelch || isMuted.value) return;
    try {
      const AudioContext = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioContext) return;
      const ctx = new AudioContext();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(1200, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(600, ctx.currentTime + 0.1);
      gain.gain.setValueAtTime(0.05, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.1);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.15);
    } catch (e) {
      console.warn('Squelch audio failed', e);
    }
  };

  const processQueue = () => {
    if (isMuted.value || messageQueue.length === 0 || synth.speaking) return;
    const text = messageQueue.shift();
    if (!text) return;
    isSpeaking.value = true;
    playSquelch();
    setTimeout(() => { attemptSpeak(text, true); }, 150);
  };

  const attemptSpeak = (text: string, allowRetry: boolean) => {
    const utterance = new SpeechSynthesisUtterance(text);
    if (voice.value) {
        utterance.voice = voice.value;
        utterance.pitch = 0.9; 
        utterance.rate = 1.1;
    } else {
        utterance.pitch = 1.0;
        utterance.rate = 1.0;
    }
    utterance.volume = 1.0;
    utterance.onend = () => { isSpeaking.value = false; processQueue(); };
    utterance.onerror = (e) => {
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
    try { synth.speak(utterance); } catch (err) { playCloudTTS(text); }
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
        audio.playbackRate = 1.0; 
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
    let cleanText = text.replace(/(\*\*|__)(.*?)\1/g, '$2');
    cleanText = cleanText.replace(/(\*|_)(.*?)\1/g, '$2');
    cleanText = cleanText.replace(/^#+\s+/gm, '');
    cleanText = cleanText.replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1');
    cleanText = cleanText.replace(/```[\s\S]*?```/g, 'Code block omitted.');
    cleanText = cleanText.replace(/`([^`]+)`/g, '$1');
    messageQueue.push(cleanText);
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