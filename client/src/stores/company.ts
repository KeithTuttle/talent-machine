import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '@/lib/api'
import { useScopeStore } from '@/stores/scope'
import type { Company } from '@/types'

/**
 * The companies (tenants) the signed-in user belongs to, plus which one is active.
 * The active id is mirrored to localStorage['activeCompanyId'] and sent as the
 * X-Tenant-Id header by the api client (see lib/api.ts). Only meaningful when Clerk
 * auth is on; in dev (auth off) there are no memberships and this stays empty.
 */
const authEnabled = !!import.meta.env.VITE_CLERK_PUBLISHABLE_KEY
const ACTIVE_KEY = 'activeCompanyId'

export const useCompanyStore = defineStore('company', () => {
  const companies = ref<Company[]>([])
  const activeCompanyId = ref<number | null>(Number(localStorage.getItem(ACTIVE_KEY)) || null)
  const loading = ref(false)

  const activeCompany = computed(
    () => companies.value.find((c) => c.tenantId === activeCompanyId.value) ?? null,
  )
  const hasMultiple = computed(() => companies.value.length > 1)

  function setActive(id: number | null) {
    activeCompanyId.value = id
    if (id == null) localStorage.removeItem(ACTIVE_KEY)
    else localStorage.setItem(ACTIVE_KEY, String(id))
  }

  async function fetchCompanies() {
    if (!authEnabled) return
    loading.value = true
    try {
      const { data } = await api.get<Company[]>('/companies')
      companies.value = data
    } catch {
      companies.value = []
    } finally {
      loading.value = false
    }
  }

  /** Point the active id at a company we actually belong to (default: the first). */
  function ensureActive() {
    if (companies.value.length === 0) {
      setActive(null)
      return
    }
    if (!companies.value.some((c) => c.tenantId === activeCompanyId.value)) {
      setActive(companies.value[0].tenantId)
    }
  }

  async function switchCompany(id: number) {
    if (id === activeCompanyId.value) return
    setActive(id) // written to localStorage first so the next requests carry the header
    const scope = useScopeStore()
    scope.reset()
    await scope.fetchAll()
    await fetchCompanies() // refresh the isActive flags
  }

  async function createCompany(name: string) {
    const { data } = await api.post<Company>('/companies', { name })
    await fetchCompanies()
    await switchCompany(data.tenantId)
    return data
  }

  async function renameCompany(id: number, name: string) {
    await api.put(`/companies/${id}`, { name })
    await fetchCompanies()
  }

  return {
    companies,
    activeCompanyId,
    activeCompany,
    hasMultiple,
    loading,
    fetchCompanies,
    ensureActive,
    switchCompany,
    createCompany,
    renameCompany,
    setActive,
  }
})
