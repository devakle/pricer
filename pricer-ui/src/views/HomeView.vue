<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import L from 'leaflet';
import { createPricerApi } from '../api/pricerApi.js';
import { config } from '../config.js';
import { useUserSession } from '../state/useUserSession.js';
import AuthTokenCard from '../components/AuthTokenCard.vue';

const router = useRouter();
const userSession = useUserSession();
const api = createPricerApi({
  baseUrl: config.apiBaseUrl,
  getToken: () => userSession.token.value?.trim() || null,
});

const currentLocation = ref({ lat: -31.4201, lng: -64.1888 });

const form = ref({
  lat: -31.4201,
  lng: -64.1888,
  radiusKm: 2,
  take: 40,
});

const loading = ref(false);
const error = ref('');
const stores = ref([]);
const selectedStoreId = ref('');

const productQuery = ref('');
const productResults = ref([]);
const selectedProductId = ref('');
const skusByProduct = ref([]);
const selectedSkuId = ref('');
const priceInput = ref(0);
const submitLoading = ref(false);
const submitError = ref('');
const submitSuccess = ref('');
const catalogLoading = ref(false);
const catalogError = ref('');
const priceReports = ref([]);
const storeProducts = ref([]);
const storeProductsLoading = ref(false);
const storeProductsError = ref('');
const storeSort = ref('recent');
const storeCurrency = ref('');
const storePage = ref(1);
const storePageSize = ref(6);
const storeBrand = ref('');
const storeCategory = ref('');
const historyModalOpen = ref(false);
const historyLoading = ref(false);
const historyError = ref('');
const historyData = ref([]);

const selectedStore = computed(() =>
  stores.value.find((store) => store.storeId === selectedStoreId.value)
);

const selectedProduct = computed(() =>
  productResults.value.find((product) => product.productId === selectedProductId.value)
);

const selectedSku = computed(() =>
  skusByProduct.value.find((sku) => sku.skuId === selectedSkuId.value)
);

const selectedStoreProduct = computed(() =>
  storeProducts.value.find((item) => item.skuId === selectedSkuId.value)
);

const sortedStoreProducts = computed(() => {
  const items = [...storeProducts.value];
  switch (storeSort.value) {
    case 'priceAsc':
      return items.sort((a, b) => Number(a.price) - Number(b.price));
    case 'priceDesc':
      return items.sort((a, b) => Number(b.price) - Number(a.price));
    case 'name':
      return items.sort((a, b) => a.skuDisplayName.localeCompare(b.skuDisplayName));
    case 'recent':
    default:
      return items.sort((a, b) => new Date(b.reportedAt) - new Date(a.reportedAt));
  }
});

const pagedStoreProducts = computed(() => {
  const start = (storePage.value - 1) * storePageSize.value;
  return sortedStoreProducts.value.slice(start, start + storePageSize.value);
});

const mapRef = ref(null);
const mapInstance = ref(null);
const markersLayer = ref(null);
const centerLayer = ref(null);

const mapCenter = computed(() => [Number(form.value.lat), Number(form.value.lng)]);
const radiusMeters = computed(() => Math.max(0.2, Number(form.value.radiusKm) || 0.2) * 1000);

const priceMap = computed(() => {
  const map = new Map();
  priceReports.value.forEach((report) => {
    map.set(report.storeId, report);
  });
  return map;
});

const priceRange = computed(() => {
  if (!priceReports.value.length) return { min: 0, max: 0 };
  const prices = priceReports.value.map((r) => Number(r.price));
  return {
    min: Math.min(...prices),
    max: Math.max(...prices),
  };
});

function priceToColor(price) {
  if (!priceReports.value.length) return '#64748b';
  const { min, max } = priceRange.value;
  if (min === max) return '#0f766e';
  const ratio = (price - min) / (max - min);
  const hue = 120 - ratio * 120;
  return `hsl(${hue}, 65%, 45%)`;
}

function setupLeafletIcons() {
  L.Icon.Default.mergeOptions({
    iconRetinaUrl: new URL('leaflet/dist/images/marker-icon-2x.png', import.meta.url).toString(),
    iconUrl: new URL('leaflet/dist/images/marker-icon.png', import.meta.url).toString(),
    shadowUrl: new URL('leaflet/dist/images/marker-shadow.png', import.meta.url).toString(),
  });
}

function initMap() {
  if (!mapRef.value || mapInstance.value) return;

  setupLeafletIcons();

  mapInstance.value = L.map(mapRef.value, {
    center: mapCenter.value,
    zoom: 13,
    zoomControl: false,
  });

  L.control.zoom({ position: 'bottomright' }).addTo(mapInstance.value);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap',
  }).addTo(mapInstance.value);

  markersLayer.value = L.layerGroup().addTo(mapInstance.value);
  centerLayer.value = L.layerGroup().addTo(mapInstance.value);

  renderCenter();
}

function renderCenter() {
  if (!mapInstance.value || !centerLayer.value) return;
  centerLayer.value.clearLayers();
  L.circle(mapCenter.value, {
    radius: radiusMeters.value,
    color: '#0f766e',
    weight: 2,
    fillColor: '#d1fae5',
    fillOpacity: 0.35,
  }).addTo(centerLayer.value);

  L.circleMarker([currentLocation.value.lat, currentLocation.value.lng], {
    radius: 7,
    color: '#f97316',
    fillColor: '#f97316',
    fillOpacity: 0.9,
  }).addTo(centerLayer.value);
}

function renderMarkers() {
  if (!mapInstance.value || !markersLayer.value) return;
  markersLayer.value.clearLayers();

  stores.value.forEach((store) => {
    const report = priceMap.value.get(store.storeId);
    const color = report ? priceToColor(Number(report.price)) : '#64748b';
    const marker = L.circleMarker([store.lat, store.lng], {
      radius: report ? 10 : 7,
      color,
      fillColor: color,
      fillOpacity: 0.85,
    });
    marker.on('click', () => {
      selectStore(store.storeId);
    });
    const image = report?.imageUrl
      ? `<img src="${report.imageUrl}" alt="product" />`
      : '';
    const tooltip = report
      ? `<div class="tooltip">${image}<div><strong>${store.name}</strong><span>${report.currency} ${report.price}</span></div></div>`
      : `<div class="tooltip"><strong>${store.name}</strong></div>`;
    marker.bindTooltip(tooltip, {
      direction: 'top',
      offset: [0, -6],
      className: 'price-tooltip',
      opacity: 0.95,
    });
    marker.addTo(markersLayer.value);
  });
}

function selectStore(storeId) {
  if (selectedStoreId.value === storeId) {
    loadStoreProducts();
    return;
  }
  selectedStoreId.value = storeId;
}

function fitToResults() {
  if (!mapInstance.value) return;
  if (!stores.value.length) {
    mapInstance.value.setView(mapCenter.value, 13);
    return;
  }

  const group = L.featureGroup(
    stores.value.map((store) => L.marker([store.lat, store.lng]))
  );
  mapInstance.value.fitBounds(group.getBounds().pad(0.25));
}

async function searchStores() {
  loading.value = true;
  error.value = '';
  stores.value = [];
  selectedStoreId.value = '';
  try {
    const response = await api.stores.getNear({
      lat: Number(form.value.lat),
      lng: Number(form.value.lng),
      radiusKm: Number(form.value.radiusKm),
      take: Number(form.value.take),
    });

    if (!response.ok) {
      error.value = response.error?.message || 'No se pudo cargar el mapa.';
      return;
    }

    stores.value = response.data || [];
    renderMarkers();
    fitToResults();
    await loadPriceReports();
  } catch (err) {
    error.value = err?.message || 'Error inesperado.';
  } finally {
    loading.value = false;
  }
}

let searchTimer = null;

async function searchCatalog() {
  const term = productQuery.value.trim();
  if (!term) {
    productResults.value = [];
    selectedProductId.value = '';
    skusByProduct.value = [];
    selectedSkuId.value = '';
    return;
  }
  catalogLoading.value = true;
  catalogError.value = '';
  try {
    const response = await api.catalog.searchProducts({ query: term, take: 25 });
    if (!response.ok) {
      catalogError.value = response.error?.message || 'No se pudo buscar productos.';
      return;
    }
    productResults.value = response.data || [];
  } catch (err) {
    catalogError.value = err?.message || 'Error inesperado.';
  } finally {
    catalogLoading.value = false;
  }
}

async function loadSkusByProduct() {
  skusByProduct.value = [];
  selectedSkuId.value = '';
  if (!selectedProductId.value) return;

  try {
    const response = await api.catalog.getSkusByProduct(selectedProductId.value);
    if (!response.ok) {
      catalogError.value = response.error?.message || 'No se pudieron cargar SKUs.';
      return;
    }
    skusByProduct.value = response.data || [];
  } catch (err) {
    catalogError.value = err?.message || 'Error inesperado.';
  }
}

async function loadPriceReports() {
  priceReports.value = [];
  if (!selectedSkuId.value) {
    renderMarkers();
    return;
  }

  try {
    const response = await api.priceReports.getNear({
      skuId: selectedSkuId.value,
      lat: Number(form.value.lat),
      lng: Number(form.value.lng),
      radiusKm: Number(form.value.radiusKm),
      take: 100,
    });

    if (!response.ok) {
      submitError.value = response.error?.message || 'No se pudieron cargar precios.';
      return;
    }

    priceReports.value = response.data || [];
    renderMarkers();
  } catch (err) {
    submitError.value = err?.message || 'Error inesperado.';
  }
}

async function submitPrice() {
  submitLoading.value = true;
  submitError.value = '';
  submitSuccess.value = '';

  if (!selectedStoreId.value || !selectedSkuId.value || !priceInput.value) {
    submitError.value = 'Completa tienda, producto y precio.';
    submitLoading.value = false;
    return;
  }

  try {
    const response = await api.priceReports.create({
      storeId: selectedStoreId.value,
      skuId: selectedSkuId.value,
      price: Number(priceInput.value),
      currency: 'ARS',
      source: 'user',
      evidenceUrl: null,
    });

    if (!response.ok) {
      submitError.value = response.error?.message || 'No se pudo guardar el precio.';
      return;
    }

    submitSuccess.value = 'Precio guardado correctamente.';
    priceInput.value = 0;
    await loadPriceReports();
  } catch (err) {
    submitError.value = err?.message || 'Error inesperado.';
  } finally {
    submitLoading.value = false;
  }
}

async function openHistory() {
  historyError.value = '';
  historyData.value = [];
  if (!selectedStoreId.value || !selectedSkuId.value) {
    historyError.value = 'Selecciona tienda y producto.';
    historyModalOpen.value = true;
    return;
  }

  historyModalOpen.value = true;
  historyLoading.value = true;
  try {
    const response = await api.priceReports.getHistory({
      storeId: selectedStoreId.value,
      skuId: selectedSkuId.value,
      take: 30,
    });
    if (!response.ok) {
      historyError.value = response.error?.message || 'No se pudo cargar el historial.';
      return;
    }
    historyData.value = response.data || [];
  } catch (err) {
    historyError.value = err?.message || 'Error inesperado.';
  } finally {
    historyLoading.value = false;
  }
}

async function loadStoreProducts() {
  storeProducts.value = [];
  storeProductsError.value = '';

  if (!selectedStoreId.value) return;
  storeProductsLoading.value = true;
  try {
    const response = await api.stores.getProducts(selectedStoreId.value, {
      take: 200,
      skip: 0,
      currency: storeCurrency.value || undefined,
      brand: storeBrand.value || undefined,
      category: storeCategory.value || undefined,
    });
    if (!response.ok) {
      storeProductsError.value = response.error?.message || 'No se pudieron cargar productos.';
      return;
    }
    storeProducts.value = response.data || [];
    storePage.value = 1;
  } catch (err) {
    storeProductsError.value = err?.message || 'Error inesperado.';
  } finally {
    storeProductsLoading.value = false;
  }
}

function simulateUserLocation() {
  const baseLat = -31.4201;
  const baseLng = -64.1888;
  const jitterLat = (Math.random() - 0.5) * 0.01;
  const jitterLng = (Math.random() - 0.5) * 0.01;
  currentLocation.value = {
    lat: Number((baseLat + jitterLat).toFixed(6)),
    lng: Number((baseLng + jitterLng).toFixed(6)),
  };
  form.value.lat = currentLocation.value.lat;
  form.value.lng = currentLocation.value.lng;
}

function logoutUser() {
  userSession.logout();
  router.replace({ name: 'user-login' });
}

watch(mapCenter, () => {
  if (!mapInstance.value) return;
  renderCenter();
});

watch(radiusMeters, () => {
  if (!mapInstance.value) return;
  renderCenter();
});

watch(selectedStore, (store) => {
  if (!store || !mapInstance.value) return;
  mapInstance.value.setView([store.lat, store.lng], 15, { animate: true });
});

watch(selectedStoreId, () => {
  loadStoreProducts();
});

watch(storeCurrency, () => {
  loadStoreProducts();
});

watch(storeBrand, () => {
  loadStoreProducts();
});

watch(storeCategory, () => {
  loadStoreProducts();
});

watch(
  () => userSession.isAuthenticated.value,
  (isAuth) => {
    if (!isAuth) {
      router.replace({ name: 'user-login' });
    }
  }
);

watch(productQuery, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    searchCatalog();
  }, 300);
});

watch(selectedProductId, () => {
  loadSkusByProduct();
});

watch(selectedSkuId, () => {
  loadPriceReports();
});

onMounted(() => {
  simulateUserLocation();
  initMap();
  searchStores();
});

onBeforeUnmount(() => {
  if (searchTimer) clearTimeout(searchTimer);
  if (mapInstance.value) {
    mapInstance.value.remove();
    mapInstance.value = null;
  }
});
</script>

<template>
  <div class="view home">
    <header class="hero">
      <div class="hero-text">
        <p class="eyebrow">Pricer Map</p>
        <h1>Explora precios en tu zona</h1>
        <p class="subtitle">
          Visualiza tiendas cercanas, busca productos y reporta precios.
        </p>
      </div>
      <div class="hero-card">
        <form class="form" @submit.prevent="searchStores">
          <label>
            Lat
            <input v-model.number="form.lat" type="number" step="0.0001" />
          </label>
          <label>
            Lng
            <input v-model.number="form.lng" type="number" step="0.0001" />
          </label>
          <label>
            Radio (km)
            <input v-model.number="form.radiusKm" type="number" min="0.2" step="0.1" />
          </label>
          <label>
            Resultados
            <input v-model.number="form.take" type="number" min="1" max="200" />
          </label>
          <button type="submit" :disabled="loading">
            {{ loading ? 'Buscando...' : 'Buscar en el mapa' }}
          </button>
        </form>
        <p v-if="error" class="error">{{ error }}</p>
      </div>
      <div class="hero-card auth-card">
        <h3>Sesion activa</h3>
        <p class="subtitle">Token del usuario actual.</p>
        <AuthTokenCard :model-value="userSession.token.value" @clear="logoutUser" />
      </div>
    </header>

    <section class="map-shell">
      <div class="map-panel">
        <div ref="mapRef" class="map-canvas" />
        <div v-if="priceReports.length" class="price-legend">
          <div class="legend-row">
            <span class="legend-dot low" />
            Precio bajo
          </div>
          <div class="legend-row">
            <span class="legend-dot high" />
            Precio alto
          </div>
        </div>
      </div>

      <aside class="map-sidebar">
        <h2>Tiendas encontradas</h2>
        <p v-if="!stores.length && !loading" class="empty">
          Ejecuta una busqueda para ver resultados.
        </p>
        <ul class="store-list">
          <li
            v-for="store in stores"
            :key="store.storeId"
            :class="{ active: store.storeId === selectedStoreId }"
            @click="selectStore(store.storeId)"
          >
            <div>
              <strong>{{ store.name }}</strong>
              <span v-if="store.chainName">- {{ store.chainName }}</span>
            </div>
            <div class="meta">
              <span>{{ store.city || 'Sin ciudad' }}</span>
              <span class="mono">{{ store.distanceMeters.toFixed(0) }} m</span>
            </div>
            <div v-if="priceMap.get(store.storeId)" class="price-chip">
              {{ priceMap.get(store.storeId).currency }} {{ priceMap.get(store.storeId).price }}
            </div>
          </li>
        </ul>
      </aside>
    </section>

    <section class="store-products-main">
      <div class="store-products-header">
        <div>
          <h2>Productos en tienda</h2>
          <p class="subtitle">
            {{ selectedStore ? `Inventario de ${selectedStore.name}` : 'Selecciona una tienda para ver sus productos.' }}
          </p>
        </div>
        <div class="store-sort">
          <label>
            Ordenar
            <select v-model="storeSort">
              <option value="recent">Mas reciente</option>
              <option value="priceAsc">Precio menor</option>
              <option value="priceDesc">Precio mayor</option>
              <option value="name">Nombre</option>
            </select>
          </label>
          <label>
            Moneda
            <select v-model="storeCurrency">
              <option value="">Todas</option>
              <option value="ARS">ARS</option>
              <option value="USD">USD</option>
            </select>
          </label>
          <label>
            Marca
            <input v-model="storeBrand" placeholder="Marca" />
          </label>
          <label>
            Categoria
            <input v-model="storeCategory" placeholder="Categoria" />
          </label>
          <button type="button" class="ghost refresh" @click="loadStoreProducts" :disabled="!selectedStoreId">
            Actualizar
          </button>
        </div>
      </div>

      <div v-if="!selectedStoreId" class="store-products-empty">
        <p class="hint">Selecciona una tienda para ver sus productos.</p>
      </div>
      <p v-else-if="storeProductsLoading" class="hint">Cargando productos...</p>
      <p v-else-if="storeProductsError" class="error">{{ storeProductsError }}</p>
      <p v-else-if="!storeProducts.length" class="hint">Sin precios cargados.</p>

      <ul v-else class="product-grid">
        <li v-for="item in pagedStoreProducts" :key="item.skuId">
          <div class="product-row">
            <img v-if="item.imageUrl" :src="item.imageUrl" alt="product" />
            <div>
              <strong>{{ item.skuDisplayName }}</strong>
              <span class="meta">{{ item.productName }} - {{ item.brand || 'Marca' }}</span>
            </div>
          </div>
          <span class="price-chip">
            {{ item.currency }} {{ item.price }}
          </span>
        </li>
      </ul>

      <div v-if="storeProducts.length" class="pager">
        <button type="button" :disabled="storePage === 1" @click="storePage -= 1">Anterior</button>
        <span>Pagina {{ storePage }}</span>
        <button
          type="button"
          :disabled="storePage * storePageSize >= sortedStoreProducts.length"
          @click="storePage += 1"
        >
          Siguiente
        </button>
      </div>
    </section>

    <section class="product-shell">
      <div class="product-card">
        <h2>Buscar producto</h2>
        <input v-model="productQuery" placeholder="Buscar por nombre o marca" />
        <p v-if="catalogLoading" class="hint">Buscando productos...</p>
        <p v-if="catalogError" class="error">{{ catalogError }}</p>
        <ul class="product-list">
          <li
            v-for="product in productResults"
            :key="product.productId"
            :class="{ active: product.productId === selectedProductId }"
            @click="selectedProductId = product.productId"
          >
            <div>
              <strong>{{ product.name }}</strong>
              <span class="meta">{{ product.brand || 'Marca' }} - {{ product.category || 'Categoria' }}</span>
            </div>
          </li>
        </ul>
      </div>

      <div class="product-card">
        <h2>Seleccionar SKU</h2>
        <p class="subtitle">Elige la variante del producto.</p>
        <ul class="product-list">
          <li
            v-for="sku in skusByProduct"
            :key="sku.skuId"
            :class="{ active: sku.skuId === selectedSkuId }"
            @click="selectedSkuId = sku.skuId"
          >
            <div>
              <strong>{{ sku.displayName }}</strong>
              <span class="meta">
                {{ sku.productName }} - {{ sku.brand || 'Marca' }} - {{ sku.sizeValue || '' }} {{ sku.sizeUnit || '' }}
              </span>
            </div>
          </li>
        </ul>
      </div>

      <div class="product-card">
        <h2>Registrar precio</h2>
        <p class="subtitle">
          Selecciona un comercio del mapa y un producto.
        </p>
        <div class="selection">
          <div>
            <p class="label">Comercio</p>
            <p class="value">{{ selectedStore?.name || 'Sin seleccionar' }}</p>
          </div>
          <div>
            <p class="label">Producto</p>
            <p class="value">{{ selectedSku?.displayName || 'Sin seleccionar' }}</p>
          </div>
          <div>
            <p class="label">Precio actual</p>
            <p class="value">
              {{ selectedStoreProduct ? `${selectedStoreProduct.currency} ${selectedStoreProduct.price}` : 'Sin precio' }}
            </p>
          </div>
        </div>
        <label>
          Precio
          <input v-model.number="priceInput" type="number" step="0.01" />
        </label>
        <button type="button" :disabled="submitLoading" @click="submitPrice">
          {{ submitLoading ? 'Guardando...' : 'Guardar precio' }}
        </button>
        <button type="button" class="ghost" @click="openHistory">Ver historial</button>
        <p v-if="submitError" class="error">{{ submitError }}</p>
        <p v-if="submitSuccess" class="success">{{ submitSuccess }}</p>
      </div>
    </section>

    <div v-if="historyModalOpen" class="modal-backdrop" @click.self="historyModalOpen = false">
      <div class="modal-card">
        <div class="modal-header">
          <h3>Historial de precios</h3>
          <button type="button" class="ghost" @click="historyModalOpen = false">Cerrar</button>
        </div>
        <p v-if="historyLoading" class="hint">Cargando historial...</p>
        <p v-else-if="historyError" class="error">{{ historyError }}</p>
        <ul v-else class="history-list">
          <li v-for="item in historyData" :key="item.reportedAt">
            <span>{{ new Date(item.reportedAt).toLocaleString() }}</span>
            <strong>{{ item.currency }} {{ item.price }}</strong>
          </li>
        </ul>
      </div>
    </div>

    <section v-if="selectedStore" class="detail-card">
      <div>
        <h3>{{ selectedStore.name }}</h3>
        <p class="subtitle">
          {{ selectedStore.address || 'Direccion sin registrar' }} - {{ selectedStore.city || 'Ciudad' }}
        </p>
      </div>
      <div class="detail-meta">
        <div>
          <p class="label">Distancia</p>
          <p class="value">{{ selectedStore.distanceMeters.toFixed(0) }} m</p>
        </div>
        <div>
          <p class="label">Coordenadas</p>
          <p class="value mono">{{ selectedStore.lat.toFixed(4) }}, {{ selectedStore.lng.toFixed(4) }}</p>
        </div>
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

.home {
  --surface: #ffffff;
  --surface-alt: #fffaf5;
  --accent: #0f766e;
  --accent-strong: #0b5f58;
  --muted: #6b7280;
  --stroke: rgba(15, 118, 110, 0.15);
  --shadow-soft: 0 18px 40px rgba(17, 24, 39, 0.1);
  --shadow-strong: 0 28px 70px rgba(15, 23, 42, 0.16);
  font-family: 'Plus Jakarta Sans', 'Segoe UI', sans-serif;
  display: grid;
  gap: 2.8rem;
  max-width: 1400px;
  margin: 0 auto;
  padding: clamp(1.6rem, 2.5vw, 3rem) clamp(1.4rem, 4vw, 3rem) 4.5rem;
  position: relative;
}

.home::before {
  content: '';
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.85) 0%, rgba(255, 255, 255, 0.55) 45%, rgba(255, 255, 255, 0.9) 100%);
  pointer-events: none;
  z-index: 0;
}

.home > * {
  position: relative;
  z-index: 1;
}

.hero {
  display: grid;
  gap: 2.4rem;
  grid-template-columns: minmax(280px, 1.2fr) minmax(280px, 0.9fr) minmax(280px, 0.9fr);
  align-items: stretch;
}

.hero-text {
  max-width: 560px;
  padding-right: 1.2rem;
}

.eyebrow {
  font-size: 0.8rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--muted);
}

.hero-text h1,
.hero h2,
.hero h3,
.product-card h2,
.map-sidebar h2,
.detail-card h3 {
  font-family: 'Fraunces', 'Times New Roman', serif;
}

.hero-text h1 {
  font-size: clamp(2.6rem, 4vw, 3.8rem);
  margin: 0.6rem 0 0.5rem;
  line-height: 1.05;
  color: #101828;
}

.subtitle {
  color: var(--muted);
}

.hero-card {
  background: var(--surface);
  border-radius: 26px;
  padding: 1.6rem;
  box-shadow: var(--shadow-soft);
  border: 1px solid rgba(15, 118, 110, 0.08);
  animation: float-in 0.7s ease both;
  height: 100%;
  display: grid;
  align-content: start;
}

.auth-card h3 {
  margin: 0 0 0.5rem;
}

.form {
  display: grid;
  gap: 0.85rem;
}

label {
  display: grid;
  gap: 0.35rem;
  font-weight: 600;
  color: #111827;
}

input {
  padding: 0.65rem 0.8rem;
  border-radius: 12px;
  border: 1px solid var(--stroke);
  background: #ffffff;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
  font-family: inherit;
}

input:focus {
  outline: none;
  border-color: rgba(15, 118, 110, 0.5);
  box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.15);
}

button {
  padding: 0.75rem 1rem;
  border: none;
  border-radius: 14px;
  background: linear-gradient(135deg, var(--accent), var(--accent-strong));
  color: white;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  box-shadow: 0 12px 24px rgba(15, 118, 110, 0.2);
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  box-shadow: none;
}

button:not(:disabled):hover {
  transform: translateY(-1px);
}

.ghost {
  background: transparent;
  color: var(--accent);
  border: 1px solid rgba(15, 118, 110, 0.5);
  padding: 0.6rem 0.9rem;
  border-radius: 999px;
  font-weight: 600;
  box-shadow: none;
}

.map-shell {
  display: grid;
  grid-template-columns: minmax(0, 2.2fr) minmax(320px, 1fr);
  gap: 2.2rem;
  align-items: start;
}

.map-panel {
  position: relative;
  border-radius: 30px;
  min-height: clamp(360px, 58vh, 640px);
  overflow: hidden;
  box-shadow: var(--shadow-strong);
  border: 1px solid rgba(15, 118, 110, 0.08);
}

.map-canvas {
  width: 100%;
  height: 100%;
  min-height: clamp(360px, 58vh, 640px);
}

.price-legend {
  position: absolute;
  right: 1rem;
  bottom: 1rem;
  background: rgba(255, 255, 255, 0.9);
  padding: 0.6rem 0.8rem;
  border-radius: 12px;
  display: grid;
  gap: 0.4rem;
  font-size: 0.85rem;
}

.legend-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.legend-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
}

.legend-dot.low {
  background: hsl(120, 65%, 45%);
}

.legend-dot.high {
  background: hsl(0, 65%, 45%);
}

.map-sidebar {
  background: var(--surface-alt);
  border-radius: 30px;
  padding: 1.8rem 1.7rem;
  box-shadow: var(--shadow-strong);
  border: 1px solid rgba(248, 226, 205, 0.7);
  display: grid;
  gap: 1.2rem;
}

.store-sort label {
  display: grid;
  gap: 0.35rem;
  font-weight: 600;
}

.store-sort select {
  padding: 0.5rem 0.6rem;
  border-radius: 12px;
  border: 1px solid var(--stroke);
  background: #fff;
  font-family: inherit;
}

.store-sort input {
  padding: 0.5rem 0.6rem;
  border-radius: 12px;
  border: 1px solid var(--stroke);
}

.product-list.small li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.6rem;
}

.product-row {
  display: grid;
  grid-template-columns: 42px 1fr;
  gap: 0.6rem;
  align-items: center;
}

.product-row img {
  width: 42px;
  height: 42px;
  border-radius: 10px;
  object-fit: cover;
  border: 1px solid rgba(15, 118, 110, 0.1);
}

.store-products-main {
  background: var(--surface);
  border-radius: 30px;
  padding: 1.8rem;
  box-shadow: var(--shadow-soft);
  border: 1px solid rgba(15, 118, 110, 0.08);
  display: grid;
  gap: 1.5rem;
}

.store-products-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.store-products-header .store-sort {
  display: grid;
  grid-template-columns: repeat(4, minmax(160px, 1fr));
  gap: 0.8rem;
}

.store-products-header .refresh {
  align-self: end;
  justify-self: start;
  white-space: nowrap;
}

.store-products-empty {
  padding: 1rem 0;
}

.product-grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.product-grid li {
  padding: 1rem;
  border-radius: 18px;
  background: #f6fbfa;
  border: 1px solid rgba(15, 118, 110, 0.1);
  display: grid;
  gap: 0.8rem;
}

.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.6rem;
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.35);
  display: grid;
  place-items: center;
  z-index: 20;
}

.modal-card {
  width: min(520px, 92vw);
  background: #ffffff;
  border-radius: 26px;
  padding: 1.5rem;
  box-shadow: 0 24px 60px rgba(15, 23, 42, 0.25);
  display: grid;
  gap: 1rem;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.history-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.6rem;
}

.history-list li {
  display: flex;
  justify-content: space-between;
  padding: 0.6rem 0.8rem;
  border-radius: 12px;
  background: #f4f8f7;
}

:global(.leaflet-tooltip.price-tooltip) {
  background: #ffffff;
  border: 1px solid rgba(15, 118, 110, 0.2);
  color: #111827;
  border-radius: 12px;
  padding: 0.4rem 0.5rem;
  box-shadow: 0 10px 20px rgba(17, 24, 39, 0.12);
}

:global(.leaflet-tooltip.price-tooltip .tooltip) {
  display: grid;
  grid-template-columns: 36px 1fr;
  gap: 0.5rem;
  align-items: center;
}

:global(.leaflet-tooltip.price-tooltip img) {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  object-fit: cover;
}

.store-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.9rem;
}

.store-list li {
  padding: 1rem 1.1rem;
  border-radius: 20px;
  background: #eef8f6;
  cursor: pointer;
  display: grid;
  gap: 0.4rem;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  border: 1px solid transparent;
}

.store-list li.active {
  background: #e3f4f1;
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(15, 118, 110, 0.12);
  border-color: rgba(15, 118, 110, 0.25);
}

.price-chip {
  align-self: start;
  justify-self: start;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  background: #0f766e;
  color: white;
  font-size: 0.8rem;
  font-weight: 600;
}

.meta {
  display: flex;
  justify-content: space-between;
  color: var(--muted);
  font-size: 0.85rem;
}

.product-shell {
  display: grid;
  gap: 2rem;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
}

.product-card {
  background: var(--surface);
  border-radius: 26px;
  padding: 1.6rem;
  box-shadow: var(--shadow-soft);
  display: grid;
  gap: 1rem;
  border: 1px solid rgba(15, 118, 110, 0.08);
}

.product-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.6rem;
  max-height: 240px;
  overflow: auto;
}

.product-list li {
  padding: 0.7rem;
  border-radius: 14px;
  background: #f4f8f7;
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.product-list li.active {
  background: #e6f4f1;
  box-shadow: 0 10px 20px rgba(15, 118, 110, 0.12);
}

.selection {
  display: grid;
  gap: 0.6rem;
}

.detail-card {
  background: var(--surface);
  border-radius: 26px;
  padding: 1.5rem;
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1.5rem;
  box-shadow: var(--shadow-soft);
  border: 1px solid rgba(15, 118, 110, 0.08);
}

.detail-meta {
  display: flex;
  gap: 2rem;
  flex-wrap: wrap;
}

.label {
  text-transform: uppercase;
  font-size: 0.7rem;
  letter-spacing: 0.12em;
  color: var(--muted);
}

.value {
  font-size: 1.05rem;
  font-weight: 600;
}

.empty {
  color: var(--muted);
}

.error {
  color: var(--warn);
  margin-top: 0.8rem;
}

.success {
  color: #065f46;
}

.hint {
  color: var(--muted);
  font-size: 0.85rem;
}

@media (min-width: 1200px) {
  .hero {
    grid-template-columns: minmax(320px, 1.2fr) minmax(300px, 0.9fr) minmax(300px, 0.9fr);
  }

  .product-shell {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 1024px) {
  .home {
    max-width: 1100px;
  }

  .map-shell {
    grid-template-columns: minmax(0, 1.5fr) minmax(280px, 1fr);
    gap: 1.6rem;
  }

  .hero {
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  }

  .store-products-header .store-sort {
    grid-template-columns: repeat(2, minmax(160px, 1fr));
  }
}

@media (max-width: 900px) {
  .map-shell {
    grid-template-columns: 1fr;
  }

  .map-panel,
  .map-canvas {
    min-height: 380px;
  }

  .map-sidebar {
    order: 2;
  }

  .map-panel {
    order: 1;
  }

  .detail-card {
    flex-direction: column;
  }

  .store-products-main {
    padding: 1.4rem;
  }
}

@media (max-width: 768px) {
  .home {
    padding-bottom: 3rem;
  }

  .hero {
    gap: 1.5rem;
  }

  .hero-text h1 {
    font-size: clamp(2rem, 8vw, 3rem);
  }

  .hero-card {
    padding: 1.3rem;
  }

  .map-sidebar {
    padding: 1.4rem;
  }

  .store-list li {
    padding: 0.9rem;
  }

  .product-list {
    max-height: 200px;
  }

  .store-products-header .store-sort {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .hero {
    grid-template-columns: 1fr;
  }

  .map-panel,
  .map-canvas {
    min-height: 320px;
  }

  .price-legend {
    right: 0.6rem;
    bottom: 0.6rem;
    font-size: 0.75rem;
  }

  .product-card {
    padding: 1.2rem;
  }

  .product-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 480px) {
  .home {
    padding: 1rem 0.9rem 2.8rem;
  }

  .map-panel,
  .map-canvas {
    min-height: 280px;
  }

  .meta {
    flex-direction: column;
    gap: 0.2rem;
  }

  .price-chip {
    font-size: 0.75rem;
  }

  .detail-card {
    padding: 1.2rem;
  }
}
</style>
