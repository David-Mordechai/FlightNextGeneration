<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue';
import { signalRService } from '../services/SignalRService';
import { useVoiceComms } from '../composables/useVoiceComms';

const { speak, toggleMute, isMuted, voiceStatus } = useVoiceComms();

interface Message {
  user: string;
  text: string;
  isSystem: boolean;
  duration?: number; // Duration in seconds
}

const messages = ref<Message[]>([]);
const newMessage = ref('');
const chatContainer = ref<HTMLElement | null>(null);

const scrollToBottom = async () => {
  await nextTick();
  if (chatContainer.value) {
    chatContainer.value.scrollTop = chatContainer.value.scrollHeight;
  }
};

onMounted(() => {
  signalRService.onReceiveChatMessage((user: string, text: string, duration?: number) => {
    messages.value.push({
      user,
      text,
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
        <span v-if="voiceStatus === 'error'" class="text-[10px] text-red-500 font-bold tracking-wider animate-pulse" title="System voice missing. Run: sudo apt install speech-dispatcher">
          NO AUDIO DRIVER
        </span>
        
        <!-- Test Button -->
        <button 
          @click="speak('The speakers are working.')" 
          class="text-primary/60 hover:text-white transition-colors p-1"
          title="Test Audio Output"
        >
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
            <path stroke-linecap="square" stroke-linejoin="miter" d="M11.42 15.17L17.25 21A2.652 2.652 0 0021 17.25l-5.83-5.83m0 0a2.984 2.984 0 000-4.242L14.75 6.75m0 0l-1.06-1.06a1.5 1.5 0 010-2.122l.53-.53a1.5 1.5 0 012.122 0l1.06 1.06a1.5 1.5 0 010 2.122l-.53.53a1.5 1.5 0 01-2.122 0l1.06-1.06zm-4.242 4.242l-4.243 4.243a1.5 1.5 0 01-2.122 0l-1.06-1.06a1.5 1.5 0 010-2.122l4.243-4.243a1.5 1.5 0 012.122 0l1.06 1.06a1.5 1.5 0 010 2.122l-1.06-1.06z" />
          </svg>
        </button>

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
                <textarea 
                    v-model="newMessage" 
                    @keydown.enter.exact.prevent="sendMessage"
                    rows="2"
                    class="bg-transparent w-full text-white outline-none placeholder-white/30 text-sm font-mono font-bold resize-none py-1 custom-scrollbar" 
                    placeholder="ENTER COMMAND..." 
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
