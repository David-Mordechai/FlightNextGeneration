<script setup lang="ts">

const props = defineProps<{
  visible: boolean;
  form: {
    id?: string;
    name: string;
    minAltitude: number;
    maxAltitude: number;
  };
}>();

const emit = defineEmits(['update:visible', 'update:form', 'save', 'cancel', 'delete']);

</script>

<template>
  <div v-if="visible" class="nfz-modal">
    <h3>{{ form.id ? 'Edit' : 'Create' }} No Fly Zone</h3>
    <div class="form-group">
      <label>Name</label>
      <input 
        :value="form.name" 
        @input="$emit('update:form', { ...form, name: ($event.target as HTMLInputElement).value })"
        placeholder="Zone Name" 
      />
    </div>
    <div class="form-group">
      <label>Min Altitude (ft)</label>
      <input 
        type="number" 
        :value="form.minAltitude" 
        @input="$emit('update:form', { ...form, minAltitude: Number(($event.target as HTMLInputElement).value) })"
      />
    </div>
    <div class="form-group">
      <label>Max Altitude (ft)</label>
      <input 
        type="number" 
        :value="form.maxAltitude" 
        @input="$emit('update:form', { ...form, maxAltitude: Number(($event.target as HTMLInputElement).value) })"
      />
    </div>
    <div class="actions">
      <button v-if="form.id" @click="$emit('delete')" class="delete-btn">Delete</button>
      <button @click="$emit('cancel')" class="cancel-btn">Cancel</button>
      <button @click="$emit('save')" class="save-btn">Save</button>
    </div>
  </div>
</template>

<style scoped>
.nfz-modal {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  background: white;
  padding: 20px;
  border-radius: 8px;
  box-shadow: 0 4px 6px rgba(0,0,0,0.1);
  z-index: 1000;
  width: 300px;
}
.form-group {
  margin-bottom: 15px;
}
.form-group label {
  display: block;
  margin-bottom: 5px;
  font-weight: bold;
}
.form-group input {
  width: 100%;
  padding: 8px;
  border: 1px solid #ddd;
  border-radius: 4px;
}
.actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
.save-btn {
  background: #3B82F6;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
}
.cancel-btn {
  background: #E5E7EB;
  color: #374151;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
}
.delete-btn {
  background: #EF4444;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  margin-right: auto;
}
</style>
