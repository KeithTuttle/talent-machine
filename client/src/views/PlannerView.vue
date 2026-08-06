<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  ArrowDown,
  ArrowUp,
  ChevronRight,
  FileText,
  Music,
  Plus,
  Shirt,
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
import CastMemberDrawer from '@/components/CastMemberDrawer.vue'
import CastOverviewGrid from '@/components/CastOverviewGrid.vue'
import NumberDrawer from '@/components/NumberDrawer.vue'
import StaffPicker from '@/components/StaffPicker.vue'
import { teachIcon, TEACH_STATUSES, TEACH_LABELS } from '@/lib/teach'
import { useScopeStore } from '@/stores/scope'
import { useStaffStore } from '@/stores/staff'
import type { TeachStatus } from '@/types'
import type {
  Act,
  CastGroup,
  CastMembership,
  CostumeAssignment,
  CostumePiece,
  Gender,
  MusicalNumber,
  NumberCast,
  NumberCharacter,
  Performer,
  Role,
  SceneCharacter,
} from '@/types'

const genderOptions: { value: Gender; label: string }[] = [
  { value: 'Male', label: 'Male' },
  { value: 'Female', label: 'Female' },
  { value: 'NonBinary', label: 'Non-binary' },
]

const scope = useScopeStore()
const staff = useStaffStore()

const numbers = ref<MusicalNumber[]>([])
const acts = ref<Act[]>([])
const cast = ref<CastMembership[]>([])
const groups = ref<CastGroup[]>([])
const roles = ref<Role[]>([])
const numberCasts = ref<NumberCast[]>([])
const performers = ref<Performer[]>([])
const sceneChars = ref<SceneCharacter[]>([])
const numberChars = ref<NumberCharacter[]>([])
const costumePieces = ref<CostumePiece[]>([])
const costumeAssignments = ref<CostumeAssignment[]>([])

const selectedNumberId = ref<number | null>(null)
const sideTab = ref<'cast' | 'groups' | 'roles'>('cast')
/** Plan (numbers + casting editor) vs Overview (kids × numbers grid). */
const view = ref<'plan' | 'overview'>(
  (localStorage.getItem('planner.view') as 'plan' | 'overview') ?? 'plan',
)
watch(view, (v) => localStorage.setItem('planner.view', v))
/** Cast member open in the per-kid drawer (notes + conflicts). */
const drawerMember = ref<CastMembership | null>(null)
/** Number open in the per-number drawer (costumes + formations). */
const drawerNumber = ref<MusicalNumber | null>(null)

const hasNotes = (m: CastMembership) => !!(m.notes || m.performer?.notes)

/** Distinct costume labels in use (for reuse chips in the number drawer). */
const costumeLabels = computed(() =>
  [...new Set(numbers.value.map((n) => n.costumeLabel?.trim()).filter((l): l is string => !!l))].sort(),
)

/** Fetch a costume-related PDF and hand it to the browser as a download. */
async function downloadPdf(path: string, filename: string) {
  try {
    const { data } = await api.get(`${path}?productionId=${scope.selectedProductionId}`, { responseType: 'blob' })
    const url = URL.createObjectURL(data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    toast.error("Couldn't generate the PDF — is the server running?")
  }
}

const downloadCostumePdf = () => downloadPdf('/costumes/pdf', 'costumes.pdf')
const downloadQuickChangePdf = () => downloadPdf('/costumes/quickchanges/pdf', 'quick-changes.pdf')
const downloadCostumePlotPdf = () => downloadPdf('/costumes/plot/pdf', 'costume-plot.pdf')

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
    sceneChars.value = []
    numberChars.value = []
    costumePieces.value = []
    costumeAssignments.value = []
    return
  }
  ;[
    numbers.value,
    acts.value,
    cast.value,
    groups.value,
    roles.value,
    numberCasts.value,
    performers.value,
    sceneChars.value,
    numberChars.value,
    costumePieces.value,
    costumeAssignments.value,
  ] = await Promise.all([
    safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
    safeGet<Act>(`/acts?productionId=${pid}`),
    safeGet<CastMembership>(`/castmemberships?productionId=${pid}`),
    safeGet<CastGroup>(`/castgroups?productionId=${pid}`),
    safeGet<Role>(`/roles?productionId=${pid}`),
    safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
    safeGet<Performer>('/performers'),
    safeGet<SceneCharacter>(`/scenecharacters?productionId=${pid}`),
    safeGet<NumberCharacter>(`/numbercharacters?productionId=${pid}`),
    safeGet<CostumePiece>(`/costumepieces?productionId=${pid}`),
    safeGet<CostumeAssignment>(`/costumeassignments?productionId=${pid}`),
  ])
  if (!numbers.value.some((n) => n.id === selectedNumberId.value))
    selectedNumberId.value = numbers.value[0]?.id ?? null
}

/** Refresh costume pieces/assignments after the number drawer edits looks, so the
 * overview's per-performer quick-change stays accurate without a full reload. */
async function reloadCostumes() {
  const pid = scope.selectedProductionId
  if (pid === null) return
  ;[costumePieces.value, costumeAssignments.value] = await Promise.all([
    safeGet<CostumePiece>(`/costumepieces?productionId=${pid}`),
    safeGet<CostumeAssignment>(`/costumeassignments?productionId=${pid}`),
  ])
}

onMounted(() => {
  loadAll()
  staff.fetch()
})
watch(() => scope.selectedProductionId, loadAll)

// --- Derived -----------------------------------------------------------------

const selectedNumber = computed(
  () => numbers.value.find((n) => n.id === selectedNumberId.value) ?? null,
)

/** Numbers in show running order: within each act by orderIndex, acts in order,
 * act-less last. Mirrors ShowOrderView / the overview grid. */
const orderedNumbers = computed(() => {
  const ordered = [...numbers.value].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
  if (acts.value.length === 0) return ordered
  const out: MusicalNumber[] = []
  for (const act of [...acts.value].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id))
    out.push(...ordered.filter((n) => n.actId === act.id))
  out.push(...ordered.filter((n) => n.actId == null))
  return out
})

/** The same list grouped under act headers (for the numbers panel). */
const numberGroups = computed(() => {
  const ordered = orderedNumbers.value
  if (acts.value.length === 0) return [{ header: null as string | null, numbers: ordered }]
  const out = [...acts.value]
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
    .map((act) => ({ header: act.name as string | null, numbers: ordered.filter((n) => n.actId === act.id) }))
  const unassigned = ordered.filter((n) => n.actId == null)
  if (unassigned.length > 0) out.push({ header: 'Unassigned', numbers: unassigned })
  return out.filter((g) => g.numbers.length > 0)
})

/** Continuous 1-based running position across the whole show. */
const runningIndex = (n: MusicalNumber) => orderedNumbers.value.indexOf(n) + 1

/** The character a cast member plays IF that character is featured in this number. */
const characterOf = (numberId: number, performerId: number) => {
  const role = roles.value.find(
    (r) =>
      r.performerId === performerId &&
      numberChars.value.some((nc) => nc.musicalNumberId === numberId && nc.roleId === r.id),
  )
  return role?.name ?? null
}

const performerName = (id: number) => {
  const p = cast.value.find((m) => m.performerId === id)?.performer ?? performers.value.find((x) => x.id === id)
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}

/** Age as of the production's show date (fallback: today); null when DoB unknown. */
const performerAge = (id: number) => {
  const p =
    cast.value.find((m) => m.performerId === id)?.performer ??
    performers.value.find((x) => x.id === id)
  return ageOn(p?.dateOfBirth, scope.selectedProduction?.openingDate)
}

const castGroupOf = (m: CastMembership) => groups.value.find((g) => g.id === m.castGroupId) ?? null

/** The color a member's chip carries in the checklist (their cast group's). */
const memberColor = (m: CastMembership) => castGroupOf(m)?.color ?? null

interface Bucket {
  group: CastGroup | null
  members: CastMembership[]
}

/** Production cast bucketed by cast group (plus an "Ungrouped" bucket). */
function bucketize(defs: CastGroup[], idOf: (m: CastMembership) => number | null) {
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

async function setChoreographer(staffId: number | null) {
  const n = selectedNumber.value
  if (!n) return
  const prev = n.choreographerStaffId ?? null
  n.choreographerStaffId = staffId
  try {
    // Send a clean scalar payload (drop the choreographer nav object, mirroring
    // setMemberGroup) so the write can't be rejected by a stale nested entity.
    await api.put(`/numbers/${n.id}`, { ...n, choreographer: undefined })
  } catch {
    n.choreographerStaffId = prev // revert so the UI matches the server
    toast.error("Couldn't save the choreographer — please try again.")
  }
}

async function setTeachStatus(e: Event) {
  if (!selectedNumber.value) return
  const raw = (e.target as HTMLSelectElement).value
  selectedNumber.value.teachStatus = (raw || null) as TeachStatus | null
  await api.put(`/numbers/${selectedNumber.value.id}`, selectedNumber.value).catch(() => {})
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
  // Nudge within the running order (act-grouped). Cross-act moves are the Show
  // Order page's job; here a swap resequences the global running order.
  const list = [...orderedNumbers.value]
  const i = list.indexOf(number)
  const j = i + delta
  if (j < 0 || j >= list.length) return
  ;[list[i], list[j]] = [list[j], list[i]]
  // Reflect locally by rewriting orderIndex so the computed re-sorts immediately.
  list.forEach((n, idx) => (n.orderIndex = idx + 1))
  await api
    .put('/numbers/reorder', {
      productionId: scope.selectedProductionId,
      orderedIds: list.map((n) => n.id),
    })
    .catch(() => {})
}

// --- Number casting ----------------------------------------------------------

async function toggleCastFor(numberId: number, performerId: number) {
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

async function toggleCast(performerId: number) {
  if (selectedNumberId.value === null) return
  await toggleCastFor(selectedNumberId.value, performerId)
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
const quickGender = ref<Gender | ''>('')
const newGroupName = ref('')
const newRoleName = ref('')

// --- "Add a kid" type-ahead: suggest existing performers, kids first ---------
const castQuery = ref('')
const castPickerOpen = ref(false)

/** Performers not yet in this show, 18-or-younger (or unknown age), alphabetical. */
const suggestablePerformers = computed(() =>
  availablePerformers.value
    .filter((p) => {
      const age = performerAge(p.id)
      return age === null || age <= 18
    })
    .sort((a, b) =>
      `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`),
    ),
)
const castMatches = computed(() => {
  const q = castQuery.value.trim().toLowerCase()
  return suggestablePerformers.value
    .filter((p) => q === '' || `${p.firstName} ${p.lastName}`.toLowerCase().includes(q))
    .slice(0, 8)
})

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

async function pickExistingToCast(p: Performer) {
  addPerformerId.value = p.id
  await addExistingToCast()
  castQuery.value = ''
  castPickerOpen.value = false
}

async function quickCreatePerformer() {
  const pid = scope.selectedProductionId
  if (pid === null || !quickFirst.value.trim()) return
  const { data: performer } = await api.post<Performer>('/performers', {
    id: 0,
    firstName: quickFirst.value.trim(),
    lastName: quickLast.value.trim(),
    gender: quickGender.value || null,
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
  quickGender.value = ''
}

async function setMemberGroup(member: CastMembership, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  member.castGroupId = raw === '' ? null : Number(raw)
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

/** How many scenes / numbers a character is linked to (shown in the cast list). */
const roleAppearsIn = (roleId: number) => ({
  scenes: sceneChars.value.filter((sc) => sc.roleId === roleId).length,
  numbers: numberChars.value.filter((nc) => nc.roleId === roleId).length,
})

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
  <div class="flex h-full flex-col p-6">
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

    <div v-else class="flex min-h-0 flex-1 flex-col gap-4">
      <div class="flex shrink-0 flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">{{ scope.selectedProduction?.title }}</h1>
          <p class="text-sm text-muted-foreground">{{ scope.selectedSeason?.name }}</p>
        </div>
        <div class="flex rounded-md border border-border text-sm">
          <button
            v-for="v in ([['plan', 'Plan'], ['overview', 'Overview']] as const)"
            :key="v[0]"
            class="px-3 py-1.5 font-medium"
            :class="view === v[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="view = v[0]"
          >
            {{ v[1] }}
          </button>
        </div>
      </div>

      <!-- Overview: the kids × numbers grid (fills the remaining height) -->
      <div v-if="view === 'overview'" class="flex min-h-0 flex-1 flex-col">
        <CastOverviewGrid
          :numbers="numbers"
          :acts="acts"
          :cast="cast"
          :groups="groups"
          :number-casts="numberCasts"
          :costume-pieces="costumePieces"
          :costume-assignments="costumeAssignments"
          :performer-name="performerName"
          :performer-age="performerAge"
          @toggle="toggleCastFor"
          @open-member="drawerMember = $event"
          @open-number="drawerNumber = $event"
          @costume-pdf="downloadCostumePdf"
          @quick-change-pdf="downloadQuickChangePdf"
          @costume-plot-pdf="downloadCostumePlotPdf"
        />
      </div>

      <div v-else class="grid min-h-0 flex-1 gap-6 overflow-y-auto lg:grid-cols-[minmax(0,22rem)_1fr]">
      <!-- LEFT: numbers list + side panels -->
      <div class="space-y-4">

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
        <div v-else class="space-y-3">
          <div v-for="grp in numberGroups" :key="grp.header ?? 'flat'">
            <h3 v-if="grp.header" class="px-1 pb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {{ grp.header }}
            </h3>
            <ul class="space-y-1">
              <li v-for="number in grp.numbers" :key="number.id">
                <button
                  class="flex w-full items-center gap-2 rounded-md border px-3 py-2 text-left text-sm transition-colors"
                  :class="
                    number.id === selectedNumberId
                      ? 'border-primary bg-accent text-accent-foreground'
                      : 'border-border hover:bg-accent/50'
                  "
                  @click="selectedNumberId = number.id"
                >
                  <span class="w-5 shrink-0 text-xs text-muted-foreground">{{ runningIndex(number) }}.</span>
                  <span class="flex-1 truncate font-medium">{{ number.title }}</span>
                  <component
                    :is="teachIcon(number.teachStatus)!.icon"
                    v-if="teachIcon(number.teachStatus)"
                    class="h-3.5 w-3.5 shrink-0"
                    :class="teachIcon(number.teachStatus)!.class"
                    :aria-label="teachIcon(number.teachStatus)!.label"
                  />
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
          </div>
        </div>

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
            <!-- Add an existing kid: type-ahead over past performers (18 & under first) -->
            <div v-if="availablePerformers.length > 0" class="relative">
              <input
                v-model="castQuery"
                placeholder="Add a kid — type to search…"
                class="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                @focus="castPickerOpen = true"
                @keydown.escape="castPickerOpen = false"
                @keydown.enter.prevent="castMatches[0] && pickExistingToCast(castMatches[0])"
              />
              <div
                v-if="castPickerOpen && castMatches.length > 0"
                class="absolute z-20 mt-1 max-h-56 w-full overflow-y-auto rounded-md border border-border bg-popover p-1 shadow-md"
              >
                <button
                  v-for="p in castMatches"
                  :key="p.id"
                  type="button"
                  class="flex w-full items-center justify-between gap-2 rounded-sm px-2 py-1.5 text-left text-sm hover:bg-accent"
                  @mousedown.prevent="pickExistingToCast(p)"
                >
                  <span class="truncate">{{ p.firstName }} {{ p.lastName }}</span>
                  <span v-if="performerAge(p.id) !== null" class="shrink-0 text-xs text-muted-foreground">{{ performerAge(p.id) }}</span>
                </button>
              </div>
              <div v-if="castPickerOpen" class="fixed inset-0 z-10" @mousedown="castPickerOpen = false" />
            </div>
            <!-- Quick-create a brand-new kid: name row, then sex + add -->
            <form class="space-y-2" @submit.prevent="quickCreatePerformer">
              <div class="flex gap-2">
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
              </div>
              <div class="flex gap-2">
                <select
                  v-model="quickGender"
                  class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  aria-label="Sex"
                >
                  <option value="">Sex…</option>
                  <option v-for="o in genderOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
                </select>
                <button type="submit" class="rounded-md border border-border px-2.5 text-sm hover:bg-accent" aria-label="Quick-add performer">
                  <Plus class="h-4 w-4" />
                </button>
              </div>
            </form>
            <p v-if="cast.length === 0" class="text-center text-xs text-muted-foreground">Nobody in this production yet.</p>
            <ul class="space-y-1">
              <li v-for="m in cast" :key="m.id" class="flex items-center gap-2 text-sm">
                <ColorDot :color="castGroupOf(m)?.color" size="sm" />
                <button
                  class="flex min-w-0 flex-1 items-center gap-1 text-left underline decoration-muted-foreground/40 decoration-dotted underline-offset-2 hover:text-primary hover:decoration-primary"
                  :title="`Open ${performerName(m.performerId)}'s notes & conflicts`"
                  @click="drawerMember = m"
                >
                  <span class="truncate">{{ performerName(m.performerId) }}</span>
                  <span v-if="performerAge(m.performerId) !== null" class="text-xs text-muted-foreground">
                    {{ performerAge(m.performerId) }}
                  </span>
                  <FileText v-if="hasNotes(m)" class="h-3 w-3 shrink-0 text-muted-foreground" />
                </button>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="m.castGroupId ?? ''"
                  aria-label="Cast group"
                  title="Cast group"
                  @change="setMemberGroup(m, $event)"
                >
                  <option value="">No group</option>
                  <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
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
            <p v-if="roles.length === 0" class="text-center text-xs text-muted-foreground">No characters yet.</p>
            <ul class="space-y-2">
              <li v-for="r in roles" :key="r.id" class="space-y-0.5">
                <div class="flex items-center gap-2 text-sm">
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
                </div>
                <p class="pl-0.5 text-xs text-muted-foreground">
                  Appears in {{ roleAppearsIn(r.id).scenes }}
                  {{ roleAppearsIn(r.id).scenes === 1 ? 'scene' : 'scenes' }} ·
                  {{ roleAppearsIn(r.id).numbers }}
                  {{ roleAppearsIn(r.id).numbers === 1 ? 'number' : 'numbers' }}
                </p>
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
              <div class="flex flex-wrap gap-3">
                <label class="flex-1 space-y-1">
                  <span class="text-xs font-medium text-muted-foreground">Choreographer</span>
                  <div v-if="selectedNumber.choreographerStaffId" class="flex items-center gap-1.5">
                    <span class="flex-1 truncate rounded-md border border-border bg-background px-2.5 py-1.5 text-sm">
                      {{ staff.byId(selectedNumber.choreographerStaffId)?.name ?? 'Unknown' }}
                    </span>
                    <button
                      class="rounded p-1 text-muted-foreground hover:text-destructive"
                      title="Clear choreographer"
                      @click="setChoreographer(null)"
                    >
                      <Trash2 class="h-3.5 w-3.5" />
                    </button>
                  </div>
                  <StaffPicker v-else placeholder="Assign choreographer…" @select="setChoreographer($event.id)" />
                </label>
                <label class="space-y-1">
                  <span class="text-xs font-medium text-muted-foreground">Teaching</span>
                  <select
                    class="block rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                    :value="selectedNumber.teachStatus ?? ''"
                    @change="setTeachStatus($event)"
                  >
                    <option value="">Not taught</option>
                    <option v-for="s in TEACH_STATUSES" :key="s" :value="s">{{ TEACH_LABELS[s] }}</option>
                  </select>
                </label>
              </div>
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
          <button
            class="mt-3 flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="drawerNumber = selectedNumber"
          >
            <Shirt class="h-4 w-4" /> Costumes &amp; formations
          </button>
        </div>

        <!-- Casting checklist -->
        <div class="rounded-lg border border-border p-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold">
              Cast in “{{ selectedNumber.title }}”
              <span class="ml-1 font-normal text-muted-foreground">({{ castCountOf(selectedNumber.id) }})</span>
            </h2>
          </div>
          <p v-if="cast.length === 0" class="mt-3 text-sm text-muted-foreground">
            Add performers to the production first (Cast tab on the left).
          </p>
          <div v-else class="mt-3 space-y-4">
            <div v-for="bucket in castByGroup" :key="bucket.group?.id ?? 'ungrouped'">
              <div class="flex items-center justify-between">
                <h3 class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <ColorDot :color="bucket.group?.color" size="sm" />
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
                    <span v-if="characterOf(selectedNumber.id, m.performerId)" class="text-xs font-medium text-primary">
                      — {{ characterOf(selectedNumber.id, m.performerId) }}
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

    <CastMemberDrawer
      :member="drawerMember"
      :cast-group="drawerMember ? castGroupOf(drawerMember) : null"
      :opening-date="scope.selectedProduction?.openingDate"
      @close="drawerMember = null"
    />
    <NumberDrawer
      :number="drawerNumber"
      :cast="cast"
      :groups="groups"
      :performers="performers"
      :number-casts="numberCasts"
      :costume-labels="costumeLabels"
      @close="drawerNumber = null; reloadCostumes()"
      @toggle-cast="toggleCastFor"
    />
  </div>
</template>
