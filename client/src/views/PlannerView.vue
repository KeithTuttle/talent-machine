<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  ArrowDown,
  ArrowUp,
  ChevronRight,
  Music,
  Plus,
  Trash2,
  Users,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import type { CastGroup, CastMembership, MusicalNumber, NumberCast, Person, Role } from '@/types'

const scope = useScopeStore()

const numbers = ref<MusicalNumber[]>([])
const cast = ref<CastMembership[]>([])
const groups = ref<CastGroup[]>([])
const roles = ref<Role[]>([])
const numberCasts = ref<NumberCast[]>([])
const people = ref<Person[]>([])

const selectedNumberId = ref<number | null>(null)
const sideTab = ref<'cast' | 'groups' | 'roles'>('cast')

// --- Loading -----------------------------------------------------------------

async function safeGet<T>(url: string): Promise<T[]> {
  try {
    const { data } = await api.get<T[]>(url)
    return data
  } catch {
    return []
  }
}

async function loadAll() {
  const pid = scope.selectedProductionId
  if (pid === null) {
    numbers.value = []
    cast.value = []
    groups.value = []
    roles.value = []
    numberCasts.value = []
    return
  }
  ;[numbers.value, cast.value, groups.value, roles.value, numberCasts.value, people.value] =
    await Promise.all([
      safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
      safeGet<CastMembership>(`/castmemberships?productionId=${pid}`),
      safeGet<CastGroup>(`/castgroups?productionId=${pid}`),
      safeGet<Role>(`/roles?productionId=${pid}`),
      safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
      safeGet<Person>('/people'),
    ])
  if (!numbers.value.some((n) => n.id === selectedNumberId.value))
    selectedNumberId.value = numbers.value[0]?.id ?? null
}

onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Derived -----------------------------------------------------------------

const selectedNumber = computed(
  () => numbers.value.find((n) => n.id === selectedNumberId.value) ?? null,
)

const personName = (id: number) => {
  const p = cast.value.find((m) => m.personId === id)?.person ?? people.value.find((x) => x.id === id)
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}

/** Production cast grouped by cast group (plus an "Ungrouped" bucket). */
const castByGroup = computed(() => {
  const buckets: { group: CastGroup | null; members: CastMembership[] }[] = [
    ...groups.value.map((g) => ({ group: g as CastGroup | null, members: [] as CastMembership[] })),
    { group: null, members: [] },
  ]
  for (const m of cast.value) {
    const bucket = buckets.find((b) => (b.group?.id ?? null) === (m.castGroupId ?? null))
    ;(bucket ?? buckets[buckets.length - 1]).members.push(m)
  }
  for (const b of buckets)
    b.members.sort((x, y) => personName(x.personId).localeCompare(personName(y.personId)))
  return buckets.filter((b) => b.group !== null || b.members.length > 0)
})

const castCountOf = (numberId: number) =>
  numberCasts.value.filter((c) => c.musicalNumberId === numberId).length

const isCast = (numberId: number, personId: number) =>
  numberCasts.value.some((c) => c.musicalNumberId === numberId && c.personId === personId)

/** People not yet in this production (for the "add to cast" picker). */
const availablePeople = computed(() =>
  people.value.filter((p) => !cast.value.some((m) => m.personId === p.id)),
)

// --- Numbers -----------------------------------------------------------------

const newNumberTitle = ref('')

async function addNumber() {
  const pid = scope.selectedProductionId
  const title = newNumberTitle.value.trim()
  if (pid === null || !title) return
  const { data } = await api.post<MusicalNumber>('/numbers', {
    id: 0,
    productionId: pid,
    title,
    orderIndex: 0,
  })
  numbers.value.push(data)
  selectedNumberId.value = data.id
  newNumberTitle.value = ''
}

async function saveNumber(number: MusicalNumber) {
  await api.put(`/numbers/${number.id}`, number)
  toast.success('Saved')
}

async function deleteNumber(number: MusicalNumber) {
  const ok = await confirm({
    title: `Delete “${number.title}”?`,
    message: 'Its casting is removed too.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/numbers/${number.id}`)
  numbers.value = numbers.value.filter((n) => n.id !== number.id)
  if (selectedNumberId.value === number.id) selectedNumberId.value = numbers.value[0]?.id ?? null
}

async function moveNumber(number: MusicalNumber, delta: -1 | 1) {
  const i = numbers.value.indexOf(number)
  const j = i + delta
  if (j < 0 || j >= numbers.value.length) return
  const list = [...numbers.value]
  ;[list[i], list[j]] = [list[j], list[i]]
  numbers.value = list
  await api
    .put('/numbers/reorder', {
      productionId: scope.selectedProductionId,
      orderedIds: list.map((n) => n.id),
    })
    .catch(() => {})
}

// --- Number casting ----------------------------------------------------------

async function toggleCast(personId: number) {
  const numberId = selectedNumberId.value
  if (numberId === null) return
  if (isCast(numberId, personId)) {
    numberCasts.value = numberCasts.value.filter(
      (c) => !(c.musicalNumberId === numberId && c.personId === personId),
    )
    await api.delete(`/numbercast?numberId=${numberId}&personId=${personId}`).catch(() => {})
  } else {
    numberCasts.value.push({ musicalNumberId: numberId, personId })
    await api.post('/numbercast', { musicalNumberId: numberId, personId }).catch(() => {})
  }
}

async function addGroupToNumber(bucket: { group: CastGroup | null; members: CastMembership[] }) {
  const numberId = selectedNumberId.value
  if (numberId === null) return
  const missing = bucket.members.filter((m) => !isCast(numberId, m.personId))
  for (const m of missing) {
    numberCasts.value.push({ musicalNumberId: numberId, personId: m.personId })
  }
  await Promise.all(
    missing.map((m) =>
      api.post('/numbercast', { musicalNumberId: numberId, personId: m.personId }).catch(() => {}),
    ),
  )
}

// --- Production cast / groups / roles ---------------------------------------

const addPersonId = ref<number | ''>('')
const quickFirst = ref('')
const quickLast = ref('')
const newGroupName = ref('')
const newRoleName = ref('')

async function addExistingToCast() {
  const pid = scope.selectedProductionId
  if (pid === null || addPersonId.value === '') return
  const { data } = await api.post<CastMembership>('/castmemberships', {
    id: 0,
    productionId: pid,
    personId: addPersonId.value,
  })
  cast.value.push(data)
  addPersonId.value = ''
}

async function quickCreatePerson() {
  const pid = scope.selectedProductionId
  if (pid === null || !quickFirst.value.trim()) return
  const { data: person } = await api.post<Person>('/people', {
    id: 0,
    firstName: quickFirst.value.trim(),
    lastName: quickLast.value.trim(),
    isActive: true,
    createdAt: new Date().toISOString(),
  })
  people.value.push(person)
  const { data: membership } = await api.post<CastMembership>('/castmemberships', {
    id: 0,
    productionId: pid,
    personId: person.id,
  })
  cast.value.push(membership)
  toast.success(`${person.firstName} added to the cast`)
  quickFirst.value = ''
  quickLast.value = ''
}

async function setMemberGroup(member: CastMembership, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  member.castGroupId = raw === '' ? null : Number(raw)
  await api.put(`/castmemberships/${member.id}`, { ...member, person: undefined }).catch(() => {})
}

async function removeFromCast(member: CastMembership) {
  const ok = await confirm({
    title: `Remove ${personName(member.personId)} from this production?`,
    destructive: true,
    confirmText: 'Remove',
  })
  if (!ok) return
  await api.delete(`/castmemberships/${member.id}`)
  cast.value = cast.value.filter((m) => m.id !== member.id)
}

async function addGroup() {
  const pid = scope.selectedProductionId
  const name = newGroupName.value.trim()
  if (pid === null || !name) return
  const { data } = await api.post<CastGroup>('/castgroups', {
    id: 0,
    productionId: pid,
    name,
    orderIndex: groups.value.length + 1,
  })
  groups.value.push(data)
  newGroupName.value = ''
}

async function deleteGroup(group: CastGroup) {
  const ok = await confirm({
    title: `Delete group “${group.name}”?`,
    message: 'People in it stay in the show, just ungrouped.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/castgroups/${group.id}`)
  groups.value = groups.value.filter((g) => g.id !== group.id)
  for (const m of cast.value) if (m.castGroupId === group.id) m.castGroupId = null
}

async function addRole() {
  const pid = scope.selectedProductionId
  const name = newRoleName.value.trim()
  if (pid === null || !name) return
  const { data } = await api.post<Role>('/roles', {
    id: 0,
    productionId: pid,
    name,
    orderIndex: roles.value.length + 1,
  })
  roles.value.push(data)
  newRoleName.value = ''
}

async function setRolePerson(role: Role, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  role.personId = raw === '' ? null : Number(raw)
  await api.put(`/roles/${role.id}`, role).catch(() => {})
}

async function deleteRole(role: Role) {
  if (!(await confirm({ title: `Delete role “${role.name}”?`, destructive: true, confirmText: 'Delete' }))) return
  await api.delete(`/roles/${role.id}`)
  roles.value = roles.value.filter((r) => r.id !== role.id)
}
</script>

<template>
  <div class="p-6">
    <!-- Empty scope state -->
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <Music class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Create a season and a production first, then plan its numbers here.
      </p>
      <RouterLink
        to="/seasons"
        class="mt-4 inline-block rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
      >
        Set up a season
      </RouterLink>
    </div>

    <div v-else class="grid gap-6 lg:grid-cols-[minmax(0,22rem)_1fr]">
      <!-- LEFT: numbers list + side panels -->
      <div class="space-y-4">
        <div>
          <h1 class="font-display text-2xl font-bold">{{ scope.selectedProduction?.title }}</h1>
          <p class="text-sm text-muted-foreground">{{ scope.selectedSeason?.name }}</p>
        </div>

        <form class="flex gap-2" @submit.prevent="addNumber">
          <input
            v-model="newNumberTitle"
            placeholder="New number (e.g. Tomorrow)"
            class="flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          />
          <button
            type="submit"
            class="flex items-center gap-1 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90"
          >
            <Plus class="h-4 w-4" /> Add
          </button>
        </form>

        <p v-if="numbers.length === 0" class="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
          No numbers yet.
        </p>
        <ul v-else class="space-y-1">
          <li v-for="(number, i) in numbers" :key="number.id">
            <button
              class="flex w-full items-center gap-2 rounded-md border px-3 py-2 text-left text-sm transition-colors"
              :class="
                number.id === selectedNumberId
                  ? 'border-primary bg-accent text-accent-foreground'
                  : 'border-border hover:bg-accent/50'
              "
              @click="selectedNumberId = number.id"
            >
              <span class="w-5 shrink-0 text-xs text-muted-foreground">{{ i + 1 }}.</span>
              <span class="flex-1 truncate font-medium">{{ number.title }}</span>
              <span class="flex items-center gap-1 text-xs text-muted-foreground">
                <Users class="h-3.5 w-3.5" /> {{ castCountOf(number.id) }}
              </span>
              <span class="flex flex-col">
                <ArrowUp
                  class="h-3.5 w-3.5 text-muted-foreground hover:text-foreground"
                  role="button"
                  aria-label="Move up"
                  @click.stop="moveNumber(number, -1)"
                />
                <ArrowDown
                  class="h-3.5 w-3.5 text-muted-foreground hover:text-foreground"
                  role="button"
                  aria-label="Move down"
                  @click.stop="moveNumber(number, 1)"
                />
              </span>
            </button>
          </li>
        </ul>

        <!-- Side panel: cast / groups / roles -->
        <div class="rounded-lg border border-border">
          <div class="flex border-b border-border text-sm">
            <button
              v-for="tab in (['cast', 'groups', 'roles'] as const)"
              :key="tab"
              class="flex-1 px-3 py-2 font-medium capitalize"
              :class="sideTab === tab ? 'border-b-2 border-primary text-primary' : 'text-muted-foreground hover:text-foreground'"
              @click="sideTab = tab"
            >
              {{ tab }}
            </button>
          </div>

          <!-- Cast tab -->
          <div v-if="sideTab === 'cast'" class="space-y-3 p-3">
            <form v-if="availablePeople.length > 0" class="flex gap-2" @submit.prevent="addExistingToCast">
              <select
                v-model="addPersonId"
                class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              >
                <option value="" disabled>Add existing person…</option>
                <option v-for="p in availablePeople" :key="p.id" :value="p.id">
                  {{ p.firstName }} {{ p.lastName }}
                </option>
              </select>
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent">Add</button>
            </form>
            <form class="flex gap-2" @submit.prevent="quickCreatePerson">
              <input
                v-model="quickFirst"
                placeholder="First"
                class="w-0 flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
              <input
                v-model="quickLast"
                placeholder="Last"
                class="w-0 flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent" aria-label="Quick-add person">
                <Plus class="h-4 w-4" />
              </button>
            </form>
            <p v-if="cast.length === 0" class="text-center text-xs text-muted-foreground">Nobody in this production yet.</p>
            <ul class="space-y-1">
              <li v-for="m in cast" :key="m.id" class="flex items-center gap-2 text-sm">
                <span class="flex-1 truncate">{{ personName(m.personId) }}</span>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="m.castGroupId ?? ''"
                  @change="setMemberGroup(m, $event)"
                >
                  <option value="">No group</option>
                  <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
                </select>
                <button
                  class="rounded p-1 text-muted-foreground hover:text-destructive"
                  :aria-label="`Remove ${personName(m.personId)}`"
                  @click="removeFromCast(m)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </button>
              </li>
            </ul>
          </div>

          <!-- Groups tab -->
          <div v-else-if="sideTab === 'groups'" class="space-y-3 p-3">
            <form class="flex gap-2" @submit.prevent="addGroup">
              <input
                v-model="newGroupName"
                placeholder="New group (e.g. Leads)"
                class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent">Add</button>
            </form>
            <p v-if="groups.length === 0" class="text-center text-xs text-muted-foreground">No groups yet.</p>
            <ul class="space-y-1">
              <li v-for="g in groups" :key="g.id" class="flex items-center justify-between text-sm">
                <span>{{ g.name }}</span>
                <span class="flex items-center gap-2">
                  <span class="text-xs text-muted-foreground">
                    {{ cast.filter((m) => m.castGroupId === g.id).length }} people
                  </span>
                  <button
                    class="rounded p-1 text-muted-foreground hover:text-destructive"
                    :aria-label="`Delete ${g.name}`"
                    @click="deleteGroup(g)"
                  >
                    <Trash2 class="h-3.5 w-3.5" />
                  </button>
                </span>
              </li>
            </ul>
          </div>

          <!-- Roles tab -->
          <div v-else class="space-y-3 p-3">
            <form class="flex gap-2" @submit.prevent="addRole">
              <input
                v-model="newRoleName"
                placeholder="New role (e.g. Annie)"
                class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent">Add</button>
            </form>
            <p v-if="roles.length === 0" class="text-center text-xs text-muted-foreground">No roles yet.</p>
            <ul class="space-y-1">
              <li v-for="r in roles" :key="r.id" class="flex items-center gap-2 text-sm">
                <span class="flex-1 truncate font-medium">{{ r.name }}</span>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="r.personId ?? ''"
                  @change="setRolePerson(r, $event)"
                >
                  <option value="">Uncast</option>
                  <option v-for="m in cast" :key="m.personId" :value="m.personId">
                    {{ personName(m.personId) }}
                  </option>
                </select>
                <button
                  class="rounded p-1 text-muted-foreground hover:text-destructive"
                  :aria-label="`Delete role ${r.name}`"
                  @click="deleteRole(r)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </button>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <!-- RIGHT: selected number editor -->
      <div v-if="selectedNumber" class="space-y-4">
        <div class="rounded-lg border border-border p-4">
          <div class="flex items-start justify-between gap-3">
            <div class="flex-1 space-y-3">
              <label class="block space-y-1">
                <span class="text-xs font-medium text-muted-foreground">Title</span>
                <input
                  v-model="selectedNumber.title"
                  class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm font-medium focus:outline-none focus:ring-1 focus:ring-ring"
                  @change="saveNumber(selectedNumber)"
                />
              </label>
              <label class="block space-y-1">
                <span class="text-xs font-medium text-muted-foreground">Songwriter / composer</span>
                <input
                  v-model="selectedNumber.songwriter"
                  class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  @change="saveNumber(selectedNumber)"
                />
              </label>
              <label class="block space-y-1">
                <span class="text-xs font-medium text-muted-foreground">Notes</span>
                <textarea
                  v-model="selectedNumber.notes"
                  rows="3"
                  class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  @change="saveNumber(selectedNumber)"
                />
              </label>
            </div>
            <button
              class="rounded-md p-2 text-muted-foreground hover:bg-accent hover:text-destructive"
              aria-label="Delete number"
              @click="deleteNumber(selectedNumber)"
            >
              <Trash2 class="h-4 w-4" />
            </button>
          </div>
        </div>

        <!-- Casting checklist -->
        <div class="rounded-lg border border-border p-4">
          <h2 class="text-sm font-semibold">
            Cast in “{{ selectedNumber.title }}”
            <span class="ml-1 font-normal text-muted-foreground">({{ castCountOf(selectedNumber.id) }})</span>
          </h2>
          <p v-if="cast.length === 0" class="mt-3 text-sm text-muted-foreground">
            Add people to the production first (Cast tab on the left).
          </p>
          <div v-else class="mt-3 space-y-4">
            <div v-for="bucket in castByGroup" :key="bucket.group?.id ?? 'ungrouped'">
              <div class="flex items-center justify-between">
                <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {{ bucket.group?.name ?? 'Ungrouped' }}
                </h3>
                <button
                  v-if="bucket.members.length > 0"
                  class="text-xs font-medium text-primary hover:underline"
                  @click="addGroupToNumber(bucket)"
                >
                  Add all
                </button>
              </div>
              <div class="mt-1.5 grid grid-cols-1 gap-1 sm:grid-cols-2 xl:grid-cols-3">
                <label
                  v-for="m in bucket.members"
                  :key="m.id"
                  class="flex cursor-pointer items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent/50"
                  :class="isCast(selectedNumber.id, m.personId) ? 'border-primary bg-accent' : ''"
                >
                  <input
                    type="checkbox"
                    class="accent-[hsl(var(--primary))]"
                    :checked="isCast(selectedNumber.id, m.personId)"
                    @change="toggleCast(m.personId)"
                  />
                  <span class="truncate">{{ personName(m.personId) }}</span>
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="flex items-center justify-center rounded-lg border border-dashed border-border p-12 text-sm text-muted-foreground">
        <span class="flex items-center gap-2">
          <ChevronRight class="h-4 w-4" /> Add or select a number to edit it
        </span>
      </div>
    </div>
  </div>
</template>
