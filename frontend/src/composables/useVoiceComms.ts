import { ref, computed } from 'vue';

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
  };

  // Set up voice initialization
  if (typeof window !== 'undefined' && window.speechSynthesis) {
    if (synth.getVoices().length > 0) {
      initVoice();
    } else {
      synth.onvoiceschanged = () => initVoice();
      // Also fallback timeout
      setTimeout(initVoice, 1000);
    }
  }

  const processQueue = () => {
    if (isMuted.value || messageQueue.length === 0 || isSpeaking.value) return; 
    
    const text = messageQueue.shift();
    if (!text) return;
    
    isSpeaking.value = true;

    // Primary: backend (Google) TTS. Fallback: browser speech synthesis.
    setTimeout(async () => {
        const played = await playCloudTTS(text);
        if (played) {
            isSpeaking.value = false;
            processQueue();
        } else {
            attemptSpeak(text, true);
        }
    }, 50);
  };

      // Fallback path: browser speech synthesis (cloud TTS is the primary).
      const attemptSpeak = (text: string, allowRetry: boolean) => {
        if (!synth) {
            playDataBurst();
            return;
        }
        const utterance = new SpeechSynthesisUtterance(text);
    if (voice.value) {
        utterance.voice = voice.value;
        utterance.pitch = 0.9;
        utterance.rate = 1.3;
    } else {
        utterance.pitch = 1.0;
        utterance.rate = 1.3;
    }
    utterance.volume = 1.0;
    utterance.onend = () => { isSpeaking.value = false; processQueue(); };
    utterance.onerror = (e) => {
        if (!isSpeaking.value) return;

        if (e.error === 'synthesis-failed' || e.error === 'voice-unavailable') {
             playDataBurst();
             return;
        }
        console.warn('Voice: Speech error:', e);
        if (allowRetry && voice.value !== null) {
            voice.value = null;
            attemptSpeak(text, false);
            return;
        }
        playDataBurst();
    };
    try {
        isSpeaking.value = true;
        synth.speak(utterance);
    } catch (err) {
        playDataBurst();
    }
  };

      // Primary voice: backend (Google) TTS.
      // Resolves true when playback completed, false when it could not play
      // (caller decides the fallback).
      let currentCloudAudio: HTMLAudioElement | null = null;
      const playCloudTTS = (text: string): Promise<boolean> => {
        return new Promise((resolve) => {
          const a = document.createElement('audio');
          if (a.canPlayType('audio/mpeg') === '') {
              resolve(false);
              return;
          }
          try {
              const safeText = encodeURIComponent(text.substring(0, 200));
              const url = `http://localhost:5135/api/tts?text=${safeText}`;
              const audio = new Audio(url);
              audio.playbackRate = 1.3;
              audio.volume = 1.0;
              audio.onended = () => {
                  currentCloudAudio = null;
                  resolve(true);
              };
              audio.onerror = () => {
                  currentCloudAudio = null;
                  resolve(false);
              };
              currentCloudAudio = audio;
              audio.play().catch((err) => {
                  console.warn('Voice: Cloud TTS play failed:', err);
                  currentCloudAudio = null;
                  resolve(false);
              });
          } catch (e) {
              currentCloudAudio = null;
              resolve(false);
          }
        });
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

    const chunks = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];
    
    chunks.forEach(chunk => {
        let remaining = chunk.trim();
        while (remaining.length > 0) {
            if (remaining.length > 180) {
                const splitIndex = remaining.lastIndexOf(' ', 180);
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
    if (isMuted.value) {
      if (synth) synth.cancel();
      messageQueue.length = 0;
      if (currentCloudAudio) {
        const audio = currentCloudAudio;
        audio.pause();
        // Fire 'ended' so the pending playCloudTTS promise resolves and state resets.
        audio.dispatchEvent(new Event('ended'));
      }
    }
  };

  const speakImmediate = (text: string, maxWait?: number): Promise<void> => {
      return new Promise((resolve) => {
          if (isMuted.value) { 
              resolve(); 
              return; 
          }
          
          if (synth) synth.cancel();

          let hasResolved = false;
          const safeResolve = () => {
              if (!hasResolved) {
                  hasResolved = true;
                  resolve();
              }
          };

          const speakViaBrowser = () => {
              if (!synth) {
                  safeResolve();
                  return;
              }
              const utterance = new SpeechSynthesisUtterance(text);
              if (voice.value) {
                  utterance.voice = voice.value;
                  utterance.pitch = 0.9;
                  utterance.rate = 1.3;
              }

              utterance.onend = () => safeResolve();

                              utterance.onerror = (e) => {
                                  if (e.error === 'interrupted') {
                                      safeResolve();
                                      return;
                                  }
                                  if (e.error === 'not-allowed') {
                                      const playOnUnlock = () => {
                                          synth.speak(utterance);
                                          window.removeEventListener('click', playOnUnlock);
                                          window.removeEventListener('keydown', playOnUnlock);
                                      };
                                      window.addEventListener('click', playOnUnlock, { once: true });
                                      window.addEventListener('keydown', playOnUnlock, { once: true });
                                      safeResolve();
                                      return;
                                  }

                                  if (e.error !== 'synthesis-failed' && e.error !== 'voice-unavailable') {
                                      console.warn('Voice: Speech error in speakImmediate:', e);
                                  }
                                  safeResolve();
                              };
              try {
                  synth.speak(utterance);
              } catch (e) {
                  safeResolve();
              }
          };

          // Primary: backend (Google) TTS; browser synthesis only if it fails.
          const doSpeak = async () => {
              if (maxWait) {
                  setTimeout(safeResolve, maxWait);
              }
              const played = await playCloudTTS(text);
              if (played) {
                  safeResolve();
              } else {
                  speakViaBrowser();
              }
          };

          doSpeak();
      });
  };

  const playBeep = () => {
      try {
          const AudioCtor = window.AudioContext || (window as any).webkitAudioContext;
          const ctx = new AudioCtor({ latencyHint: 'interactive' });
          
          const osc = ctx.createOscillator();
          const gain = ctx.createGain();
          
          osc.type = 'square'; 
          osc.frequency.setValueAtTime(1000, ctx.currentTime);
          osc.frequency.exponentialRampToValueAtTime(1600, ctx.currentTime + 0.08);
          
          gain.gain.setValueAtTime(1.0, ctx.currentTime); 
          gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.08);
          
          osc.connect(gain);
          gain.connect(ctx.destination);
          
          osc.start(ctx.currentTime);
          osc.stop(ctx.currentTime + 0.08);
      } catch (e) {}
  };

  const recordingState = ref<'idle' | 'preamble' | 'initializing' | 'recording'>('idle');
  let audioContext: AudioContext | null = null;
  let mediaStream: MediaStream | null = null;
  let workletNode: AudioWorkletNode | null = null;
  let input: MediaStreamAudioSourceNode | null = null;
  let audioData: Float32Array[] = [];
  let recordingLength = 0;
  const targetSampleRate = 16000;
  let shouldCancel = false;

  const startRecording = async () => {
    if (recordingState.value !== 'idle') return;
    
    shouldCancel = false;
    recordingState.value = 'preamble';
    
    await speakImmediate("How can I help?", 1000);
    
    if (shouldCancel) {
        recordingState.value = 'idle';
        return;
    }

    playBeep();
    recordingState.value = 'initializing';

    try {
      if (shouldCancel) throw new Error('Cancelled');
      mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (shouldCancel) throw new Error('Cancelled');
      
      const AudioCtor = window.AudioContext || (window as any).webkitAudioContext;
      audioContext = new AudioCtor({ sampleRate: targetSampleRate });
      
      if (audioContext.state === 'suspended') {
          await audioContext.resume();
      }

      input = audioContext.createMediaStreamSource(mediaStream);
      
      try {
          await audioContext.audioWorklet.addModule('/recorder-processor.js');
          workletNode = new AudioWorkletNode(audioContext, 'recorder-processor');
      } catch (err) {
          console.error("Voice: Failed to load audio worklet", err);
          throw err;
      }

      audioData = [];
      recordingLength = 0;

      workletNode.port.onmessage = (e) => {
        if (recordingState.value !== 'recording') return;
        const channelData = e.data; 
        audioData.push(new Float32Array(channelData));
        recordingLength += channelData.length;
      };

      input.connect(workletNode);
      workletNode.connect(audioContext.destination);

      if (shouldCancel) throw new Error('Cancelled');
      recordingState.value = 'recording';
    } catch (err) {
      if (mediaStream) {
          mediaStream.getTracks().forEach(track => track.stop());
          mediaStream = null;
      }
      recordingState.value = 'idle';
    }
  };

  const stopRecording = async (): Promise<Blob | null> => {
    if (recordingState.value === 'preamble' || recordingState.value === 'initializing') {
        shouldCancel = true;
        let attempts = 0;
        while ((recordingState.value as any) !== 'idle' && (recordingState.value as any) !== 'recording' && attempts < 20) {
            await new Promise(r => setTimeout(r, 100));
            attempts++;
        }
    }

    if (recordingState.value !== 'recording') {
        recordingState.value = 'idle';
        return null;
    }

    recordingState.value = 'idle'; 

    if (mediaStream) {
      mediaStream.getTracks().forEach(track => track.stop());
      mediaStream = null;
    }
    
    if (input) { input.disconnect(); input = null; }
    if (workletNode) { 
        workletNode.disconnect(); 
        workletNode.port.onmessage = null;
        workletNode = null; 
    }
    if (audioContext) { 
        await audioContext.close(); 
        audioContext = null; 
    }

    if (recordingLength === 0) {
        console.warn('Voice: No audio data captured.');
        return null;
    }

    const buffer = mergeBuffers(audioData, recordingLength);
    const wavBlob = encodeWAV(buffer, targetSampleRate);
    
    return wavBlob;
  };

  const mergeBuffers = (buffers: Float32Array[], length: number) => {
      const result = new Float32Array(length);
      let offset = 0;
      for (const buffer of buffers) {
          result.set(buffer, offset);
          offset += buffer.length;
      }
      return result;
  };

  const encodeWAV = (samples: Float32Array, sampleRate: number) => {
      const buffer = new ArrayBuffer(44 + samples.length * 2);
      const view = new DataView(buffer);

      writeString(view, 0, 'RIFF');
      view.setUint32(4, 36 + samples.length * 2, true);
      writeString(view, 8, 'WAVE');
      writeString(view, 12, 'fmt ');
      view.setUint32(16, 16, true);
      view.setUint16(20, 1, true); 
      view.setUint16(22, 1, true); 
      view.setUint32(24, sampleRate, true);
      view.setUint32(28, sampleRate * 2, true);
      view.setUint16(32, 2, true); 
      view.setUint16(34, 16, true); 

      writeString(view, 36, 'data');
      view.setUint32(40, samples.length * 2, true);

      floatTo16BitPCM(view, 44, samples);

      return new Blob([view], { type: 'audio/wav' });
  };

  const floatTo16BitPCM = (output: DataView, offset: number, input: Float32Array) => {
      for (let i = 0; i < input.length; i++, offset += 2) {
          const val = input[i] || 0;
          const s = Math.max(-1, Math.min(1, val));
          output.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7FFF, true);
      }
  };

  const writeString = (view: DataView, offset: number, string: string) => {
      for (let i = 0; i < string.length; i++) {
          view.setUint8(offset + i, string.charCodeAt(i));
      }
  };

  const transcribe = async (audioBlob: Blob): Promise<string> => {
    const formData = new FormData();
    formData.append('file', audioBlob, 'recording.wav');

    try {
      const response = await fetch('http://localhost:5135/api/tts/transcribe', {
        method: 'POST',
        body: formData
      });
      
      if (!response.ok) throw new Error('Transcription failed: ' + response.statusText);
      
      const data = await response.json();
      return data.text;
    } catch (e) {
      console.error('Voice: Transcription error:', e);
      return '';
    }
  };

  const speakAsCommander = (text: string): Promise<void> => {
    return new Promise((resolve) => {
      if (isMuted.value) {
        resolve();
        return;
      }
      if (!synth) {
        resolve();
        return;
      }
      
      if (synth.speaking) {
        synth.cancel();
      }

      const utterance = new SpeechSynthesisUtterance(text);
      const voices = synth.getVoices();
      const englishVoices = voices.filter(v => v.lang.startsWith('en'));
      
      // Try to find a distinct male/non-default voice for the Commander
      const agentVoiceName = voice.value?.name || '';
      const commanderVoice = englishVoices.find(v => (v.name.toLowerCase().includes('mark') || v.name.toLowerCase().includes('george') || v.name.toLowerCase().includes('male')) && v.name !== agentVoiceName) ||
                             englishVoices.find(v => v.name !== agentVoiceName) ||
                             voices[0];

      if (commanderVoice) {
        utterance.voice = commanderVoice;
        utterance.pitch = 0.82; // Deep authoritative male pitch!
        utterance.rate = 0.95; // Slightly slower, commanding speed
        console.log(`Voice: Commander using male voice '${commanderVoice.name}'`);
      } else {
        utterance.pitch = 0.8; // Fallback pitch shift
        utterance.rate = 0.95;
      }

      utterance.onend = () => {
        resolve();
      };
      utterance.onerror = (err) => {
        console.warn('Voice: Commander speech error/interrupted:', err);
        resolve();
      };

      synth.speak(utterance);
    });
  };

  return { isMuted, isSpeaking, voiceStatus, toggleMute, speak, speakImmediate, playBeep, isRecording: computed(() => recordingState.value !== 'idle'), startRecording, stopRecording, transcribe, speakAsCommander };
}