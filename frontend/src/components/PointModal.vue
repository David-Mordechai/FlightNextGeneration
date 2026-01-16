<script setup lang="ts">
import { computed } from 'vue';
import { PointType } from '../services/C4IService';

const props = defineProps<{
  visible: boolean;
  form: {
    name: string;
    type: PointType;
  };
}>();

const emit = defineEmits(['update:visible', 'update:form', 'save', 'cancel']);

const localVisible = computed({
  get: () => props.visible,
  set: (val) => emit('update:visible', val)
});

const localForm = computed({
  get: () => props.form,
  set: (val) => emit('update:form', val)
});

const handleSave = () => {
  emit('save');
};

const handleCancel = () => {
  emit('cancel');
};
</script>

<template>
  <div v-if="localVisible" class="modal-overlay">
    <div class="modal-content">
      <h3>Add Point</h3>
      
      <div class="form-group">
        <label>Name:</label>
        <input v-model="localForm.name" type="text" placeholder="Enter point name" />
      </div>

      <div class="form-group">
        <label>Type:</label>
        <select v-model="localForm.type">
          <option :value="PointType.Home">Home</option>
          <option :value="PointType.Target">Target</option>
        </select>
      </div>

      <div class="modal-actions">
        <button @click="handleCancel">Cancel</button>
        <button class="primary" @click="handleSave">Save</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0; left: 0;
  width: 100%; height: 100%;
  background: rgba(0,0,0,0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 2000;
}

.modal-content {
  background: #1e1e1e;
  padding: 20px;
  border-radius: 8px;
  min-width: 300px;
  color: white;
}

.form-group {
  margin-bottom: 15px;
}

label {
  display: block;
  margin-bottom: 5px;
}

input, select {
  width: 100%;
  padding: 8px;
  background: #2d2d2d;
  border: 1px solid #3d3d3d;
  color: white;
  border-radius: 4px;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}

button {
  padding: 8px 16px;
  border-radius: 4px;
  border: none;
  cursor: pointer;
}

button.primary {
  background: #3B82F6;
  color: white;
}
</style>
