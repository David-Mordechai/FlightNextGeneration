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
  <div v-if="visible" class="modal modal-open">
    <div class="modal-box bg-base-100 border border-white/10 shadow-2xl">
      <h3 class="font-bold text-lg mb-4 flex items-center gap-2 text-error">
          <span class="text-2xl">🛑</span>
          {{ form.id ? 'EDIT' : 'CREATE' }} NO-FLY ZONE
      </h3>
      
      <div class="form-control w-full mb-3">
        <label class="label"><span class="label-text">Zone Name</span></label>
        <input 
            :value="form.name" 
            @input="$emit('update:form', { ...form, name: ($event.target as HTMLInputElement).value })"
            type="text" 
            placeholder="e.g. Restricted Area 51" 
            class="input input-bordered w-full" 
        />
      </div>

      <div class="grid grid-cols-2 gap-4 mb-6">
          <div class="form-control w-full">
            <label class="label"><span class="label-text">Min Alt (ft)</span></label>
            <input 
                type="number" 
                :value="form.minAltitude" 
                @input="$emit('update:form', { ...form, minAltitude: Number(($event.target as HTMLInputElement).value) })"
                class="input input-bordered w-full font-mono" 
            />
          </div>
          <div class="form-control w-full">
            <label class="label"><span class="label-text">Max Alt (ft)</span></label>
            <input 
                type="number" 
                :value="form.maxAltitude" 
                @input="$emit('update:form', { ...form, maxAltitude: Number(($event.target as HTMLInputElement).value) })"
                class="input input-bordered w-full font-mono" 
            />
          </div>
      </div>

      <div class="modal-action">
        <button v-if="form.id" @click="$emit('delete')" class="btn btn-error btn-outline mr-auto">Delete</button>
        <button @click="$emit('cancel')" class="btn btn-ghost">Cancel</button>
        <button @click="$emit('save')" class="btn btn-primary">Save Zone</button>
      </div>
    </div>
  </div>
</template>
