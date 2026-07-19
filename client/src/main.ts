import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './style.css'
import App from './App.vue'
import { router } from './router'
import { clerkPlugin } from '@clerk/vue'

const app = createApp(App).use(createPinia()).use(router)

// Auth is enabled only when a Clerk publishable key is provided. Without it the
// app runs open (dev convenience) — set VITE_CLERK_PUBLISHABLE_KEY to turn it on.
const clerkKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY as string | undefined
if (clerkKey) {
  app.use(clerkPlugin, { publishableKey: clerkKey })
} else {
  // eslint-disable-next-line no-console
  console.warn(
    'VITE_CLERK_PUBLISHABLE_KEY is not set — authentication is disabled (dev mode). ' +
      'The API will also be unauthenticated and tenant-scoped data will be empty.',
  )
}

app.mount('#app')
