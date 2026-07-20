<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ChevronDown, ChevronRight, Plus, Trash2, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { ageOn } from '@/lib/age'
import type { Gender, Guardian, Performer, PerformerGuardian } from '@/types'

const performers = ref<Performer[]>([])
const search = ref('')

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

// --- Add form (collapsible; captures guardian + notes in one step) -----------

const addOpen = ref(false)
const form = ref({
  firstName: '',
  lastName: '',
  gender: '' as Gender | '',
  dateOfBirth: '',
  notes: '',
  guardianName: '',
  guardianEmail: '',
  guardianPhone: '',
})

function resetForm() {
  form.value = {
    firstName: '', lastName: '', gender: '', dateOfBirth: '', notes: '',
    guardianName: '', guardianEmail: '', guardianPhone: '',
  }
}

async function addPerformer() {
  const f = form.value
  if (!f.firstName.trim()) return
  const { data: performer } = await api.post<Performer>('/performers', {
    id: 0,
    firstName: f.firstName.trim(),
    lastName: f.lastName.trim(),
    gender: f.gender || null,
    dateOfBirth: f.dateOfBirth || null,
    notes: f.notes.trim() || null,
    isActive: true,
    createdAt: new Date().toISOString(),
  })
  performers.value.push(performer)

  if (f.guardianName.trim()) {
    const { data: guardian } = await api.post<Guardian>('/guardians', {
      id: 0,
      name: f.guardianName.trim(),
      email: f.guardianEmail.trim() || null,
      phone: f.guardianPhone.trim() || null,
    })
    guardians.value.push(guardian)
    guardianLinks.value.push({ performerId: performer.id, guardianId: guardian.id })
    await api.post('/performerguardians', { performerId: performer.id, guardianId: guardian.id }).catch(() => {})
  }

  toast.success(`${performer.firstName} added`)
  resetForm()
}

// --- Rows (collapsible) ------------------------------------------------------

const openRows = ref<Set<number>>(new Set())
function toggleRow(id: number) {
  const s = openRows.value
  s.has(id) ? s.delete(id) : s.add(id)
  openRows.value = new Set(s)
}

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  const list = q
    ? performers.value.filter((p) => `${p.firstName} ${p.lastName}`.toLowerCase().includes(q))
    : performers.value
  return [...list].sort((a, b) =>
    (a.lastName + a.firstName).localeCompare(b.lastName + b.firstName),
  )
})

// --- Per-performer edits -----------------------------------------------------

async function savePerformer(performer: Performer) {
  await api.put(`/performers/${performer.id}`, performer).catch(() => {})
}
async function saveNotes(performer: Performer, e: Event) {
  performer.notes = (e.target as HTMLTextAreaElement).value.trim() || null
  await savePerformer(performer)
}
async function setDateOfBirth(performer: Performer, e: Event) {
  performer.dateOfBirth = (e.target as HTMLInputElement).value || null
  await savePerformer(performer)
}
async function setGender(performer: Performer, e: Event) {
  performer.gender = ((e.target as HTMLSelectElement).value || null) as Gender | null
  await savePerformer(performer)
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

// --- Guardians ---------------------------------------------------------------

const linkGuardianId = ref<Record<number, number | ''>>({})
const newGuardian = ref<Record<number, { name: string; email: string; phone: string }>>({})
const gFor = (id: number) => (newGuardian.value[id] ??= { name: '', email: '', phone: '' })

const guardiansOf = (performerId: number) =>
  guardianLinks.value
    .filter((l) => l.performerId === performerId)
    .map((l) => guardians.value.find((g) => g.id === l.guardianId))
    .filter((g): g is Guardian => !!g)

const linkableFor = (performerId: number) =>
  guardians.value.filter((g) => !guardiansOf(performerId).some((x) => x.id === g.id))

async function linkGuardian(performerId: number) {
  const guardianId = linkGuardianId.value[performerId]
  if (!guardianId) return
  guardianLinks.value.push({ performerId, guardianId })
  linkGuardianId.value[performerId] = ''
  await api.post('/performerguardians', { performerId, guardianId }).catch(() => {})
}

async function createGuardian(performerId: number) {
  const g = gFor(performerId)
  if (!g.name.trim()) return
  const { data } = await api.post<Guardian>('/guardians', {
    id: 0,
    name: g.name.trim(),
    email: g.email.trim() || null,
    phone: g.phone.trim() || null,
  })
  guardians.value.push(data)
  guardianLinks.value.push({ performerId, guardianId: data.id })
  await api.post('/performerguardians', { performerId, guardianId: data.id }).catch(() => {})
  newGuardian.value[performerId] = { name: '', email: '', phone: '' }
  toast.success(`${data.name} linked`)
}

async function unlinkGuardian(performerId: number, guardianId: number) {
  guardianLinks.value = guardianLinks.value.filter(
    (l) => !(l.performerId === performerId && l.guardianId === guardianId),
  )
  await api.delete(`/performerguardians?performerId=${performerId}&guardianId=${guardianId}`).catch(() => {})
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-5 p-6">
    <div class="flex items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-bold">Performers</h1>
        <p class="text-sm text-muted-foreground">
          Your company's performers across all seasons — one record each, history spans years.
        </p>
      </div>
      <button
        class="flex shrink-0 items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
        @click="addOpen = !addOpen"
      >
        <Plus class="h-4 w-4" /> Add performer
      </button>
    </div>

    <!-- Add form -->
    <form v-if="addOpen" class="space-y-3 rounded-lg border border-border p-4" @submit.prevent="addPerformer">
      <div class="flex flex-wrap items-end gap-2">
        <label class="flex-1 space-y-1">
          <span class="text-xs font-medium text-muted-foreground">First name</span>
          <input v-model="form.firstName" class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
        </label>
        <label class="flex-1 space-y-1">
          <span class="text-xs font-medium text-muted-foreground">Last name</span>
          <input v-model="form.lastName" class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
        </label>
        <label class="space-y-1">
          <span class="text-xs font-medium text-muted-foreground">Date of birth</span>
          <input v-model="form.dateOfBirth" type="date" class="block rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
        </label>
        <label class="space-y-1">
          <span class="text-xs font-medium text-muted-foreground">Gender</span>
          <select v-model="form.gender" class="block w-20 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="">—</option>
            <option v-for="o in genderOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>
        </label>
      </div>
      <div class="grid gap-2 sm:grid-cols-3">
        <input v-model="form.guardianName" placeholder="Guardian name (optional)" class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="form.guardianEmail" type="email" placeholder="Guardian email" class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="form.guardianPhone" placeholder="Guardian phone" class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <textarea v-model="form.notes" rows="2" placeholder="Notes — allergies, sizes, family info… (optional)" class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" />
      <div class="flex justify-end gap-2">
        <button type="button" class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent" @click="addOpen = false; resetForm()">Cancel</button>
        <button type="submit" class="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90">Add performer</button>
      </div>
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
      <li v-for="performer in filtered" :key="performer.id">
        <div class="flex items-center gap-2 px-3 py-2.5 text-sm">
          <button class="flex flex-1 items-center gap-2 text-left" @click="toggleRow(performer.id)">
            <ChevronDown v-if="openRows.has(performer.id)" class="h-4 w-4 shrink-0 text-muted-foreground" />
            <ChevronRight v-else class="h-4 w-4 shrink-0 text-muted-foreground" />
            <span class="font-medium">{{ performer.firstName }} {{ performer.lastName }}</span>
            <span v-if="genderLabel(performer.gender)" class="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">{{ genderLabel(performer.gender) }}</span>
            <span v-if="ageOn(performer.dateOfBirth) !== null" class="text-xs text-muted-foreground">{{ ageOn(performer.dateOfBirth) }} yrs</span>
            <span v-if="guardiansOf(performer.id).length > 0" class="text-xs text-muted-foreground">· {{ guardiansOf(performer.id).length }} guardian{{ guardiansOf(performer.id).length === 1 ? '' : 's' }}</span>
          </button>
          <button
            class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
            :aria-label="`Delete ${performer.firstName}`"
            :title="`Delete ${performer.firstName}`"
            @click="removePerformer(performer)"
          >
            <Trash2 class="h-4 w-4" />
          </button>
        </div>

        <!-- Expanded detail -->
        <div v-if="openRows.has(performer.id)" class="space-y-3 border-t border-border bg-muted/20 px-4 py-3">
          <div class="flex flex-wrap items-end gap-3">
            <label class="space-y-1">
              <span class="text-xs font-medium text-muted-foreground">Date of birth</span>
              <input type="date" class="block rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring" :value="performer.dateOfBirth ?? ''" @change="setDateOfBirth(performer, $event)" />
            </label>
            <label class="space-y-1">
              <span class="text-xs font-medium text-muted-foreground">Gender</span>
              <select class="block w-20 rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring" :value="performer.gender ?? ''" @change="setGender(performer, $event)">
                <option value="">—</option>
                <option v-for="o in genderOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
              </select>
            </label>
          </div>

          <!-- Guardians -->
          <div>
            <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Guardians</h3>
            <ul v-if="guardiansOf(performer.id).length > 0" class="mt-1.5 space-y-1">
              <li v-for="g in guardiansOf(performer.id)" :key="g.id" class="flex items-center gap-2 text-xs">
                <span class="font-medium">{{ g.name }}</span>
                <a v-if="g.email" :href="`mailto:${g.email}`" class="text-primary hover:underline">{{ g.email }}</a>
                <span v-if="g.phone" class="text-muted-foreground">{{ g.phone }}</span>
                <button class="ml-auto rounded p-0.5 text-muted-foreground hover:text-destructive" :aria-label="`Unlink ${g.name}`" @click="unlinkGuardian(performer.id, g.id)">
                  <X class="h-3.5 w-3.5" />
                </button>
              </li>
            </ul>
            <form v-if="linkableFor(performer.id).length > 0" class="mt-2 flex gap-2" @submit.prevent="linkGuardian(performer.id)">
              <select v-model="linkGuardianId[performer.id]" class="flex-1 rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none">
                <option value="">Link existing guardian (siblings share)…</option>
                <option v-for="g in linkableFor(performer.id)" :key="g.id" :value="g.id">{{ g.name }}{{ g.email ? ` (${g.email})` : '' }}</option>
              </select>
              <button type="submit" class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent">Link</button>
            </form>
            <form class="mt-2 flex flex-wrap gap-2" @submit.prevent="createGuardian(performer.id)">
              <input v-model="gFor(performer.id).name" placeholder="New guardian name" class="w-0 min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
              <input v-model="gFor(performer.id).email" type="email" placeholder="Email" class="w-0 min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
              <input v-model="gFor(performer.id).phone" placeholder="Phone" class="w-24 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" />
              <button type="submit" class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent" aria-label="Add guardian" title="Add guardian"><Plus class="h-3.5 w-3.5" /></button>
            </form>
          </div>

          <!-- Notes -->
          <label class="block space-y-1">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Notes <span class="font-normal normal-case">(all shows)</span></span>
            <textarea
              :value="performer.notes ?? ''"
              rows="2"
              placeholder="Constant notes — allergies, sizes, family info… (per-show notes live in the Planner)"
              class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveNotes(performer, $event)"
            />
          </label>
        </div>
      </li>
    </ul>
  </div>
</template>
