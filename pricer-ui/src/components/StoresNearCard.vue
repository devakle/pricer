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
  results: {
    type: Array,
    default: () => [],
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
      <h2>Buscar tiendas cercanas</h2>
      <button type="button" class="ghost" @click="onSeed">Cargar ejemplo</button>
    </div>
    <form class="form" @submit.prevent="onSubmit">
      <label>
        Lat
        <input
          :value="form.lat"
          type="number"
          step="0.0001"
          @input="updateField('lat', Number($event.target.value))"
        />
      </label>
      <label>
        Lng
        <input
          :value="form.lng"
          type="number"
          step="0.0001"
          @input="updateField('lng', Number($event.target.value))"
        />
      </label>
      <label>
        Radio (km)
        <input
          :value="form.radiusKm"
          type="number"
          step="0.1"
          min="0.1"
          @input="updateField('radiusKm', Number($event.target.value))"
        />
      </label>
      <label>
        Take
        <input
          :value="form.take"
          type="number"
          min="1"
          @input="updateField('take', Number($event.target.value))"
        />
      </label>
      <button type="submit" :disabled="loading">
        {{ loading ? 'Buscando...' : 'Buscar' }}
      </button>
    </form>
    <p v-if="error" class="error">{{ error }}</p>
    <div v-if="results.length" class="results">
      <div v-for="store in results" :key="store.storeId" class="result-row">
        <div>
          <strong>{{ store.name }}</strong>
          <span v-if="store.chainName">- {{ store.chainName }}</span>
        </div>
        <div class="meta">
          <span>{{ store.city }}</span>
          <span class="mono">{{ store.distanceMeters.toFixed(0) }} m</span>
        </div>
      </div>
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

.results {
  margin-top: 1.2rem;
  display: grid;
  gap: 0.8rem;
}

.result-row {
  padding: 0.8rem;
  border-radius: 12px;
  background: #f4f8f7;
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.meta {
  display: flex;
  gap: 0.8rem;
  color: var(--muted);
}

.error {
  color: var(--warn);
  margin-top: 0.8rem;
}
</style>
