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
  <div class="collapse collapse-arrow bg-gray-900 text-green-400 border border-gray-700 shadow-2xl rounded-none font-mono" :class="{'collapse-open': isOpen, 'collapse-close': !isOpen}">
    <!-- Removed checkbox, using manual toggle -->
    <div @click="isOpen = !isOpen" class="collapse-title text-lg font-bold flex items-center gap-3 bg-gray-800 cursor-pointer text-green-500">
        <div class="w-2 h-2 rounded-full bg-red-500 animate-pulse"></div>
        <span>COMM_LINK</span>
    </div>
    
    <div class="collapse-content p-0 flex flex-col bg-gray-900" :style="{ maxHeight: isOpen ? '400px' : '0' }">
        <div ref="chatContainer" class="flex-1 overflow-y-auto p-4 space-y-3 h-[350px]">
            <div v-for="(msg, index) in messages" :key="index" class="flex flex-col">
                <span class="text-[10px] uppercase opacity-50 mb-0.5" :class="msg.isSystem ? 'text-yellow-500' : 'text-blue-400'">
                    [{{ msg.user }}]
                </span>
                <span class="text-sm leading-tight text-gray-300">
                    {{ msg.text }}
                </span>
            </div>
        </div>
        
        <div class="p-2 bg-gray-800 border-t border-gray-700">
            <div class="flex gap-2">
                <span class="text-green-500 py-1">&gt;</span>
                <input 
                    v-model="newMessage" 
                    @keyup.enter="sendMessage"
                    class="bg-transparent w-full text-green-400 outline-none placeholder-gray-600 text-sm font-mono" 
                    placeholder="ENTER COMMAND..." 
                />
                <button @click="sendMessage" class="text-xs bg-gray-700 hover:bg-gray-600 px-2 py-1 text-white uppercase font-bold border border-gray-600">TX</button>
            </div>
        </div>
    </div>
  </div>
</template>

<style scoped>
/* Custom scrollbar for chat */
.collapse-content > div::-webkit-scrollbar {
  width: 4px;
}
.collapse-content > div::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
}
</style>
