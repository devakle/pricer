<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import L from 'leaflet';
import { createPricerApi } from '../api/pricerApi.js';
import { config } from '../config.js';
import { useMerchantSession } from '../state/useMerchantSession.js';
import AuthTokenCard from '../components/AuthTokenCard.vue';

const router = useRouter();
const merchantSession = useMerchantSession();
const api = createPricerApi({
  baseUrl: config.apiBaseUrl,
  getToken: () => merchantSession.token.value?.trim() || null,
});

const currentLocation = ref({ lat: -31.4201, lng: -64.1888 });
const mapRef = ref(null);
const mapInstance = ref(null);
const markersLayer = ref(null);

const storeForm = ref({
  name: '',
  chainName: '',
  address: '',
  city: '',
  lat: -31.4201,
  lng: -64.1888,
});

const stores = ref([]);
const storeLoading = ref(false);
const storeError = ref('');
const storeSuccess = ref('');
const selectedStoreId = ref('');

const productForm = ref({
  name: '',
  brand: '',
  category: '',
  skuDisplayName: '',
  sizeValue: '',
  sizeUnit: '',
  price: '',
  currency: 'ARS',
  photo: null,
});

const productLoading = ref(false);
const productError = ref('');
const productSuccess = ref('');
const previewUrl = ref('');

const mapCenter = computed(() => [Number(storeForm.value.lat), Number(storeForm.value.lng)]);

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
  });

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap',
  }).addTo(mapInstance.value);

  markersLayer.value = L.layerGroup().addTo(mapInstance.value);

  mapInstance.value.on('click', (event) => {
    storeForm.value.lat = event.latlng.lat;
    storeForm.value.lng = event.latlng.lng;
    renderMarkers();
  });
}

function renderMarkers() {
  if (!markersLayer.value) return;
  markersLayer.value.clearLayers();

  stores.value.forEach((store) => {
    const marker = L.circleMarker([store.lat, store.lng], {
      radius: store.storeId === selectedStoreId.value ? 10 : 7,
      color: '#0f766e',
      fillColor: '#0f766e',
      fillOpacity: 0.85,
    });
    marker.on('click', () => {
      selectedStoreId.value = store.storeId;
    });
    marker.bindTooltip(store.name, { direction: 'top' });
    marker.addTo(markersLayer.value);
  });

  L.circleMarker([storeForm.value.lat, storeForm.value.lng], {
    radius: 8,
    color: '#f97316',
    fillColor: '#f97316',
    fillOpacity: 0.9,
  }).addTo(markersLayer.value);
}

async function loadStores() {
  storeLoading.value = true;
  storeError.value = '';
  try {
    const response = await api.stores.getNear({
      lat: storeForm.value.lat,
      lng: storeForm.value.lng,
      radiusKm: 5,
      take: 100,
    });
    if (!response.ok) {
      storeError.value = response.error?.message || 'No se pudieron cargar comercios.';
      return;
    }
    stores.value = response.data || [];
    renderMarkers();
  } catch (err) {
    storeError.value = err?.message || 'Error inesperado.';
  } finally {
    storeLoading.value = false;
  }
}

async function createStore() {
  storeLoading.value = true;
  storeError.value = '';
  storeSuccess.value = '';
  try {
    const response = await api.stores.create({
      name: storeForm.value.name,
      chainName: storeForm.value.chainName || null,
      address: storeForm.value.address || null,
      city: storeForm.value.city || null,
      lat: Number(storeForm.value.lat),
      lng: Number(storeForm.value.lng),
    });
    if (!response.ok) {
      storeError.value = response.error?.message || 'No se pudo crear el comercio.';
      return;
    }
    storeSuccess.value = 'Comercio creado.';
    await loadStores();
  } catch (err) {
    storeError.value = err?.message || 'Error inesperado.';
  } finally {
    storeLoading.value = false;
  }
}

async function submitProduct() {
  productLoading.value = true;
  productError.value = '';
  productSuccess.value = '';

  if (!selectedStoreId.value) {
    productError.value = 'Selecciona un comercio.';
    productLoading.value = false;
    return;
  }

  if (!productForm.value.name || !productForm.value.skuDisplayName) {
    productError.value = 'Nombre y SKU requeridos.';
    productLoading.value = false;
    return;
  }

  const formData = new FormData();
  formData.append('name', productForm.value.name);
  formData.append('brand', productForm.value.brand || '');
  formData.append('category', productForm.value.category || '');
  formData.append('skuDisplayName', productForm.value.skuDisplayName);
  if (productForm.value.sizeValue) formData.append('sizeValue', productForm.value.sizeValue);
  if (productForm.value.sizeUnit) formData.append('sizeUnit', productForm.value.sizeUnit);
  formData.append('storeId', selectedStoreId.value);
  formData.append('price', productForm.value.price || '0');
  formData.append('currency', productForm.value.currency || 'ARS');
  if (productForm.value.photo) formData.append('photo', productForm.value.photo);

  try {
    const response = await api.catalog.createProduct(formData);
    if (!response.ok) {
      productError.value = response.error?.message || 'No se pudo crear el producto.';
      return;
    }
    productSuccess.value = 'Producto y precio creados.';
    productForm.value = {
      name: '',
      brand: '',
      category: '',
      skuDisplayName: '',
      sizeValue: '',
      sizeUnit: '',
      price: '',
      currency: 'ARS',
      photo: null,
    };
    previewUrl.value = '';
  } catch (err) {
    productError.value = err?.message || 'Error inesperado.';
  } finally {
    productLoading.value = false;
  }
}

function onPhotoChange(event) {
  const file = event.target.files?.[0];
  productForm.value.photo = file || null;
  previewUrl.value = file ? URL.createObjectURL(file) : '';
}

function logoutMerchant() {
  merchantSession.logout();
  router.replace({ name: 'merchant-login' });
}

watch(mapCenter, () => {
  if (!mapInstance.value) return;
  mapInstance.value.setView(mapCenter.value, 13);
});

watch(selectedStoreId, () => {
  renderMarkers();
});

onMounted(() => {
  const jitterLat = (Math.random() - 0.5) * 0.01;
  const jitterLng = (Math.random() - 0.5) * 0.01;
  currentLocation.value = {
    lat: Number((currentLocation.value.lat + jitterLat).toFixed(6)),
    lng: Number((currentLocation.value.lng + jitterLng).toFixed(6)),
  };
  storeForm.value.lat = currentLocation.value.lat;
  storeForm.value.lng = currentLocation.value.lng;
  initMap();
  loadStores();
});

onBeforeUnmount(() => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value);
  if (mapInstance.value) {
    mapInstance.value.remove();
    mapInstance.value = null;
  }
});
</script>

<template>
  <div class="view merchant">
    <header class="hero">
      <div>
        <p class="eyebrow">Comerciante</p>
        <h1>Panel de comercios</h1>
        <p class="subtitle">Crea comercios y agrega productos con precios.</p>
      </div>
      <div class="hero-card auth-card">
        <h3>Token activo</h3>
        <AuthTokenCard :model-value="merchantSession.token.value" @clear="logoutMerchant" />
      </div>
    </header>

    <section class="map-shell">
      <div class="map-panel">
        <div ref="mapRef" class="map-canvas" />
        <p class="hint">Haz click en el mapa para ubicar tu comercio.</p>
      </div>
      <div class="map-sidebar">
        <h2>Crear comercio</h2>
        <form class="form" @submit.prevent="createStore">
          <label>
            Nombre
            <input v-model="storeForm.name" />
          </label>
          <label>
            Cadena
            <input v-model="storeForm.chainName" />
          </label>
          <label>
            Direccion
            <input v-model="storeForm.address" />
          </label>
          <label>
            Ciudad
            <input v-model="storeForm.city" />
          </label>
          <label>
            Lat
            <input v-model.number="storeForm.lat" type="number" step="0.0001" />
          </label>
          <label>
            Lng
            <input v-model.number="storeForm.lng" type="number" step="0.0001" />
          </label>
          <button type="submit" :disabled="storeLoading">
            {{ storeLoading ? 'Guardando...' : 'Crear comercio' }}
          </button>
        </form>
        <p v-if="storeError" class="error">{{ storeError }}</p>
        <p v-if="storeSuccess" class="success">{{ storeSuccess }}</p>
        <div class="store-list">
          <div
            v-for="store in stores"
            :key="store.storeId"
            class="store-item"
            :class="{ active: store.storeId === selectedStoreId }"
            @click="selectedStoreId = store.storeId"
          >
            <strong>{{ store.name }}</strong>
            <span class="meta">{{ store.city || 'Ciudad' }}</span>
          </div>
        </div>
      </div>
    </section>

    <section class="product-shell">
      <div class="product-card">
        <h2>Agregar producto</h2>
        <form class="form" @submit.prevent="submitProduct">
          <label>
            Nombre
            <input v-model="productForm.name" />
          </label>
          <label>
            Marca
            <input v-model="productForm.brand" />
          </label>
          <label>
            Categoria
            <input v-model="productForm.category" />
          </label>
          <label>
            SKU display
            <input v-model="productForm.skuDisplayName" />
          </label>
          <div class="grid">
            <label>
              Tamaño
              <input v-model="productForm.sizeValue" />
            </label>
            <label>
              Unidad
              <input v-model="productForm.sizeUnit" />
            </label>
          </div>
          <label>
            Precio
            <input v-model="productForm.price" type="number" step="0.01" />
          </label>
          <label>
            Moneda
            <input v-model="productForm.currency" />
          </label>
          <label>
            Foto
            <input type="file" accept="image/*" @change="onPhotoChange" />
          </label>
          <img v-if="previewUrl" :src="previewUrl" class="preview" alt="preview" />
          <button type="submit" :disabled="productLoading">
            {{ productLoading ? 'Guardando...' : 'Crear producto' }}
          </button>
        </form>
        <p v-if="productError" class="error">{{ productError }}</p>
        <p v-if="productSuccess" class="success">{{ productSuccess }}</p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.merchant {
  display: grid;
  gap: 2.5rem;
}

.hero {
  display: grid;
  gap: 2rem;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  align-items: center;
}

.hero-card {
  background: var(--surface);
  border-radius: 22px;
  padding: 1.5rem;
  box-shadow: 0 22px 45px rgba(17, 24, 39, 0.08);
}

.map-shell {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);
  gap: 2rem;
}

.map-panel {
  position: relative;
  border-radius: 24px;
  min-height: 420px;
  overflow: hidden;
  box-shadow: 0 24px 50px rgba(17, 24, 39, 0.08);
}

.map-canvas {
  width: 100%;
  height: 100%;
  min-height: 420px;
}

.map-sidebar {
  background: var(--surface);
  border-radius: 22px;
  padding: 1.5rem;
  box-shadow: 0 22px 45px rgba(17, 24, 39, 0.08);
  display: grid;
  gap: 1rem;
}

.store-list {
  display: grid;
  gap: 0.6rem;
}

.store-item {
  padding: 0.7rem;
  border-radius: 12px;
  background: #f4f8f7;
  cursor: pointer;
  display: grid;
  gap: 0.2rem;
}

.store-item.active {
  background: #e6f4f1;
}

.product-shell {
  display: grid;
  gap: 2rem;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}

.product-card {
  background: var(--surface);
  border-radius: 22px;
  padding: 1.5rem;
  box-shadow: 0 20px 40px rgba(17, 24, 39, 0.08);
}

.form {
  display: grid;
  gap: 0.8rem;
}

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 0.8rem;
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

.preview {
  width: 100%;
  max-height: 200px;
  object-fit: cover;
  border-radius: 12px;
}

.error {
  color: var(--warn);
}

.success {
  color: #065f46;
}

.hint {
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: var(--muted);
}
</style>
