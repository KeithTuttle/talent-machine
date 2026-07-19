import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'planner',
    component: () => import('@/views/PlannerView.vue'),
    meta: { title: 'Planner' },
  },
  {
    path: '/people',
    name: 'people',
    component: () => import('@/views/PeopleView.vue'),
    meta: { title: 'People' },
  },
  {
    path: '/seasons',
    name: 'seasons',
    component: () => import('@/views/SeasonsView.vue'),
    meta: { title: 'Seasons & Productions' },
  },
  {
    path: '/team',
    name: 'team',
    component: () => import('@/views/TeamView.vue'),
    meta: { title: 'Team' },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { title: 'Not Found' },
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Keep the browser tab title in sync with the active route.
const BASE_TITLE = 'The Talent Machine Company'
router.afterEach((to) => {
  const pageTitle = to.meta.title as string | undefined
  document.title = pageTitle ? `${pageTitle} · ${BASE_TITLE}` : BASE_TITLE
})
