import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '../views/HomeView.vue';
import AdminLoginView from '../views/AdminLoginView.vue';
import AdminPanelView from '../views/AdminPanelView.vue';
import UserLoginView from '../views/UserLoginView.vue';
import MerchantLoginView from '../views/MerchantLoginView.vue';
import MerchantPanelView from '../views/MerchantPanelView.vue';
import { getStoredAdminSession } from '../state/useAdminSession.js';
import { getStoredUserSession } from '../state/useUserSession.js';
import { getStoredMerchantSession } from '../state/useMerchantSession.js';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView, meta: { requiresUser: true } },
    { path: '/login', name: 'user-login', component: UserLoginView },
    { path: '/admin/login', name: 'admin-login', component: AdminLoginView },
    { path: '/admin', name: 'admin-panel', component: AdminPanelView, meta: { requiresAdmin: true } },
    { path: '/merchant/login', name: 'merchant-login', component: MerchantLoginView },
    { path: '/merchant', name: 'merchant-panel', component: MerchantPanelView, meta: { requiresMerchant: true } },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
});

router.beforeEach((to) => {
  if (to.meta.requiresAdmin) {
    const session = getStoredAdminSession();
    if (!session.isAuthenticated) {
      return { name: 'admin-login' };
    }
  }

  if (to.meta.requiresUser) {
    const session = getStoredUserSession();
    if (!session.isAuthenticated) {
      return { name: 'user-login' };
    }
  }

  if (to.meta.requiresMerchant) {
    const session = getStoredMerchantSession();
    if (!session.isAuthenticated) {
      return { name: 'merchant-login' };
    }
  }

  return true;
});

export default router;
