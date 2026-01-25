<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue';
import { signalRService } from '../services/SignalRService';
import { useVoiceComms } from '../composables/useVoiceComms';

const { speak, toggleMute, isMuted, voiceStatus, startRecording, stopRecording, transcribe, speakImmediate } = useVoiceComms();

interface Message {
  user: string;
  text: string;
  isSystem: boolean;
  duration?: number; // Duration in seconds
  isTransient?: boolean; // If true, this message won't persist to DB but stays in UI session
}

const messages = ref<Message[]>([]);
const newMessage = ref('');
const chatContainer = ref<HTMLElement | null>(null);
const micState = ref<'initial' | 'checking' | 'ready' | 'error'>('initial');
const isMicActive = ref(false); // Local state for instant UI feedback

const stripMarkdown = (text: string) => {
    return text
        .replace(/(\*\*|__)(.*?)\1/g, '$2')
        .replace(/(\*|_)(.*?)\1/g, '$2')
        .replace(/^#+\s+/gm, '')
        .replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1')
        .replace(/```[\s\S]*?```/g, '')
        .replace(/`([^`]+)`/g, '$1');
};

const scrollToBottom = async () => {
  await nextTick();
  if (chatContainer.value) {
    chatContainer.value.scrollTop = chatContainer.value.scrollHeight;
  }
};

const handleMicClick = async () => {
    // 1. Initial State: Check Readiness
    if (micState.value === 'initial' || micState.value === 'error') {
        micState.value = 'checking';
        try {
            const isReady = await signalRService.checkAiStatus();
            if (isReady) {
                micState.value = 'ready';
                messages.value.push({ user: 'Mission Control', text: 'I am here to assist.', isSystem: true, isTransient: true });
                await speakImmediate("I am here to assist.");
            } else {
                micState.value = 'error';
                messages.value.push({ user: 'Mission Control', text: 'System unavailable.', isSystem: true, isTransient: true });
                await speakImmediate("System unavailable.");
            }
        } catch (e) {
            micState.value = 'error';
            messages.value.push({ user: 'Mission Control', text: 'Connection error.', isSystem: true, isTransient: true });
        }
        return;
    }
};

// Separate handlers for Recording (Only active when Ready)
const handleMicDown = async () => {
    if (micState.value !== 'ready') return;
    isMicActive.value = true;
    await startRecording();
};

const handleMicUp = async () => {
  if (micState.value !== 'ready') return;
  if (!isMicActive.value) return;
  isMicActive.value = false;
  
  const blob = await stopRecording();
  if (blob) {
    const text = await transcribe(blob);
    if (text) {
        // 1. Show User Message Immediately (Optimistic)
        const userMsgObj: Message = { user: 'Commander', text: text, isSystem: false };
        messages.value.push(userMsgObj);

        // 2. Show Processing (Transient)
        const processingMsg: Message = { user: 'Mission Control', text: `Processing ${text}...`, isSystem: true, isTransient: true };
        messages.value.push(processingMsg);
        scrollToBottom();
        
        await speakImmediate(`Processing ${text}...`);

        // 3. Send to Backend
        await signalRService.sendChatMessage('Commander', text);
    }
  }
};

onMounted(async () => {
  signalRService.onReceiveChatMessage((user: string, text: string, duration?: number) => {
    // Deduplicate: If we just sent this exact message as 'Commander', don't show it again
    if (user === 'Commander') {
        const lastMsg = messages.value[messages.value.length - 2]; // Check 2nd to last (since last is 'Processing')
        if (lastMsg && lastMsg.user === 'Commander' && lastMsg.text === text) {
            return;
        }
        // Also check last message just in case processing hasn't appeared or was cleared
        const veryLast = messages.value[messages.value.length - 1];
        if (veryLast && veryLast.user === 'Commander' && veryLast.text === text) {
            return;
        }
    }

    // We no longer remove "Processing..." messages here to let them stay in history
    
    const cleanedText = user === 'Mission Control' ? stripMarkdown(text) : text;
    
    messages.value.push({
      user,
      text: cleanedText,
      isSystem: user === 'Mission Control',
      duration
    });
    
    // Trigger voice for Mission Control messages
    if (user === 'Mission Control') {
      speak(text);
    }
    
    scrollToBottom();
  });
});

const sendMessage = async () => {
  if (!newMessage.value.trim()) return;
  const userMsg = newMessage.value;
  newMessage.value = '';
  
  // 1. Show User Message
  messages.value.push({ user: 'Commander', text: userMsg, isSystem: false });

  // 2. Show Processing
  messages.value.push({ user: 'Mission Control', text: `Processing ${userMsg}...`, isSystem: true, isTransient: true });
  scrollToBottom();
  await speakImmediate(`Processing ${userMsg}...`);
  
  await signalRService.sendChatMessage('Commander', userMsg);
};
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 text-white">
    
    <!-- Header (Integrated) -->
    <div class="flex items-center justify-between px-5 py-4 border-b border-white/5 bg-black/10">
      <div class="flex items-center gap-3">
        <div class="w-1.5 h-1.5 rounded-full bg-primary animate-pulse shadow-[0_0_8px_rgba(var(--p),0.8)]"></div>
        <span class="text-sm font-black tracking-[0.2em] text-primary uppercase">Mission Chat</span>
      </div>
      
      <!-- Audio Toggle -->
      <div class="flex items-center gap-2">
        <span v-if="voiceStatus === 'error'" class="text-[10px] text-red-500 font-bold tracking-wider animate-pulse" title="System voice missing">
          NO AUDIO
        </span>
        
        <button 
          @click="toggleMute" 
          class="text-primary hover:text-white transition-colors p-1"
          :title="isMuted ? 'Unmute Voice Comms' : 'Mute Voice Comms'"
        >
          <!-- Speaker Icon -->
          <svg v-if="!isMuted" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
          <path stroke-linecap="square" stroke-linejoin="miter" d="M19.114 5.636a9 9 0 010 12.728M16.463 8.288a5.25 5.25 0 010 7.424M6.75 8.25l4.72-4.72a.75.75 0 011.28.53v15.88a.75.75 0 01-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.01 9.01 0 012.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75z" />
        </svg>
        <!-- Muted Icon -->
        <svg v-else xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4 opacity-50">
          <path stroke-linecap="square" stroke-linejoin="miter" d="M17.25 9.75L19.5 12m0 0l2.25 2.25M19.5 12l2.25-2.25M19.5 12l-2.25 2.25m-10.5-6l4.72-4.72a.75.75 0 011.28.53v15.88a.75.75 0 01-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.01 9.01 0 012.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75z" />
        </svg>
      </button>
    </div>
  </div>
    
    <!-- Chat Content -->
    <div class="flex-1 flex flex-col min-h-0">
        <div ref="chatContainer" class="flex-1 overflow-y-auto px-5 py-4 space-y-4 custom-scrollbar">
            <div v-for="(msg, index) in messages" :key="index" class="flex flex-col gap-1">
                <div class="flex items-center gap-2">
                    <span class="text-[10px] font-black uppercase tracking-widest px-1.5 py-0.5 rounded border border-white/10 bg-white/5" 
                          :class="msg.isSystem ? 'text-primary' : 'text-accent'">
                        {{ msg.user }}
                    </span>
                    <span v-if="msg.duration" class="text-[10px] font-mono font-bold text-accent/60 tracking-wider ml-1">
                        {{ msg.duration.toFixed(2) }}s
                    </span>
                </div>
                <span class="text-sm font-mono font-bold leading-relaxed text-white/90 pl-1">
                    {{ msg.text }}
                </span>
            </div>
        </div>
        
        <!-- Input Area -->
        <div class="p-4 bg-white/5 border-t border-white/5">
            <div class="flex gap-3 items-start bg-black/40 rounded-xl px-3 py-2 border border-white/5 focus-within:border-primary/30 transition-colors">
                <span class="text-primary text-sm font-mono font-black opacity-50 mt-1">&gt;</span>
                
                <!-- Mic Button with Dynamic State -->
                <button 
                    @click="handleMicClick"
                    @mousedown.prevent="handleMicDown" 
                    @mouseup.prevent="handleMicUp" 
                    @mouseleave.prevent="isMicActive ? handleMicUp() : null"
                    class="mt-1 transition-all outline-none"
                    :class="{
                        'text-white/30 cursor-pointer': micState === 'initial',
                        'text-yellow-500 animate-spin': micState === 'checking',
                        'text-primary hover:text-white cursor-pointer': micState === 'ready' && !isMicActive,
                        'text-red-500 animate-pulse': isMicActive,
                        'text-red-700': micState === 'error'
                    }"
                    :title="micState === 'initial' ? 'Click to Initialize Voice' : (micState === 'ready' ? 'Hold to Speak' : 'Initializing...')"
                >
                    <!-- Loading Spinner -->
                    <svg v-if="micState === 'checking'" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
                    </svg>

                    <!-- Error Icon -->
                    <svg v-else-if="micState === 'error'" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
                    </svg>

                    <!-- Mic Icon (Initial/Ready/Recording) -->
                    <svg v-else xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="square" stroke-linejoin="miter" d="M12 18.75a6 6 0 006-6v-1.5m-6 7.5a6 6 0 01-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 01-3-3V4.5a3 3 0 116 0v8.25a3 3 0 01-3 3z" />
                    </svg>
                </button>

                <textarea 
                    v-model="newMessage" 
                    @keydown.enter.exact.prevent="sendMessage"
                    rows="2"
                    class="bg-transparent w-full text-white outline-none placeholder-white/30 text-sm font-mono font-bold resize-none py-1 custom-scrollbar" 
                    :placeholder="isMicActive ? 'RECORDING...' : 'ENTER COMMAND...'" 
                ></textarea>
                <button @click="sendMessage" class="mt-1 text-xs font-black text-primary hover:text-white transition-colors uppercase tracking-widest">SEND</button>
            </div>
        </div>
    </div>
  </div>
</template>

<style scoped>
/* Custom scrollbar for chat */
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.02);
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.2);
}
</style>
