<script setup>
</script>

<template>
  <div class="page">
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
