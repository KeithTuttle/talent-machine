import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/lib/api'
import type { StaffMember } from '@/types'

/**
 * Tenant-wide directory of non-performer people (creative team + invitees),
 * cached so autocomplete pickers across the app share one growing list.
 */
export const useStaffStore = defineStore('staff', () => {
  const members = ref<StaffMember[]>([])
  const loaded = ref(false)

  async function fetch(force = false) {
    if (loaded.value && !force) return
    members.value = await api.get<StaffMember[]>('/staffmembers').then((r) => r.data).catch(() => [])
    loaded.value = true
  }

  async function create(input: { name: string; email?: string | null; phone?: string | null }) {
    const { data } = await api.post<StaffMember>('/staffmembers', {
      id: 0,
      name: input.name.trim(),
      email: input.email?.trim() || null,
      phone: input.phone?.trim() || null,
    })
    members.value.push(data)
    return data
  }

  const byId = (id: number) => members.value.find((m) => m.id === id) ?? null

  return { members, loaded, fetch, create, byId }
})
