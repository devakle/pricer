const DEFAULT_API_BASE_URL = 'http://localhost:5145';

export const config = Object.freeze({
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL,
});
