import { HttpClient } from './httpClient.js';

export class StoresService {
  /** @param {HttpClient} http */
  constructor(http) {
    this.http = http;
  }

  /** @param {import('./types.js').GetStoresNearParams} params */
  async getNear(params) {
    return this.http.get('/api/stores/near', { query: params });
  }

  /** @param {{ name: string, chainName?: string | null, address?: string | null, city?: string | null, lat: number, lng: number }} payload */
  async create(payload) {
    return this.http.post('/api/stores', payload);
  }

  /** @param {string} storeId @param {{ take?: number, skip?: number, currency?: string, brand?: string, category?: string }} [params] */
  async getProducts(storeId, params) {
    return this.http.get(`/api/stores/${storeId}/products`, { query: params });
  }
}

export class PriceReportsService {
  /** @param {HttpClient} http */
  constructor(http) {
    this.http = http;
  }

  /** @param {import('./types.js').CreatePriceReportRequest} payload */
  async create(payload) {
    return this.http.post('/api/price-reports', payload);
  }

  /** @param {{ skuId: string, lat: number, lng: number, radiusKm: number, take: number }} params */
  async getNear(params) {
    return this.http.get('/api/price-reports/near', { query: params });
  }

  /** @param {{ storeId: string, skuId: string, take?: number }} params */
  async getHistory(params) {
    return this.http.get('/api/price-reports/history', { query: params });
  }
}

export class AuthService {
  /** @param {HttpClient} http */
  constructor(http) {
    this.http = http;
  }

  /** @param {import('./types.js').AdminLoginRequest} payload */
  async login(payload) {
    return this.http.post('/api/auth/login', payload);
  }

  /** @param {import('./types.js').AdminLoginRequest} payload */
  async userLogin(payload) {
    return this.http.post('/api/auth/user-login', payload);
  }

  /** @param {import('./types.js').AdminLoginRequest} payload */
  async merchantLogin(payload) {
    return this.http.post('/api/auth/merchant-login', payload);
  }
}

export class CatalogService {
  /** @param {HttpClient} http */
  constructor(http) {
    this.http = http;
  }

  /** @param {{ query?: string, take?: number }} params */
  async searchSkus(params) {
    return this.http.get('/api/catalog/skus', { query: params });
  }

  /** @param {{ query?: string, take?: number }} params */
  async searchProducts(params) {
    return this.http.get('/api/catalog/products', { query: params });
  }

  /** @param {string} productId */
  async getSkusByProduct(productId) {
    return this.http.get(`/api/catalog/products/${productId}/skus`);
  }

  /** @param {FormData} formData */
  async createProduct(formData) {
    return this.http.postForm('/api/catalog/products', formData);
  }
}

/**
 * @param {{ baseUrl: string, getToken?: (() => string | null) }} options
 */
export function createPricerApi(options) {
  const http = new HttpClient(options);
  return Object.freeze({
    http,
    stores: new StoresService(http),
    priceReports: new PriceReportsService(http),
    auth: new AuthService(http),
    catalog: new CatalogService(http),
  });
}
