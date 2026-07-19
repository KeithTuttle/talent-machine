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
import { ageOn } from '@/lib/age'
import { tint } from '@/lib/colors'
import ColorDot from '@/components/ColorDot.vue'
import ColorPicker from '@/components/ColorPicker.vue'
import { useScopeStore } from '@/stores/scope'
import type {
  CastGroup,
  CastMembership,
  LevelGroup,
  MusicalNumber,
  NumberCast,
  Performer,
  Role,
} from '@/types'

const scope = useScopeStore()

const numbers = ref<MusicalNumber[]>([])
const cast = ref<CastMembership[]>([])
const groups = ref<CastGroup[]>([])
const levelGroups = ref<LevelGroup[]>([])
const roles = ref<Role[]>([])
const numberCasts = ref<NumberCast[]>([])
const performers = ref<Performer[]>([])

const selectedNumberId = ref<number | null>(null)
const sideTab = ref<'cast' | 'groups' | 'levels' | 'roles'>('cast')
/** Which grouping the casting checklist buckets by. */
const checklistBy = ref<'cast' | 'level'>('cast')

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
    levelGroups.value = []
    roles.value = []
    numberCasts.value = []
    return
  }
  ;[
    numbers.value,
    cast.value,
    groups.value,
    levelGroups.value,
    roles.value,
    numberCasts.value,
    performers.value,
  ] = await Promise.all([
    safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
    safeGet<CastMembership>(`/castmemberships?productionId=${pid}`),
    safeGet<CastGroup>(`/castgroups?productionId=${pid}`),
    safeGet<LevelGroup>(`/levelgroups?productionId=${pid}`),
    safeGet<Role>(`/roles?productionId=${pid}`),
    safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
    safeGet<Performer>('/performers'),
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

const performerName = (id: number) => {
  const p = cast.value.find((m) => m.performerId === id)?.performer ?? performers.value.find((x) => x.id === id)
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}

/** Age as of the production's show date (fallback: today); null when DoB unknown. */
const performerAge = (id: number) => {
  const p =
    cast.value.find((m) => m.performerId === id)?.performer ??
    performers.value.find((x) => x.id === id)
  return ageOn(p?.dateOfBirth, scope.selectedProduction?.showDate)
}

const castGroupOf = (m: CastMembership) => groups.value.find((g) => g.id === m.castGroupId) ?? null
const levelGroupOf = (m: CastMembership) =>
  levelGroups.value.find((g) => g.id === m.levelGroupId) ?? null

/** The color a member carries in the checklist (from whichever grouping is active). */
const memberColor = (m: CastMembership) =>
  (checklistBy.value === 'level' ? levelGroupOf(m) : castGroupOf(m))?.color ?? null

interface Bucket {
  group: (CastGroup | LevelGroup) | null
  members: CastMembership[]
}

/** Production cast bucketed by a grouping (plus an "Ungrouped" bucket). */
function bucketize(defs: (CastGroup | LevelGroup)[], idOf: (m: CastMembership) => number | null) {
  const buckets: Bucket[] = [
    ...defs.map((g) => ({ group: g as Bucket['group'], members: [] as CastMembership[] })),
    { group: null, members: [] },
  ]
  for (const m of cast.value) {
    const bucket = buckets.find((b) => (b.group?.id ?? null) === (idOf(m) ?? null))
    ;(bucket ?? buckets[buckets.length - 1]).members.push(m)
  }
  for (const b of buckets)
    b.members.sort((x, y) => performerName(x.performerId).localeCompare(performerName(y.performerId)))
  return buckets.filter((b) => b.group !== null || b.members.length > 0)
}

const castByGroup = computed(() => bucketize(groups.value, (m) => m.castGroupId ?? null))
const castByLevel = computed(() => bucketize(levelGroups.value, (m) => m.levelGroupId ?? null))
const checklistBuckets = computed(() =>
  checklistBy.value === 'level' ? castByLevel.value : castByGroup.value,
)

const castCountOf = (numberId: number) =>
  numberCasts.value.filter((c) => c.musicalNumberId === numberId).length

const isCast = (numberId: number, performerId: number) =>
  numberCasts.value.some((c) => c.musicalNumberId === numberId && c.performerId === performerId)

/** Performers not yet in this production (for the "add to cast" picker). */
const availablePerformers = computed(() =>
  performers.value.filter((p) => !cast.value.some((m) => m.performerId === p.id)),
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

async function toggleCast(performerId: number) {
  const numberId = selectedNumberId.value
  if (numberId === null) return
  if (isCast(numberId, performerId)) {
    numberCasts.value = numberCasts.value.filter(
      (c) => !(c.musicalNumberId === numberId && c.performerId === performerId),
    )
    await api.delete(`/numbercast?numberId=${numberId}&performerId=${performerId}`).catch(() => {})
  } else {
    numberCasts.value.push({ musicalNumberId: numberId, performerId })
    await api.post('/numbercast', { musicalNumberId: numberId, performerId }).catch(() => {})
  }
}

async function addGroupToNumber(bucket: Bucket) {
  const numberId = selectedNumberId.value
  if (numberId === null) return
  const missing = bucket.members.filter((m) => !isCast(numberId, m.performerId))
  for (const m of missing) {
    numberCasts.value.push({ musicalNumberId: numberId, performerId: m.performerId })
  }
  await Promise.all(
    missing.map((m) =>
      api.post('/numbercast', { musicalNumberId: numberId, performerId: m.performerId }).catch(() => {}),
    ),
  )
}

// --- Production cast / groups / roles ---------------------------------------

const addPerformerId = ref<number | ''>('')
const quickFirst = ref('')
const quickLast = ref('')
const newGroupName = ref('')
const newRoleName = ref('')

async function addExistingToCast() {
  const pid = scope.selectedProductionId
  if (pid === null || addPerformerId.value === '') return
  const { data } = await api.post<CastMembership>('/castmemberships', {
    id: 0,
    productionId: pid,
    performerId: addPerformerId.value,
  })
  cast.value.push(data)
  addPerformerId.value = ''
}

async function quickCreatePerformer() {
  const pid = scope.selectedProductionId
  if (pid === null || !quickFirst.value.trim()) return
  const { data: performer } = await api.post<Performer>('/performers', {
    id: 0,
    firstName: quickFirst.value.trim(),
    lastName: quickLast.value.trim(),
    isActive: true,
    createdAt: new Date().toISOString(),
  })
  performers.value.push(performer)
  const { data: membership } = await api.post<CastMembership>('/castmemberships', {
    id: 0,
    productionId: pid,
    performerId: performer.id,
  })
  cast.value.push(membership)
  toast.success(`${performer.firstName} added to the cast`)
  quickFirst.value = ''
  quickLast.value = ''
}

async function setMemberGroup(member: CastMembership, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  member.castGroupId = raw === '' ? null : Number(raw)
  await api.put(`/castmemberships/${member.id}`, { ...member, performer: undefined }).catch(() => {})
}

async function setMemberLevel(member: CastMembership, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  member.levelGroupId = raw === '' ? null : Number(raw)
  await api.put(`/castmemberships/${member.id}`, { ...member, performer: undefined }).catch(() => {})
}

async function removeFromCast(member: CastMembership) {
  const ok = await confirm({
    title: `Remove ${performerName(member.performerId)} from this production?`,
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

async function saveGroupColor(group: CastGroup) {
  await api.put(`/castgroups/${group.id}`, group).catch(() => {})
}

// --- Level groups (age/ability) ---------------------------------------------

const newLevelName = ref('')
const newLevelLabel = ref('')
const newLevelMinAge = ref<number | ''>('')
const newLevelMaxAge = ref<number | ''>('')

async function addLevelGroup() {
  const pid = scope.selectedProductionId
  const name = newLevelName.value.trim()
  if (pid === null || !name) return
  const { data } = await api.post<LevelGroup>('/levelgroups', {
    id: 0,
    productionId: pid,
    name,
    level: newLevelLabel.value.trim() || null,
    minAge: newLevelMinAge.value === '' ? null : newLevelMinAge.value,
    maxAge: newLevelMaxAge.value === '' ? null : newLevelMaxAge.value,
    orderIndex: levelGroups.value.length + 1,
  })
  levelGroups.value.push(data)
  newLevelName.value = ''
  newLevelLabel.value = ''
  newLevelMinAge.value = ''
  newLevelMaxAge.value = ''
}

async function saveLevelGroup(group: LevelGroup) {
  await api.put(`/levelgroups/${group.id}`, group).catch(() => {})
}

async function deleteLevelGroup(group: LevelGroup) {
  const ok = await confirm({
    title: `Delete level group “${group.name}”?`,
    message: 'Kids in it stay in the show, just unleveled.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/levelgroups/${group.id}`)
  levelGroups.value = levelGroups.value.filter((g) => g.id !== group.id)
  for (const m of cast.value) if (m.levelGroupId === group.id) m.levelGroupId = null
}

/** Age-range description like "8–12", "8+", or "to 12". */
function ageRangeLabel(group: LevelGroup) {
  if (group.minAge != null && group.maxAge != null) return `${group.minAge}–${group.maxAge}`
  if (group.minAge != null) return `${group.minAge}+`
  if (group.maxAge != null) return `to ${group.maxAge}`
  return null
}

/** True when a member's age is known and outside the group's range. */
function outOfRange(group: LevelGroup, m: CastMembership) {
  const age = performerAge(m.performerId)
  if (age === null) return false
  if (group.minAge != null && age < group.minAge) return true
  if (group.maxAge != null && age > group.maxAge) return true
  return false
}

async function deleteGroup(group: CastGroup) {
  const ok = await confirm({
    title: `Delete group “${group.name}”?`,
    message: 'Performers in it stay in the show, just ungrouped.',
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

async function setRolePerformer(role: Role, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  role.performerId = raw === '' ? null : Number(raw)
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
              v-for="tab in (['cast', 'groups', 'levels', 'roles'] as const)"
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
            <form v-if="availablePerformers.length > 0" class="flex gap-2" @submit.prevent="addExistingToCast">
              <select
                v-model="addPerformerId"
                class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              >
                <option value="" disabled>Add existing performer…</option>
                <option v-for="p in availablePerformers" :key="p.id" :value="p.id">
                  {{ p.firstName }} {{ p.lastName }}
                </option>
              </select>
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent">Add</button>
            </form>
            <form class="flex gap-2" @submit.prevent="quickCreatePerformer">
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
              <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent" aria-label="Quick-add performer">
                <Plus class="h-4 w-4" />
              </button>
            </form>
            <p v-if="cast.length === 0" class="text-center text-xs text-muted-foreground">Nobody in this production yet.</p>
            <ul class="space-y-1">
              <li v-for="m in cast" :key="m.id" class="flex items-center gap-2 text-sm">
                <ColorDot :color="castGroupOf(m)?.color" size="sm" />
                <ColorDot :color="levelGroupOf(m)?.color" size="sm" />
                <span class="flex-1 truncate">
                  {{ performerName(m.performerId) }}
                  <span v-if="performerAge(m.performerId) !== null" class="ml-1 text-xs text-muted-foreground">
                    {{ performerAge(m.performerId) }}
                  </span>
                </span>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="m.castGroupId ?? ''"
                  aria-label="Cast group"
                  @change="setMemberGroup(m, $event)"
                >
                  <option value="">No group</option>
                  <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
                </select>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="m.levelGroupId ?? ''"
                  aria-label="Level group"
                  @change="setMemberLevel(m, $event)"
                >
                  <option value="">No level</option>
                  <option v-for="g in levelGroups" :key="g.id" :value="g.id">{{ g.name }}</option>
                </select>
                <button
                  class="rounded p-1 text-muted-foreground hover:text-destructive"
                  :aria-label="`Remove ${performerName(m.performerId)}`"
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
            <ul class="space-y-2">
              <li v-for="g in groups" :key="g.id" class="space-y-1 text-sm">
                <div class="flex items-center justify-between">
                  <span class="flex items-center gap-1.5">
                    <ColorDot :color="g.color" />
                    {{ g.name }}
                  </span>
                  <span class="flex items-center gap-2">
                    <span class="text-xs text-muted-foreground">
                      {{ cast.filter((m) => m.castGroupId === g.id).length }} performers
                    </span>
                    <button
                      class="rounded p-1 text-muted-foreground hover:text-destructive"
                      :aria-label="`Delete ${g.name}`"
                      @click="deleteGroup(g)"
                    >
                      <Trash2 class="h-3.5 w-3.5" />
                    </button>
                  </span>
                </div>
                <ColorPicker
                  :model-value="g.color ?? null"
                  @update:model-value="(v: string | null | undefined) => { g.color = v ?? null; saveGroupColor(g) }"
                />
              </li>
            </ul>
          </div>

          <!-- Levels tab (age/ability groups) -->
          <div v-else-if="sideTab === 'levels'" class="space-y-3 p-3">
            <form class="space-y-2" @submit.prevent="addLevelGroup">
              <div class="flex gap-2">
                <input
                  v-model="newLevelName"
                  placeholder="New level group (e.g. Gold Group)"
                  class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                />
                <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent">Add</button>
              </div>
              <div class="flex gap-2">
                <input
                  v-model="newLevelLabel"
                  placeholder="Ability (e.g. Advanced)"
                  class="w-0 flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                />
                <input
                  v-model.number="newLevelMinAge"
                  type="number"
                  min="0"
                  placeholder="Min age"
                  class="w-20 rounded-md border border-border bg-background px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                />
                <input
                  v-model.number="newLevelMaxAge"
                  type="number"
                  min="0"
                  placeholder="Max age"
                  class="w-20 rounded-md border border-border bg-background px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                />
              </div>
            </form>
            <p v-if="levelGroups.length === 0" class="text-center text-xs text-muted-foreground">
              No level groups yet — group kids by age &amp; ability per show.
            </p>
            <ul class="space-y-3">
              <li v-for="g in levelGroups" :key="g.id" class="space-y-1 text-sm">
                <div class="flex items-center justify-between">
                  <span class="flex items-center gap-1.5">
                    <ColorDot :color="g.color" />
                    <span class="font-medium">{{ g.name }}</span>
                    <span v-if="g.level" class="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">{{ g.level }}</span>
                    <span v-if="ageRangeLabel(g)" class="text-xs text-muted-foreground">{{ ageRangeLabel(g) }}</span>
                  </span>
                  <button
                    class="rounded p-1 text-muted-foreground hover:text-destructive"
                    :aria-label="`Delete ${g.name}`"
                    @click="deleteLevelGroup(g)"
                  >
                    <Trash2 class="h-3.5 w-3.5" />
                  </button>
                </div>
                <ColorPicker
                  :model-value="g.color ?? null"
                  @update:model-value="(v: string | null | undefined) => { g.color = v ?? null; saveLevelGroup(g) }"
                />
                <ul class="ml-4 space-y-0.5">
                  <li
                    v-for="m in cast.filter((x) => x.levelGroupId === g.id)"
                    :key="m.id"
                    class="text-xs text-muted-foreground"
                  >
                    {{ performerName(m.performerId) }}
                    <span
                      v-if="performerAge(m.performerId) !== null"
                      :class="outOfRange(g, m) ? 'font-medium text-destructive' : ''"
                    >
                      · {{ performerAge(m.performerId) }}
                    </span>
                  </li>
                  <li v-if="!cast.some((x) => x.levelGroupId === g.id)" class="text-xs italic text-muted-foreground">
                    Nobody assigned (use the Cast tab)
                  </li>
                </ul>
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
                  :value="r.performerId ?? ''"
                  @change="setRolePerformer(r, $event)"
                >
                  <option value="">Uncast</option>
                  <option v-for="m in cast" :key="m.performerId" :value="m.performerId">
                    {{ performerName(m.performerId) }}
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
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold">
              Cast in “{{ selectedNumber.title }}”
              <span class="ml-1 font-normal text-muted-foreground">({{ castCountOf(selectedNumber.id) }})</span>
            </h2>
            <div class="flex rounded-md border border-border text-xs">
              <button
                v-for="mode in ([['cast', 'By group'], ['level', 'By level']] as const)"
                :key="mode[0]"
                class="px-2 py-1 font-medium"
                :class="checklistBy === mode[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'"
                @click="checklistBy = mode[0]"
              >
                {{ mode[1] }}
              </button>
            </div>
          </div>
          <p v-if="cast.length === 0" class="mt-3 text-sm text-muted-foreground">
            Add performers to the production first (Cast tab on the left).
          </p>
          <div v-else class="mt-3 space-y-4">
            <div v-for="bucket in checklistBuckets" :key="bucket.group?.id ?? 'ungrouped'">
              <div class="flex items-center justify-between">
                <h3 class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <ColorDot :color="bucket.group?.color" size="sm" />
                  {{ bucket.group?.name ?? (checklistBy === 'level' ? 'No level' : 'Ungrouped') }}
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
                  :class="isCast(selectedNumber.id, m.performerId) ? 'border-primary bg-accent' : ''"
                  :style="!isCast(selectedNumber.id, m.performerId) ? { backgroundColor: tint(memberColor(m)) } : undefined"
                >
                  <input
                    type="checkbox"
                    class="accent-[hsl(var(--primary))]"
                    :checked="isCast(selectedNumber.id, m.performerId)"
                    @change="toggleCast(m.performerId)"
                  />
                  <span class="truncate">
                    {{ performerName(m.performerId) }}
                    <span v-if="performerAge(m.performerId) !== null" class="text-xs text-muted-foreground">
                      · {{ performerAge(m.performerId) }}
                    </span>
                  </span>
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
