<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { createPricerApi } from '../api/pricerApi.js';
import { config } from '../config.js';
import { useUserSession } from '../state/useUserSession.js';

const router = useRouter();
const userSession = useUserSession();
const api = createPricerApi({
  baseUrl: config.apiBaseUrl,
  getToken: () => userSession.token.value?.trim() || null,
});

const providers = [
  { value: 'all', label: 'Todos los proveedores' },
  { value: 'mercadolibre', label: 'MercadoLibre' },
  { value: 'local', label: 'Proveedor local' },
];

const form = ref({
  query: 'yerba',
  provider: 'all',
  take: 20,
});

const loading = ref(false);
const error = ref('');
const results = ref([]);

const visibleResults = computed(() => results.value || []);

function formatMoney(money) {
  if (!money) return '-';
  return `${money.currency} ${Number(money.amount).toLocaleString('es-AR')}`;
}

function formatDate(value) {
  if (!value) return '-';
  return new Date(value).toLocaleString();
}

function normalizeProvider(value) {
  return value === 'all' ? undefined : value;
}

async function searchProducts() {
  loading.value = true;
  error.value = '';
  try {
    const response = await api.externalProducts.search({
      query: form.value.query?.trim() || undefined,
      provider: normalizeProvider(form.value.provider),
      take: Number(form.value.take) || 20,
    });

    if (!response.ok) {
      error.value = response.error?.message || 'No se pudieron cargar productos.';
      results.value = [];
      return;
    }

    results.value = response.data || [];
  } catch (err) {
    error.value = err?.message || 'Error inesperado.';
    results.value = [];
  } finally {
    loading.value = false;
  }
}

watch(
  () => userSession.isAuthenticated.value,
  (isAuth) => {
    if (!isAuth) {
      router.replace({ name: 'user-login' });
    }
  }
);

onMounted(() => {
  searchProducts();
});
</script>

<template>
  <div class="view products">
    <header class="hero">
      <div class="hero-text">
        <p class="eyebrow">Proveedor de precios</p>
        <h1>Busqueda multi proveedor</h1>
        <p class="subtitle">
          Unificamos resultados de scraping de MercadoLibre y proveedores propios en un solo formato.
        </p>
      </div>
      <div class="hero-card">
        <form class="form" @submit.prevent="searchProducts">
          <label>
            Buscar producto
            <input v-model="form.query" placeholder="Ej: yerba, aceite, arroz" />
          </label>
          <label>
            Proveedor
            <select v-model="form.provider">
              <option v-for="provider in providers" :key="provider.value" :value="provider.value">
                {{ provider.label }}
              </option>
            </select>
          </label>
          <label>
            Resultados
            <input v-model.number="form.take" type="number" min="1" max="100" />
          </label>
          <button type="submit" :disabled="loading">
            {{ loading ? 'Buscando...' : 'Buscar productos' }}
          </button>
        </form>
        <p v-if="error" class="error">{{ error }}</p>
      </div>
    </header>

    <section class="results">
      <div class="results-header">
        <h2>Resultados</h2>
        <p class="subtitle">
          {{ visibleResults.length }} productos encontrados.
        </p>
      </div>
      <div v-if="!visibleResults.length && !loading" class="empty">
        Sin resultados para esta busqueda.
      </div>

      <div class="grid">
        <article v-for="item in visibleResults" :key="item.permalink" class="card">
          <div class="card-media">
            <img :src="item.media.thumbnailUrl" :alt="item.title" />
            <span class="provider">{{ item.provider }}</span>
          </div>
          <div class="card-body">
            <div class="card-title">
              <h3>{{ item.title }}</h3>
              <a class="link" :href="item.permalink" target="_blank" rel="noreferrer">Ver</a>
            </div>
            <p class="meta">
              {{ item.condition || 'Condicion desconocida' }} · {{ item.location || 'Ubicacion no informada' }}
            </p>
            <div class="price-row">
              <span class="price">{{ formatMoney(item.offer.price) }}</span>
              <span v-if="item.offer.originalPrice" class="price-old">
                {{ formatMoney(item.offer.originalPrice) }}
              </span>
              <span v-if="item.offer.discountPercent" class="discount">
                {{ item.offer.discountPercent }}% off
              </span>
            </div>
            <div class="badges">
              <span v-if="item.shipping.freeShipping" class="badge">Envio gratis</span>
              <span v-if="item.offer.pricePerUnit" class="badge ghost">{{ item.offer.pricePerUnit }}</span>
              <span v-if="item.offer.installments?.quantity" class="badge ghost">
                {{ item.offer.installments.quantity }} cuotas
              </span>
            </div>
            <div class="seller">
              <strong>{{ item.seller.name || 'Vendedor' }}</strong>
              <span>{{ item.seller.badges || item.seller.reputationLevel || 'Sin reputacion' }}</span>
            </div>
            <details>
              <summary>Detalle completo</summary>
              <div class="detail-grid">
                <div>
                  <p class="label">Categoria</p>
                  <p>{{ item.categoryPath?.join(' / ') || 'Sin categoria' }}</p>
                </div>
                <div>
                  <p class="label">Disponibilidad</p>
                  <p>{{ item.availability || 'Sin datos' }}</p>
                </div>
                <div>
                  <p class="label">Actualizado</p>
                  <p>{{ formatDate(item.fetchedAtUtc) }}</p>
                </div>
                <div>
                  <p class="label">Proveedor</p>
                  <p>{{ item.source }}</p>
                </div>
              </div>
              <div v-if="item.attributes" class="attributes">
                <span v-for="(value, key) in item.attributes" :key="key">{{ key }}: {{ value }}</span>
              </div>
              <p v-if="item.warnings?.length" class="warning">
                {{ item.warnings.join(' · ') }}
              </p>
            </details>
          </div>
        </article>
      </div>
    </section>
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

.products {
  max-width: 1300px;
  margin: 0 auto;
  padding: clamp(1.6rem, 2.5vw, 3rem) clamp(1.4rem, 4vw, 3rem) 4rem;
  font-family: 'Plus Jakarta Sans', 'Segoe UI', sans-serif;
  display: grid;
  gap: 2.8rem;
}

.hero {
  display: grid;
  gap: 2rem;
  grid-template-columns: minmax(280px, 1.2fr) minmax(280px, 0.9fr);
  align-items: start;
}

.hero-text h1 {
  font-family: 'Fraunces', 'Times New Roman', serif;
  font-size: clamp(2.5rem, 4vw, 3.6rem);
  margin: 0.6rem 0 0.4rem;
}

.hero-card {
  background: #ffffff;
  border-radius: 24px;
  padding: 1.5rem;
  box-shadow: 0 20px 45px rgba(17, 24, 39, 0.12);
  border: 1px solid rgba(15, 118, 110, 0.08);
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

input,
select {
  padding: 0.65rem 0.8rem;
  border-radius: 12px;
  border: 1px solid rgba(15, 118, 110, 0.15);
  font-family: inherit;
}

button {
  padding: 0.75rem 1rem;
  border: none;
  border-radius: 14px;
  background: linear-gradient(135deg, #0f766e, #0b5f58);
  color: white;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 12px 24px rgba(15, 118, 110, 0.2);
}

.results {
  display: grid;
  gap: 1.6rem;
}

.results-header h2 {
  font-family: 'Fraunces', 'Times New Roman', serif;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.6rem;
}

.card {
  background: #ffffff;
  border-radius: 22px;
  overflow: hidden;
  box-shadow: 0 18px 40px rgba(17, 24, 39, 0.1);
  display: grid;
}

.card-media {
  position: relative;
  background: #f2f5f7;
  aspect-ratio: 4 / 3;
  overflow: hidden;
}

.card-media img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.provider {
  position: absolute;
  left: 1rem;
  top: 1rem;
  padding: 0.3rem 0.7rem;
  border-radius: 999px;
  background: rgba(15, 118, 110, 0.9);
  color: white;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.card-body {
  padding: 1.3rem 1.4rem 1.5rem;
  display: grid;
  gap: 0.7rem;
}

.card-title {
  display: flex;
  justify-content: space-between;
  gap: 0.8rem;
}

.card-title h3 {
  font-size: 1.05rem;
  margin: 0;
}

.link {
  color: #0f766e;
  font-weight: 600;
  text-decoration: none;
}

.meta {
  color: #6b7280;
  font-size: 0.85rem;
}

.price-row {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
}

.price {
  font-size: 1.35rem;
  font-weight: 700;
}

.price-old {
  text-decoration: line-through;
  color: #9ca3af;
}

.discount {
  color: #b42318;
  font-weight: 600;
}

.badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.badge {
  padding: 0.3rem 0.6rem;
  border-radius: 999px;
  background: #0f766e;
  color: white;
  font-size: 0.75rem;
}

.badge.ghost {
  background: #e6f4f1;
  color: #0f766e;
}

.seller {
  display: flex;
  justify-content: space-between;
  color: #374151;
  font-size: 0.85rem;
}

details {
  border-top: 1px solid rgba(15, 118, 110, 0.1);
  padding-top: 0.6rem;
}

summary {
  cursor: pointer;
  font-weight: 600;
}

.detail-grid {
  display: grid;
  gap: 0.6rem;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  margin-top: 0.6rem;
  font-size: 0.85rem;
}

.label {
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-size: 0.65rem;
  color: #6b7280;
}

.attributes {
  margin-top: 0.6rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  font-size: 0.8rem;
}

.attributes span {
  padding: 0.3rem 0.6rem;
  border-radius: 999px;
  background: #f3f4f6;
}

.warning {
  margin-top: 0.6rem;
  color: #b42318;
  font-size: 0.8rem;
}

.error {
  color: #b42318;
  margin-top: 0.6rem;
}

.empty {
  color: #6b7280;
  font-size: 0.9rem;
}

@media (max-width: 900px) {
  .hero {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .products {
    padding: 1.2rem 1rem 3rem;
  }
}
</style>
