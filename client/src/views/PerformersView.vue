<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Contact, FileText, Plus, Trash2, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { ageOn } from '@/lib/age'
import type { Gender, Guardian, Performer, PerformerGuardian } from '@/types'

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

const guardians = ref<Guardian[]>([])
const guardianLinks = ref<PerformerGuardian[]>([])

onMounted(async () => {
  ;[performers.value, guardians.value, guardianLinks.value] = await Promise.all([
    api.get<Performer[]>('/performers').then((r) => r.data).catch(() => []),
    api.get<Guardian[]>('/guardians').then((r) => r.data).catch(() => []),
    api.get<PerformerGuardian[]>('/performerguardians').then((r) => r.data).catch(() => []),
  ])
})

// --- Guardians ---------------------------------------------------------------

const guardiansOpenFor = ref<number | null>(null)
const linkGuardianId = ref<number | ''>('')
const newGuardianName = ref('')
const newGuardianEmail = ref('')
const newGuardianPhone = ref('')

const guardiansOf = (performerId: number) =>
  guardianLinks.value
    .filter((l) => l.performerId === performerId)
    .map((l) => guardians.value.find((g) => g.id === l.guardianId))
    .filter((g): g is Guardian => !!g)

const linkableFor = (performerId: number) =>
  guardians.value.filter((g) => !guardiansOf(performerId).some((x) => x.id === g.id))

async function linkGuardian(performerId: number) {
  if (linkGuardianId.value === '') return
  const guardianId = linkGuardianId.value
  guardianLinks.value.push({ performerId, guardianId })
  linkGuardianId.value = ''
  await api.post('/performerguardians', { performerId, guardianId }).catch(() => {})
}

async function createGuardian(performerId: number) {
  const name = newGuardianName.value.trim()
  if (!name) return
  const { data } = await api.post<Guardian>('/guardians', {
    id: 0,
    name,
    email: newGuardianEmail.value.trim() || null,
    phone: newGuardianPhone.value.trim() || null,
  })
  guardians.value.push(data)
  guardianLinks.value.push({ performerId, guardianId: data.id })
  await api.post('/performerguardians', { performerId, guardianId: data.id }).catch(() => {})
  newGuardianName.value = ''
  newGuardianEmail.value = ''
  newGuardianPhone.value = ''
  toast.success(`${data.name} linked`)
}

async function unlinkGuardian(performerId: number, guardianId: number) {
  guardianLinks.value = guardianLinks.value.filter(
    (l) => !(l.performerId === performerId && l.guardianId === guardianId),
  )
  await api.delete(`/performerguardians?performerId=${performerId}&guardianId=${guardianId}`).catch(() => {})
}

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

const notesOpenFor = ref<number | null>(null)

async function saveNotes(performer: Performer, e: Event) {
  performer.notes = (e.target as HTMLTextAreaElement).value.trim() || null
  await api.put(`/performers/${performer.id}`, performer).catch(() => {})
  toast.success('Saved')
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
        class="px-4 py-2.5 text-sm"
      >
        <div class="flex items-center gap-3">
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
          class="rounded-md p-1.5"
          :class="guardiansOf(performer.id).length > 0 ? 'text-primary hover:bg-accent' : 'text-muted-foreground hover:bg-accent hover:text-foreground'"
          :aria-label="`Guardians for ${performer.firstName}`"
          @click="guardiansOpenFor = guardiansOpenFor === performer.id ? null : performer.id"
        >
          <Contact class="h-4 w-4" />
        </button>
        <button
          class="rounded-md p-1.5"
          :class="performer.notes ? 'text-primary hover:bg-accent' : 'text-muted-foreground hover:bg-accent hover:text-foreground'"
          :aria-label="`Notes for ${performer.firstName}`"
          @click="notesOpenFor = notesOpenFor === performer.id ? null : performer.id"
        >
          <FileText class="h-4 w-4" />
        </button>
        <button
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
          :aria-label="`Delete ${performer.firstName}`"
          @click="removePerformer(performer)"
        >
          <Trash2 class="h-4 w-4" />
        </button>
        </div>
        <!-- Guardians editor -->
        <div v-if="guardiansOpenFor === performer.id" class="mt-2 space-y-2 rounded-md border border-border bg-muted/30 p-3">
          <p v-if="guardiansOf(performer.id).length === 0" class="text-xs text-muted-foreground">
            No guardians yet — link an existing one (siblings share a record) or add a new one.
          </p>
          <ul v-else class="space-y-1">
            <li v-for="g in guardiansOf(performer.id)" :key="g.id" class="flex items-center gap-2 text-xs">
              <span class="font-medium">{{ g.name }}</span>
              <a v-if="g.email" :href="`mailto:${g.email}`" class="text-primary hover:underline">{{ g.email }}</a>
              <span v-if="g.phone" class="text-muted-foreground">{{ g.phone }}</span>
              <button
                class="ml-auto rounded p-0.5 text-muted-foreground hover:text-destructive"
                :aria-label="`Unlink ${g.name}`"
                @click="unlinkGuardian(performer.id, g.id)"
              >
                <X class="h-3.5 w-3.5" />
              </button>
            </li>
          </ul>
          <form v-if="linkableFor(performer.id).length > 0" class="flex gap-2" @submit.prevent="linkGuardian(performer.id)">
            <select
              v-model="linkGuardianId"
              class="flex-1 rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
            >
              <option value="" disabled>Link existing guardian…</option>
              <option v-for="g in linkableFor(performer.id)" :key="g.id" :value="g.id">
                {{ g.name }}{{ g.email ? ` (${g.email})` : '' }}
              </option>
            </select>
            <button type="submit" class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent">Link</button>
          </form>
          <form class="flex flex-wrap gap-2" @submit.prevent="createGuardian(performer.id)">
            <input v-model="newGuardianName" placeholder="New guardian name" class="w-0 min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
            <input v-model="newGuardianEmail" type="email" placeholder="Email" class="w-0 min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
            <input v-model="newGuardianPhone" placeholder="Phone" class="w-24 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
            <button type="submit" class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent" aria-label="Add guardian">
              <Plus class="h-3.5 w-3.5" />
            </button>
          </form>
        </div>

        <textarea
          v-if="notesOpenFor === performer.id"
          :value="performer.notes ?? ''"
          rows="2"
          placeholder="Constant notes — allergies, sizes, family info… (per-show notes live in the Planner)"
          class="mt-2 block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          @change="saveNotes(performer, $event)"
        />
      </li>
    </ul>
  </div>
</template>
