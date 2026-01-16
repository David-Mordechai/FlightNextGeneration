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
  <div v-if="localVisible" class="modal modal-open">
    <div class="modal-box bg-base-100 border border-white/10 shadow-2xl">
      <h3 class="font-bold text-lg mb-4 flex items-center gap-2 text-primary">
          <span class="text-2xl">📍</span>
          ADD NEW POINT
      </h3>
      
      <div class="form-control w-full mb-3">
        <label class="label"><span class="label-text">Point Name</span></label>
        <input 
            v-model="localForm.name" 
            type="text" 
            placeholder="e.g. Forward Operating Base" 
            class="input input-bordered w-full" 
        />
      </div>

      <div class="form-control w-full mb-6">
        <label class="label"><span class="label-text">Point Type</span></label>
        <select v-model="localForm.type" class="select select-bordered w-full">
          <option :value="PointType.Home">🏠 Home Base</option>
          <option :value="PointType.Target">🎯 Mission Target</option>
        </select>
      </div>

      <div class="modal-action">
        <button @click="handleCancel" class="btn btn-ghost">Cancel</button>
        <button @click="handleSave" class="btn btn-primary">Save Point</button>
      </div>
    </div>
  </div>
</template>
