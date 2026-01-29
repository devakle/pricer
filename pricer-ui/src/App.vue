<script setup>
import { useRoute } from 'vue-router';

const route = useRoute();
</script>

<template>
  <div class="page">
    <nav class="topbar">
      <div class="brand">
        <span class="dot" />
        Pricer
      </div>
      <div class="links">
        <router-link to="/" :class="{ active: route.name === 'home' }">Mapa</router-link>
        <router-link to="/products" :class="{ active: route.name === 'products' }">Productos</router-link>
      </div>
    </nav>
    <router-view />
  </div>
</template>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&display=swap');

.page {
  --ink: #12141c;
  --muted: #4b5565;
  --accent: #0f766e;
  --accent-soft: #d1fae5;
  --surface: rgba(255, 255, 255, 0.86);
  --stroke: #d6dde5;
  --warn: #b42318;

  min-height: 100vh;
  padding: 2.5rem 6vw 4rem;
  background: radial-gradient(circle at top left, #f2f5f8, #fef7ed 45%, #e8f5f2 100%);
  color: var(--ink);
  font-family: 'Space Grotesk', system-ui, sans-serif;
  animation: page-in 0.6s ease both;
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 2rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--accent);
  box-shadow: 0 0 0 6px rgba(15, 118, 110, 0.15);
}

.links {
  display: flex;
  gap: 1rem;
  background: rgba(255, 255, 255, 0.7);
  padding: 0.4rem;
  border-radius: 999px;
  border: 1px solid rgba(15, 118, 110, 0.12);
}

.links a {
  text-decoration: none;
  color: var(--muted);
  font-weight: 600;
  padding: 0.4rem 0.9rem;
  border-radius: 999px;
  transition: all 0.2s ease;
}

.links a.active,
.links a:hover {
  background: var(--accent);
  color: #fff;
}

@media (max-width: 700px) {
  .topbar {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
}

.view {
  width: 100%;
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

.mono {
  font-family: 'SFMono-Regular', ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

@keyframes page-in {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes float-in {
  from {
    opacity: 0;
    transform: translateY(14px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
