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
  // Listen for chat messages from SignalR (for synchronization)
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

const toggleChat = () => {
  isOpen.value = !isOpen.value;
};
</script>

<template>
  <div class="mission-chat" :class="{ 'is-closed': !isOpen }">
    <div class="chat-header" @click="toggleChat">
      <div class="title-area">
        <div class="status-indicator"></div>
        <span class="title">MISSION CONTROL</span>
      </div>
      <span class="toggle-icon">{{ isOpen ? '−' : '+' }}</span>
    </div>

    <div v-show="isOpen" class="chat-body">
      <div ref="chatContainer" class="messages-container">
        <div v-for="(msg, index) in messages" :key="index" class="message"
          :class="{ 'system-msg': msg.isSystem, 'user-msg': !msg.isSystem }">
          <span class="user">{{ msg.user }}:</span>
          <span class="text">{{ msg.text }}</span>
        </div>
      </div>

      <div class="input-area">
        <input v-model="newMessage" @keyup.enter="sendMessage" placeholder="Enter command..." type="text" />
        <button @click="sendMessage">SEND</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.mission-chat {
  position: fixed;
  bottom: 20px;
  right: 20px;
  width: 320px;
  background: rgba(17, 24, 39, 0.75);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  color: #fff;
  font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  z-index: 99999;
  display: flex;
  flex-direction: column;
  transition: all 0.3s ease;
  overflow: hidden;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
}

.is-closed {
  height: 48px;
}

.chat-header {
  padding: 12px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
}

.title-area {
  display: flex;
  align-items: center;
}

.status-indicator {
  width: 8px;
  height: 8px;
  background-color: #3B82F6;
  /* Blue for mission control */
  border-radius: 50%;
  margin-right: 10px;
  box-shadow: 0 0 8px #3B82F6;
}

.title {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  letter-spacing: 0.5px;
  text-transform: uppercase;
}

.chat-body {
  height: 350px;
  display: flex;
  flex-direction: column;
}

.messages-container {
  flex: 1;
  padding: 12px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.message {
  font-size: 0.9rem;
  line-height: 1.4;
}

.user {
  font-weight: 600;
  margin-right: 6px;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.user-msg .user {
  color: #60A5FA;
}

.system-msg .user {
  color: #10B981;
}

.text {
  word-break: break-word;
  color: #E5E7EB;
}

.input-area {
  padding: 16px;
  display: flex;
  gap: 8px;
  background: rgba(0, 0, 0, 0.2);
  border-top: 1px solid rgba(255, 255, 255, 0.05);
}

input {
  flex: 1;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  padding: 8px 12px;
  color: #fff;
  font-size: 0.9rem;
}

input:focus {
  outline: none;
  border-color: #3B82F6;
}

button {
  background: #2563EB;
  border: none;
  border-radius: 6px;
  color: #fff;
  padding: 0 16px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.75rem;
  letter-spacing: 0.5px;
}

button:hover {
  background: #1D4ED8;
}

/* Scrollbar */
.messages-container::-webkit-scrollbar {
  width: 4px;
}

.messages-container::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
}
</style>
