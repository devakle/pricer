import { computed, ref, watch } from 'vue';

const STORAGE_KEY = 'pricer.user.session';

export function getStoredUserSession() {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return { token: '', expiresAt: '', isAuthenticated: false };
  try {
    const parsed = JSON.parse(raw);
    const token = parsed.token || '';
    const expiresAt = parsed.expiresAt || '';
    const exp = Date.parse(expiresAt);
    const isAuthenticated = Boolean(token && expiresAt && !Number.isNaN(exp) && Date.now() < exp);
    return { token, expiresAt, isAuthenticated };
  } catch {
    return { token: '', expiresAt: '', isAuthenticated: false };
  }
}

export function useUserSession() {
  const token = ref('');
  const expiresAt = ref('');
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved) {
    try {
      const parsed = JSON.parse(saved);
      token.value = parsed.token || '';
      expiresAt.value = parsed.expiresAt || '';
      if (token.value && expiresAt.value) {
        const exp = Date.parse(expiresAt.value);
        if (Number.isNaN(exp) || Date.now() >= exp) {
          token.value = '';
          expiresAt.value = '';
          localStorage.removeItem(STORAGE_KEY);
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  const isExpired = () => {
    if (!expiresAt.value) return true;
    const exp = Date.parse(expiresAt.value);
    return Number.isNaN(exp) || Date.now() >= exp;
  };

  watch([token, expiresAt], () => {
    if (token.value && token.value.trim() && expiresAt.value && !isExpired()) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ token: token.value.trim(), expiresAt: expiresAt.value })
      );
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  });

  const isAuthenticated = computed(() => Boolean(token.value && token.value.trim()) && !isExpired());

  const login = (sessionToken, sessionExpiresAt) => {
    token.value = sessionToken;
    expiresAt.value = sessionExpiresAt;
  };

  const logout = () => {
    token.value = '';
    expiresAt.value = '';
    localStorage.removeItem(STORAGE_KEY);
  };

  return {
    token,
    expiresAt,
    isAuthenticated,
    login,
    logout,
  };
}
