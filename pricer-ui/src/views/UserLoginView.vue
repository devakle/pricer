<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import UserLogin from '../components/UserLogin.vue';
import { createPricerApi } from '../api/pricerApi.js';
import { config } from '../config.js';
import { useUserSession } from '../state/useUserSession.js';

const router = useRouter();
const userSession = useUserSession();
const api = createPricerApi({
  baseUrl: config.apiBaseUrl,
  getToken: () => userSession.token.value?.trim() || null,
});

const form = ref({ username: '', password: '' });
const loading = ref(false);
const error = ref('');

async function submitLogin() {
  loading.value = true;
  error.value = '';
  const username = form.value.username.trim();
  const password = form.value.password.trim();

  if (!username || !password) {
    error.value = 'Ingresa usuario y contrasena.';
    loading.value = false;
    return;
  }

  try {
    const response = await api.auth.userLogin({ username, password });
    if (!response.ok) {
      error.value = response.error?.message || 'Credenciales invalidas.';
      return;
    }

    userSession.login(response.data.token, response.data.expiresAt);
    form.value = { username: '', password: '' };
    router.replace({ name: 'home' });
  } catch (err) {
    error.value = err?.message || 'Error inesperado.';
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="view login-shell">
    <UserLogin v-model:form="form" :loading="loading" :error="error" @submit="submitLogin" />
  </div>
</template>
