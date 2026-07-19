<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Plus, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { ageOn } from '@/lib/age'
import type { Gender, Performer } from '@/types'

const performers = ref<Performer[]>([])
const search = ref('')
const firstName = ref('')
const lastName = ref('')
const gender = ref<Gender | ''>('')
const dateOfBirth = ref('')

const genderOptions: { value: Gender; label: string }[] = [
  { value: 'Male', label: 'M' },
  { value: 'Female', label: 'F' },
  { value: 'NonBinary', label: 'NB' },
]
const genderLabel = (g?: Gender | null) => genderOptions.find((o) => o.value === g)?.label

onMounted(async () => {
  performers.value = await api.get<Performer[]>('/performers').then((r) => r.data).catch(() => [])
})

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return performers.value
  return performers.value.filter((p) =>
    `${p.firstName} ${p.lastName}`.toLowerCase().includes(q),
  )
})

async function addPerformer() {
  if (!firstName.value.trim()) return
  const { data } = await api.post<Performer>('/performers', {
    id: 0,
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    gender: gender.value || null,
    dateOfBirth: dateOfBirth.value || null,
    isActive: true,
    createdAt: new Date().toISOString(),
  })
  performers.value.push(data)
  toast.success(`${data.firstName} added`)
  firstName.value = ''
  lastName.value = ''
  gender.value = ''
  dateOfBirth.value = ''
}

async function setDateOfBirth(performer: Performer, e: Event) {
  performer.dateOfBirth = (e.target as HTMLInputElement).value || null
  await api.put(`/performers/${performer.id}`, performer).catch(() => {})
}

async function setGender(performer: Performer, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  performer.gender = (raw || null) as Gender | null
  await api.put(`/performers/${performer.id}`, performer).catch(() => {})
}

async function removePerformer(performer: Performer) {
  const ok = await confirm({
    title: `Delete ${performer.firstName} ${performer.lastName}?`,
    message: 'Their production history and number casting will be deleted too.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/performers/${performer.id}`)
  performers.value = performers.value.filter((p) => p.id !== performer.id)
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6 p-6">
    <div>
      <h1 class="font-display text-2xl font-bold">Performers</h1>
      <p class="text-sm text-muted-foreground">
        Your company's performers across all seasons — one record each, history spans years.
      </p>
    </div>

    <form class="flex flex-wrap items-end gap-2 rounded-lg border border-border p-4" @submit.prevent="addPerformer">
      <label class="flex-1 space-y-1">
        <span class="text-xs font-medium text-muted-foreground">First name</span>
        <input
          v-model="firstName"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <label class="flex-1 space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Last name</span>
        <input
          v-model="lastName"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <label class="space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Date of birth</span>
        <input
          v-model="dateOfBirth"
          type="date"
          class="block rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <label class="space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Gender</span>
        <select
          v-model="gender"
          class="block w-24 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        >
          <option value="">—</option>
          <option v-for="o in genderOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
        </select>
      </label>
      <button
        type="submit"
        class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
      >
        <Plus class="h-4 w-4" /> Add performer
      </button>
    </form>

    <input
      v-model="search"
      placeholder="Search performers…"
      class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
    />

    <p v-if="filtered.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      {{ performers.length === 0 ? 'No performers yet — add your first above.' : 'No matches.' }}
    </p>
    <ul v-else class="divide-y divide-border rounded-lg border border-border">
      <li
        v-for="performer in filtered"
        :key="performer.id"
        class="flex items-center gap-3 px-4 py-2.5 text-sm"
      >
        <span class="flex-1">
          {{ performer.firstName }} {{ performer.lastName }}
          <span v-if="genderLabel(performer.gender)" class="ml-1.5 rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
            {{ genderLabel(performer.gender) }}
          </span>
          <span v-if="ageOn(performer.dateOfBirth) !== null" class="ml-1.5 text-xs text-muted-foreground">
            {{ ageOn(performer.dateOfBirth) }} yrs
          </span>
        </span>
        <input
          type="date"
          class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
          :value="performer.dateOfBirth ?? ''"
          aria-label="Date of birth"
          @change="setDateOfBirth(performer, $event)"
        />
        <select
          class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
          :value="performer.gender ?? ''"
          aria-label="Gender"
          @change="setGender(performer, $event)"
        >
          <option value="">—</option>
          <option v-for="o in genderOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
        </select>
        <button
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
          :aria-label="`Delete ${performer.firstName}`"
          @click="removePerformer(performer)"
        >
          <Trash2 class="h-4 w-4" />
        </button>
      </li>
    </ul>
  </div>
</template>
