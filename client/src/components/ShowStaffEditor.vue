<script setup lang="ts">
// Per-show creative team: assign directors, choreographers, music directors,
// producers from the tenant staff directory (returning people autocomplete).
import { computed, onMounted, ref, watch } from 'vue'
import { X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { useStaffStore } from '@/stores/staff'
import StaffPicker from '@/components/StaffPicker.vue'
import { STAFF_ROLES, STAFF_ROLE_LABELS } from '@/types'
import type { ProductionStaff, StaffMember, StaffRole } from '@/types'

const props = defineProps<{ productionId: number }>()

const staff = useStaffStore()
const assignments = ref<ProductionStaff[]>([])

async function load() {
  await staff.fetch()
  assignments.value = await api
    .get<ProductionStaff[]>(`/productionstaff?productionId=${props.productionId}`)
    .then((r) => r.data)
    .catch(() => [])
}
onMounted(load)
watch(() => props.productionId, load)

const byRole = (role: StaffRole) =>
  assignments.value
    .filter((a) => a.role === role)
    .map((a) => staff.byId(a.staffMemberId))
    .filter((m): m is StaffMember => !!m)

const assignedIds = (role: StaffRole) => byRole(role).map((m) => m.id)

async function assign(role: StaffRole, member: StaffMember) {
  if (assignments.value.some((a) => a.role === role && a.staffMemberId === member.id)) return
  assignments.value.push({ productionId: props.productionId, staffMemberId: member.id, role })
  await api
    .post('/productionstaff', { productionId: props.productionId, staffMemberId: member.id, role })
    .catch(() => {})
}

async function remove(role: StaffRole, member: StaffMember) {
  assignments.value = assignments.value.filter(
    (a) => !(a.role === role && a.staffMemberId === member.id),
  )
  await api
    .delete(`/productionstaff?productionId=${props.productionId}&staffMemberId=${member.id}&role=${role}`)
    .catch(() => {})
}

const contactOf = computed(() => (id: number) => {
  const m = staff.byId(id)
  return m ? [m.email, m.phone].filter(Boolean).join(' · ') : ''
})
</script>

<template>
  <div>
    <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Creative team</h3>
    <div class="mt-2 grid gap-3 sm:grid-cols-2">
      <div v-for="role in STAFF_ROLES" :key="role" class="space-y-1.5">
        <span class="text-xs font-medium">{{ STAFF_ROLE_LABELS[role] }}</span>
        <ul v-if="byRole(role).length > 0" class="space-y-1">
          <li
            v-for="m in byRole(role)"
            :key="m.id"
            class="flex items-center gap-1.5 rounded-md bg-background px-2 py-1 text-sm"
          >
            <span class="flex-1 truncate">
              {{ m.name }}
              <span v-if="contactOf(m.id)" class="text-xs text-muted-foreground">· {{ contactOf(m.id) }}</span>
            </span>
            <button
              class="rounded p-0.5 text-muted-foreground hover:text-destructive"
              :aria-label="`Remove ${m.name}`"
              :title="`Remove ${m.name}`"
              @click="remove(role, m)"
            >
              <X class="h-3.5 w-3.5" />
            </button>
          </li>
        </ul>
        <StaffPicker
          :placeholder="`Add ${STAFF_ROLE_LABELS[role].toLowerCase()}…`"
          :exclude-ids="assignedIds(role)"
          @select="assign(role, $event)"
        />
      </div>
    </div>
  </div>
</template>
