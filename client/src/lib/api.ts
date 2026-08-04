import axios from 'axios'
import { toast } from '@/lib/toast'

/**
 * Shared axios instance. In dev, requests go to `/api/*` and are proxied to the
 * .NET backend by Vite (see vite.config.ts). In production (e.g. Vercel) there
 * is no proxy, so `VITE_API_URL` must point at the deployed API's origin
 * (e.g. https://talentmachine-api.onrender.com) — requests then go to
 * `${VITE_API_URL}/api/*`. Falls back to the relative `/api` dev behavior when
 * unset.
 *
 * When Clerk is configured, a request interceptor attaches the current session
 * token as a Bearer credential so the API can authenticate the caller and scope
 * data to their tenant. On a 401 we bounce to Clerk's sign-in.
 */
const apiOrigin = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ?? ''

export const api = axios.create({
  baseURL: `${apiOrigin}/api`,
  headers: { 'Content-Type': 'application/json' },
})

// Clerk loads itself onto window.Clerk once the plugin initializes.
function clerk(): { session?: { getToken: () => Promise<string | null> }; redirectToSignIn?: () => void } | undefined {
  return (window as unknown as { Clerk?: never }).Clerk
}

api.interceptors.request.use(async (config) => {
  try {
    const c = clerk()
    if (c?.session) {
      const token = await c.session.getToken()
      if (token) config.headers.Authorization = `Bearer ${token}`
    }
  } catch {
    // No session / Clerk not ready — send the request unauthenticated.
  }
  // Which company (tenant) to act as, when the user belongs to several. The server
  // validates it against the caller's memberships, so a stale value is harmless.
  const activeCompanyId = localStorage.getItem('activeCompanyId')
  if (activeCompanyId) config.headers['X-Tenant-Id'] = activeCompanyId
  return config
})

api.interceptors.response.use(
  (res) => res,
  (error) => {
    const status = error?.response?.status
    if (status === 401) {
      clerk()?.redirectToSignIn?.()
      return Promise.reject(error)
    }
    // Surface failed *writes* so a silently-dropped save is visible. Reads that
    // fail keep the existing "graceful empty state" behavior (no error spam).
    const method = (error?.config?.method ?? 'get').toLowerCase()
    if (method !== 'get') {
      toast.error('Couldn’t save your changes. Please check your connection and try again.')
    }
    return Promise.reject(error)
  },
)
