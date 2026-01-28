<script setup>
const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: '',
  },
  result: {
    type: Object,
    default: null,
  },
});

const emit = defineEmits(['submit', 'update', 'seed']);

function updateField(field, value) {
  emit('update', { field, value });
}

function onSubmit() {
  emit('submit');
}

function onSeed() {
  emit('seed');
}
</script>

<template>
  <section class="card">
    <div class="card-header">
      <h2>Crear reporte de precio</h2>
      <button type="button" class="ghost" @click="onSeed">Cargar ejemplo</button>
    </div>
    <form class="form" @submit.prevent="onSubmit">
      <label>
        StoreId
        <input :value="form.storeId" placeholder="GUID tienda" @input="updateField('storeId', $event.target.value)" />
      </label>
      <label>
        SkuId
        <input :value="form.skuId" placeholder="GUID sku" @input="updateField('skuId', $event.target.value)" />
      </label>
      <label>
        Precio
        <input
          :value="form.price"
          type="number"
          step="0.01"
          @input="updateField('price', Number($event.target.value))"
        />
      </label>
      <label>
        Moneda
        <input :value="form.currency" @input="updateField('currency', $event.target.value)" />
      </label>
      <label>
        Fuente
        <input :value="form.source" @input="updateField('source', $event.target.value)" />
      </label>
      <label>
        Evidencia (URL)
        <input
          :value="form.evidenceUrl"
          placeholder="https://..."
          @input="updateField('evidenceUrl', $event.target.value)"
        />
      </label>
      <button type="submit" :disabled="loading">
        {{ loading ? 'Enviando...' : 'Crear reporte' }}
      </button>
    </form>
    <p v-if="error" class="error">{{ error }}</p>
    <div v-if="result" class="success">
      <p>Reporte creado:</p>
      <p class="mono">{{ result.reportId }}</p>
      <p class="mono">{{ result.reportedAt }}</p>
    </div>
  </section>
</template>

<style scoped>
.card {
  background: var(--surface);
  border-radius: 20px;
  padding: 1.75rem;
  box-shadow: 0 20px 40px rgba(17, 24, 39, 0.08);
  backdrop-filter: blur(6px);
  animation: float-in 0.7s ease both;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.form {
  display: grid;
  gap: 0.9rem;
}

label {
  display: grid;
  gap: 0.35rem;
  font-weight: 600;
}

input {
  padding: 0.6rem 0.7rem;
  border-radius: 10px;
  border: 1px solid var(--stroke);
  font-size: 0.95rem;
}

button {
  padding: 0.7rem 1rem;
  border: none;
  border-radius: 12px;
  background: var(--accent);
  color: white;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.2s ease;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

button:not(:disabled):hover {
  transform: translateY(-1px);
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

.error {
  color: var(--warn);
  margin-top: 0.8rem;
}

.success {
  margin-top: 1rem;
  padding: 0.9rem;
  border-radius: 12px;
  background: var(--accent-soft);
  color: #065f46;
}
</style>
