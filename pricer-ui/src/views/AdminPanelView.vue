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
.admin-shell {
  width: 100%;
}

.hero {
  display: flex;
  flex-wrap: wrap;
  gap: 2rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 2.5rem;
}

.hero-actions {
  display: grid;
  gap: 1rem;
  justify-items: end;
}

.grid {
  display: grid;
  gap: 2rem;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
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

.logout {
  width: fit-content;
}
</style>
