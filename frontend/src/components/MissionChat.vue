<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue';
import { signalRService } from '../services/SignalRService';

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
    <div class="flex items-center gap-3 px-5 py-4 border-b border-white/5 bg-black/10">
      <div class="w-1.5 h-1.5 rounded-full bg-primary animate-pulse shadow-[0_0_8px_rgba(var(--p),0.8)]"></div>
      <span class="text-sm font-black tracking-[0.2em] text-primary uppercase">Mission Chat</span>
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
