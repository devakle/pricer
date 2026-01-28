<script setup>
const props = defineProps({
  modelValue: {
    type: String,
    default: '',
  },
});

const emit = defineEmits(['clear']);

function onCopy() {
  if (!props.modelValue) return;
  navigator.clipboard.writeText(props.modelValue);
}

function onClear() {
  emit('clear');
}
</script>

<template>
  <div class="token-card">
    <label for="token">JWT actual</label>
    <input id="token" :value="modelValue" readonly />
    <div class="token-actions">
      <button type="button" class="ghost" @click="onCopy">Copiar</button>
      <button type="button" class="ghost" @click="onClear">Limpiar</button>
    </div>
    <p class="hint">Se guarda en el navegador y se agrega al header Authorization.</p>
  </div>
</template>

<style scoped>
.token-card {
  flex: 1 1 280px;
  max-width: 360px;
  background: var(--surface);
  border-radius: 16px;
  padding: 1.25rem;
  box-shadow: 0 18px 40px rgba(19, 17, 34, 0.08);
  animation: float-in 0.7s ease both;
}

label {
  font-weight: 600;
  display: block;
  margin-bottom: 0.5rem;
}

input {
  width: 100%;
  padding: 0.65rem 0.75rem;
  border-radius: 10px;
  border: 1px solid var(--stroke);
}

.token-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.6rem;
}

.hint {
  font-size: 0.8rem;
  color: var(--muted);
  margin-top: 0.5rem;
}

.ghost {
  background: transparent;
  color: var(--accent);
  border: 1px solid var(--accent);
  padding: 0.4rem 0.85rem;
  border-radius: 999px;
  font-weight: 600;
  font-size: 0.85rem;
}
</style>
