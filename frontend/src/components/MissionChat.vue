<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue';
import { signalRService } from '../services/SignalRService';

interface Message {
  user: string;
  text: string;
  isSystem: boolean;
}

const messages = ref<Message[]>([]);
const newMessage = ref('');
const chatContainer = ref<HTMLElement | null>(null);
const isOpen = ref(true);

const scrollToBottom = async () => {
  await nextTick();
  if (chatContainer.value) {
    chatContainer.value.scrollTop = chatContainer.value.scrollHeight;
  }
};

onMounted(() => {
  signalRService.onReceiveChatMessage((user: string, text: string) => {
    messages.value.push({
      user,
      text,
      isSystem: user === 'Mission Control'
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
  <div class="bg-base-100/90 backdrop-blur-xl rounded-2xl border border-white/10 shadow-2xl overflow-hidden flex flex-col text-white transition-all duration-300" 
       :style="{ height: isOpen ? '400px' : '48px' }">
    
    <!-- Header -->
    <div @click="isOpen = !isOpen" class="flex justify-between items-center px-5 py-3 cursor-pointer hover:bg-white/5 transition-colors">
      <div class="flex items-center gap-3">
        <div class="w-1.5 h-1.5 rounded-full bg-primary animate-pulse shadow-[0_0_8px_rgba(var(--p),0.8)]"></div>
        <span class="text-[9px] font-black tracking-[0.2em] text-primary uppercase">MISSION CONTROL</span>
      </div>
      <span class="text-[9px] font-bold uppercase tracking-widest text-white/40">{{ isOpen ? 'Minimize' : 'Expand' }}</span>
    </div>
    
    <!-- Chat Content -->
    <div v-show="isOpen" class="flex-1 flex flex-col min-h-0">
        <div ref="chatContainer" class="flex-1 overflow-y-auto px-5 py-2 space-y-4 custom-scrollbar">
            <div v-for="(msg, index) in messages" :key="index" class="flex flex-col gap-1">
                <div class="flex items-center">
                    <span class="text-[8px] font-black uppercase tracking-widest px-1.5 py-0.5 rounded border border-white/10 bg-white/5" 
                          :class="msg.isSystem ? 'text-primary' : 'text-accent'">
                        {{ msg.user }}
                    </span>
                </div>
                <span class="text-[11px] font-mono font-bold leading-relaxed text-white/90 pl-1">
                    {{ msg.text }}
                </span>
            </div>
        </div>
        
        <!-- Input Area -->
        <div class="p-4 bg-white/5 border-t border-white/5">
            <div class="flex gap-3 items-center bg-black/40 rounded-xl px-3 py-2 border border-white/5 focus-within:border-primary/30 transition-colors">
                <span class="text-primary text-xs font-mono font-black opacity-50">&gt;</span>
                <input 
                    v-model="newMessage" 
                    @keyup.enter="sendMessage"
                    class="bg-transparent w-full text-white outline-none placeholder-white/30 text-[11px] font-mono font-bold" 
                    placeholder="ENTER COMMAND..." 
                />
                <button @click="sendMessage" class="text-[9px] font-black text-primary hover:text-white transition-colors uppercase tracking-widest">SEND</button>
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
