<script setup>
const props = defineProps({
  loading: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: '',
  },
});

const emit = defineEmits(['submit']);

const form = defineModel('form', { type: Object, required: true });

function onSubmit() {
  emit('submit');
}
</script>

<template>
  <section class="login-card">
    <div class="login-hero">
      <p class="eyebrow">Comerciante</p>
      <h1>Ingresa a tu panel</h1>
      <p class="subtitle">Administra tus comercios y productos.</p>
    </div>

    <form class="form" @submit.prevent="onSubmit">
      <label>
        Usuario
        <input v-model="form.username" autocomplete="username" />
      </label>
      <label>
        Contrasena
        <input v-model="form.password" type="password" autocomplete="current-password" />
      </label>
      <button type="submit" :disabled="loading">
        {{ loading ? 'Validando...' : 'Entrar' }}
      </button>
    </form>
    <p v-if="error" class="error">{{ error }}</p>
  </section>
</template>

<style scoped>
.login-card {
  width: min(420px, 90vw);
  padding: 2rem;
  border-radius: 24px;
  background: var(--surface);
  box-shadow: 0 30px 70px rgba(17, 24, 39, 0.14);
  display: grid;
  gap: 1.5rem;
  animation: float-in 0.7s ease both;
}

.login-hero {
  display: grid;
  gap: 0.4rem;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.18em;
  font-size: 0.7rem;
  color: var(--accent);
  font-weight: 700;
}

.subtitle {
  color: var(--muted);
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

.error {
  color: var(--warn);
}
</style>
