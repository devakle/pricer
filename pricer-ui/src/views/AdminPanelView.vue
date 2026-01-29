<script setup>
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import AuthTokenCard from '../components/AuthTokenCard.vue';
import StoresNearCard from '../components/StoresNearCard.vue';
import PriceReportCard from '../components/PriceReportCard.vue';
import { createPricerApi } from '../api/pricerApi.js';
import { config } from '../config.js';
import { seedData } from '../seed/seedData.js';
import { useAdminSession } from '../state/useAdminSession.js';

const router = useRouter();
const adminSession = useAdminSession();
const api = createPricerApi({
  baseUrl: config.apiBaseUrl,
  getToken: () => adminSession.token.value?.trim() || null,
});

const nearForm = ref({ ...seedData.near });
const nearLoading = ref(false);
const nearError = ref('');
const nearResults = ref([]);

const priceForm = ref({ ...seedData.priceReport });
const priceLoading = ref(false);
const priceError = ref('');
const priceResult = ref(null);

const apiBaseLabel = computed(() => config.apiBaseUrl);

function updateNearField({ field, value }) {
  nearForm.value = { ...nearForm.value, [field]: value };
}

function updatePriceField({ field, value }) {
  priceForm.value = { ...priceForm.value, [field]: value };
}

function seedNearForm() {
  nearForm.value = { ...seedData.near };
}

function seedPriceForm() {
  priceForm.value = { ...seedData.priceReport };
}

async function fetchStoresNear() {
  nearLoading.value = true;
  nearError.value = '';
  nearResults.value = [];
  try {
    const response = await api.stores.getNear({
      lat: Number(nearForm.value.lat),
      lng: Number(nearForm.value.lng),
      radiusKm: Number(nearForm.value.radiusKm),
      take: Number(nearForm.value.take),
    });

    if (!response.ok) {
      nearError.value = response.error?.message || 'Error al consultar tiendas.';
      return;
    }

    nearResults.value = response.data || [];
  } catch (error) {
    nearError.value = error?.message || 'Error inesperado.';
  } finally {
    nearLoading.value = false;
  }
}

async function createPriceReport() {
  priceLoading.value = true;
  priceError.value = '';
  priceResult.value = null;
  try {
    const response = await api.priceReports.create({
      storeId: priceForm.value.storeId.trim(),
      skuId: priceForm.value.skuId.trim(),
      price: Number(priceForm.value.price),
      currency: priceForm.value.currency.trim(),
      source: priceForm.value.source.trim(),
      evidenceUrl: priceForm.value.evidenceUrl.trim() || null,
    });

    if (!response.ok) {
      priceError.value = response.error?.message || 'Error al crear reporte.';
      return;
    }

    priceResult.value = response.data;
  } catch (error) {
    priceError.value = error?.message || 'Error inesperado.';
  } finally {
    priceLoading.value = false;
  }
}

function logout() {
  adminSession.logout();
  router.replace({ name: 'admin-login' });
}
</script>

<template>
  <div class="view admin-shell">
    <header class="hero">
      <div>
        <p class="eyebrow">Admin</p>
        <h1>Panel de gestion Pricer</h1>
        <p class="subtitle">
          Base URL activa: <span class="mono">{{ apiBaseLabel }}</span>
        </p>
      </div>
      <div class="hero-actions">
        <AuthTokenCard :model-value="adminSession.token.value" @clear="logout" />
        <button type="button" class="ghost logout" @click="logout">Cerrar sesion</button>
      </div>
    </header>

    <main class="grid">
      <StoresNearCard
        :form="nearForm"
        :loading="nearLoading"
        :error="nearError"
        :results="nearResults"
        @submit="fetchStoresNear"
        @update="updateNearField"
        @seed="seedNearForm"
      />

      <PriceReportCard
        :form="priceForm"
        :loading="priceLoading"
        :error="priceError"
        :result="priceResult"
        @submit="createPriceReport"
        @update="updatePriceField"
        @seed="seedPriceForm"
      />
    </main>
  </div>
</template>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Fraunces:wght@300;400;600&family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap');

:global(body) {
  background:
    radial-gradient(circle at 15% 15%, rgba(15, 118, 110, 0.08), transparent 45%),
    radial-gradient(circle at 85% 5%, rgba(251, 191, 36, 0.12), transparent 40%),
    #fbf9f6;
}

.admin-shell {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: clamp(1.6rem, 2.5vw, 3rem) clamp(1.4rem, 4vw, 3rem) 4rem;
  font-family: 'Plus Jakarta Sans', 'Segoe UI', sans-serif;
}

.hero {
  display: grid;
  grid-template-columns: minmax(260px, 1.2fr) minmax(260px, 0.9fr);
  gap: 2rem;
  align-items: start;
  margin-bottom: 2.5rem;
}

.hero-actions {
  display: grid;
  gap: 1rem;
  justify-items: end;
  background: #ffffff;
  border-radius: 22px;
  padding: 1.3rem;
  box-shadow: 0 20px 40px rgba(17, 24, 39, 0.12);
  border: 1px solid rgba(15, 118, 110, 0.08);
}

.grid {
  display: grid;
  gap: 2rem;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
}

.ghost {
  background: transparent;
  color: #0f766e;
  border: 1px solid rgba(15, 118, 110, 0.6);
  padding: 0.5rem 1rem;
  border-radius: 999px;
  font-weight: 600;
  font-size: 0.9rem;
}

.logout {
  width: fit-content;
}

.eyebrow {
  font-size: 0.8rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #6b7280;
}

h1 {
  font-family: 'Fraunces', 'Times New Roman', serif;
  font-size: clamp(2.2rem, 3.5vw, 3.2rem);
  margin: 0.5rem 0;
  color: #101828;
}

.subtitle {
  color: #6b7280;
}

@media (max-width: 900px) {
  .hero {
    grid-template-columns: 1fr;
  }

  .hero-actions {
    justify-items: start;
  }
}

@media (max-width: 600px) {
  .admin-shell {
    padding: 1.2rem 1rem 3rem;
  }

  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
