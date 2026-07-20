<script setup lang="ts">
// Per-number editor opened from the overview grid (and the planner): title,
// teach status, choreographer, costume label, cast, costume pieces + per-kid
// sizes, and formations. Cast toggles are emitted so the parent keeps its
// shared state in sync; costume data is loaded/saved here.
import { computed, ref, watch } from 'vue'
import { Plus, Trash2, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { useStaffStore } from '@/stores/staff'
import StaffPicker from '@/components/StaffPicker.vue'
import ColorDot from '@/components/ColorDot.vue'
import FormationEditor from '@/components/FormationEditor.vue'
import { teachIcon, TEACH_STATUSES, TEACH_LABELS } from '@/lib/teach'
import type {
  CastGroup,
  CastMembership,
  CostumeAssignment,
  CostumeGender,
  CostumePiece,
  MusicalNumber,
  NumberCast,
  Performer,
  TeachStatus,
} from '@/types'

const props = defineProps<{
  number: MusicalNumber | null
  cast: CastMembership[]
  groups: CastGroup[]
  performers: Performer[]
  numberCasts: NumberCast[]
  costumeLabels: string[]
}>()
const emit = defineEmits<{ close: []; toggleCast: [numberId: number, performerId: number] }>()

const staff = useStaffStore()
const pieces = ref<CostumePiece[]>([])
const assignments = ref<CostumeAssignment[]>([])
const tab = ref<'cast' | 'costumes' | 'formations'>('cast')

watch(
  () => props.number?.id,
  async (id) => {
    pieces.value = []
    assignments.value = []
    tab.value = 'cast'
    staff.fetch()
    if (id != null) {
      ;[pieces.value, assignments.value] = await Promise.all([
        api.get<CostumePiece[]>(`/costumepieces?numberId=${id}`).then((r) => r.data).catch(() => []),
        api.get<CostumeAssignment[]>(`/costumeassignments?numberId=${id}`).then((r) => r.data).catch(() => []),
      ])
    }
  },
  { immediate: true },
)

const performerName = (id: number) => {
  const p = props.cast.find((m) => m.performerId === id)?.performer ?? props.performers.find((x) => x.id === id)
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}
const isCast = (performerId: number) =>
  props.number != null && props.numberCasts.some((c) => c.musicalNumberId === props.number!.id && c.performerId === performerId)

// Cast grouped by cast group (+ ungrouped).
const castByGroup = computed(() => {
  const buckets = [
    ...props.groups.map((g) => ({ group: g as CastGroup | null, members: [] as CastMembership[] })),
    { group: null as CastGroup | null, members: [] as CastMembership[] },
  ]
  for (const m of props.cast) {
    const b = buckets.find((x) => (x.group?.id ?? null) === (m.castGroupId ?? null))
    ;(b ?? buckets[buckets.length - 1]).members.push(m)
  }
  for (const b of buckets) b.members.sort((x, y) => performerName(x.performerId).localeCompare(performerName(y.performerId)))
  return buckets.filter((b) => b.group !== null || b.members.length > 0)
})

// --- Number field saves ------------------------------------------------------

async function saveNumber() {
  if (!props.number) return
  await api.put(`/numbers/${props.number.id}`, props.number).catch(() => {})
}
async function setTeach(e: Event) {
  if (!props.number) return
  props.number.teachStatus = ((e.target as HTMLSelectElement).value || null) as TeachStatus | null
  await saveNumber()
}
async function setChoreographer(staffId: number | null) {
  if (!props.number) return
  props.number.choreographerStaffId = staffId
  await saveNumber()
}
function useCostumeLabel(label: string) {
  if (!props.number) return
  props.number.costumeLabel = label
  saveNumber()
}

// --- Costume pieces ----------------------------------------------------------

const GENDERS: CostumeGender[] = ['All', 'Boys', 'Girls']

async function addPiece() {
  if (!props.number) return
  const { data } = await api.post<CostumePiece>('/costumepieces', {
    id: 0,
    musicalNumberId: props.number.id,
    gender: 'All',
  })
  pieces.value.push(data)
}
async function savePiece(piece: CostumePiece) {
  await api.put(`/costumepieces/${piece.id}`, piece).catch(() => {})
}
async function deletePiece(piece: CostumePiece) {
  await api.delete(`/costumepieces/${piece.id}`)
  pieces.value = pieces.value.filter((p) => p.id !== piece.id)
}

// --- Per-kid costume detail --------------------------------------------------

const assignmentOf = (performerId: number) =>
  assignments.value.find((a) => a.performerId === performerId)

/** Cast members + any extras with an assignment. */
const wearers = computed(() => {
  const ids = new Set<number>(props.cast.filter((m) => isCast(m.performerId)).map((m) => m.performerId))
  for (const a of assignments.value) ids.add(a.performerId)
  return [...ids].sort((a, b) => performerName(a).localeCompare(performerName(b)))
})

const extraCandidates = computed(() =>
  props.cast
    .filter((m) => !wearers.value.includes(m.performerId))
    .map((m) => m.performer!)
    .filter(Boolean),
)

async function saveAssignment(performerId: number, patch: Partial<CostumeAssignment>) {
  if (!props.number) return
  let a = assignmentOf(performerId)
  if (!a) {
    a = { id: 0, musicalNumberId: props.number.id, performerId, size: null, notes: null }
    assignments.value.push(a)
  }
  Object.assign(a, patch)
  const { data } = await api.post<CostumeAssignment>('/costumeassignments', {
    id: a.id,
    musicalNumberId: props.number.id,
    performerId,
    size: a.size ?? null,
    notes: a.notes ?? null,
  })
  a.id = data.id
}

async function addExtra(e: Event) {
  const pid = Number((e.target as HTMLSelectElement).value)
  ;(e.target as HTMLSelectElement).value = ''
  if (pid) await saveAssignment(pid, {})
}

async function removeAssignment(performerId: number) {
  if (!props.number) return
  assignments.value = assignments.value.filter((a) => a.performerId !== performerId)
  await api.delete(`/costumeassignments?numberId=${props.number.id}&performerId=${performerId}`).catch(() => {})
}

// Performers cast in this number (for the formation editor).
const castPerformers = computed(() =>
  props.cast.filter((m) => isCast(m.performerId)).map((m) => m.performer!).filter(Boolean),
)
</script>

<template>
  <div v-if="number" class="fixed inset-0 z-50">
    <div class="absolute inset-0 bg-black/30" @click="emit('close')" />
    <aside class="absolute inset-y-0 right-0 flex w-full max-w-md flex-col border-l border-border bg-background shadow-xl">
      <!-- Header -->
      <div class="flex items-start justify-between gap-3 border-b border-border p-4">
        <div class="flex-1 space-y-2">
          <input
            v-model="number.title"
            class="w-full rounded-md border border-transparent bg-transparent px-1 font-display text-lg font-bold hover:border-border focus:border-border focus:outline-none"
            @change="saveNumber"
          />
          <div class="flex flex-wrap items-center gap-2">
            <label class="flex items-center gap-1 text-xs text-muted-foreground">
              Teaching
              <select class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" :value="number.teachStatus ?? ''" @change="setTeach">
                <option value="">Not taught</option>
                <option v-for="s in TEACH_STATUSES" :key="s" :value="s">{{ TEACH_LABELS[s] }}</option>
              </select>
            </label>
            <component :is="teachIcon(number.teachStatus)!.icon" v-if="teachIcon(number.teachStatus)" class="h-4 w-4" :class="teachIcon(number.teachStatus)!.class" />
          </div>
        </div>
        <button class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground" aria-label="Close" @click="emit('close')">
          <X class="h-4 w-4" />
        </button>
      </div>

      <!-- Tabs -->
      <div class="flex border-b border-border text-sm">
        <button
          v-for="t in (['cast', 'costumes', 'formations'] as const)"
          :key="t"
          class="flex-1 px-3 py-2 font-medium capitalize"
          :class="tab === t ? 'border-b-2 border-primary text-primary' : 'text-muted-foreground hover:text-foreground'"
          @click="tab = t"
        >
          {{ t }}
        </button>
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto p-4">
        <!-- Cast tab -->
        <div v-if="tab === 'cast'" class="space-y-3">
          <label class="block space-y-1">
            <span class="text-xs font-medium text-muted-foreground">Choreographer</span>
            <div v-if="number.choreographerStaffId" class="flex items-center gap-1.5">
              <span class="flex-1 truncate rounded-md border border-border bg-background px-2.5 py-1.5 text-sm">{{ staff.byId(number.choreographerStaffId)?.name ?? 'Unknown' }}</span>
              <button class="rounded p-1 text-muted-foreground hover:text-destructive" title="Clear" @click="setChoreographer(null)"><Trash2 class="h-3.5 w-3.5" /></button>
            </div>
            <StaffPicker v-else placeholder="Assign choreographer…" @select="setChoreographer($event.id)" />
          </label>

          <div v-for="bucket in castByGroup" :key="bucket.group?.id ?? 'ungrouped'">
            <h3 class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              <ColorDot :color="bucket.group?.color" size="sm" /> {{ bucket.group?.name ?? 'Ungrouped' }}
            </h3>
            <div class="mt-1.5 grid grid-cols-2 gap-1">
              <label
                v-for="m in bucket.members"
                :key="m.id"
                class="flex cursor-pointer items-center gap-2 rounded-md border border-border px-2 py-1 text-sm hover:bg-accent/50"
                :class="isCast(m.performerId) ? 'border-primary bg-accent' : ''"
              >
                <input type="checkbox" class="accent-[hsl(var(--primary))]" :checked="isCast(m.performerId)" @change="emit('toggleCast', number.id, m.performerId)" />
                <span class="truncate">{{ performerName(m.performerId) }}</span>
              </label>
            </div>
          </div>
        </div>

        <!-- Costumes tab -->
        <div v-else-if="tab === 'costumes'" class="space-y-4">
          <label class="block space-y-1">
            <span class="text-xs font-medium text-muted-foreground">Costume label</span>
            <input v-model="number.costumeLabel" placeholder="e.g. Orphan rags" class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring" @change="saveNumber" />
            <div v-if="costumeLabels.length" class="flex flex-wrap gap-1 pt-1">
              <span class="text-xs text-muted-foreground">Reuse:</span>
              <button v-for="c in costumeLabels" :key="c" class="rounded-full border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="useCostumeLabel(c)">{{ c }}</button>
            </div>
          </label>

          <!-- Pieces -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Pieces</span>
              <button class="flex items-center gap-1 rounded-md border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="addPiece"><Plus class="h-3.5 w-3.5" /> Add piece</button>
            </div>
            <div v-for="p in pieces" :key="p.id" class="space-y-1.5 rounded-md border border-border p-2.5">
              <div class="flex items-center gap-2">
                <select v-model="p.gender" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="savePiece(p)">
                  <option v-for="g in GENDERS" :key="g" :value="g">{{ g }}</option>
                </select>
                <input v-model="p.description" placeholder="Description" class="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none" @change="savePiece(p)" />
                <button class="rounded p-1 text-muted-foreground hover:text-destructive" title="Delete piece" @click="deletePiece(p)"><Trash2 class="h-3.5 w-3.5" /></button>
              </div>
              <div class="grid grid-cols-2 gap-1.5">
                <input v-model="p.accessories" placeholder="Accessories" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.shoes" placeholder="Shoes" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.photoUrl" placeholder="Photo URL" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.vendorUrl" placeholder="Vendor URL" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
              </div>
            </div>
          </div>

          <!-- Per-kid sizes -->
          <div class="space-y-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Who wears it — sizes &amp; notes</span>
            <div v-for="pid in wearers" :key="pid" class="flex flex-wrap items-center gap-1.5 text-sm">
              <span class="w-28 truncate">
                {{ performerName(pid) }}
                <span v-if="!isCast(pid)" class="text-xs text-orange-600 dark:text-orange-400" title="On stage, not in the number">(extra)</span>
              </span>
              <input :value="assignmentOf(pid)?.size ?? ''" placeholder="Size" class="w-20 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="saveAssignment(pid, { size: ($event.target as HTMLInputElement).value || null })" />
              <input :value="assignmentOf(pid)?.notes ?? ''" placeholder="Alteration notes" class="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="saveAssignment(pid, { notes: ($event.target as HTMLInputElement).value || null })" />
              <button v-if="!isCast(pid)" class="rounded p-1 text-muted-foreground hover:text-destructive" title="Remove extra" @click="removeAssignment(pid)"><X class="h-3.5 w-3.5" /></button>
            </div>
            <select v-if="extraCandidates.length" class="rounded-md border border-dashed border-border bg-transparent px-2 py-1 text-xs text-muted-foreground focus:outline-none" :value="''" @change="addExtra">
              <option value="">+ Add on-stage extra (not in number)</option>
              <option v-for="p in extraCandidates" :key="p.id" :value="p.id">{{ p.firstName }} {{ p.lastName }}</option>
            </select>
          </div>
        </div>

        <!-- Formations tab -->
        <div v-else>
          <FormationEditor :number-id="number.id" :dancers="castPerformers" empty-hint="Cast performers into this number first (Cast tab)." />
        </div>
      </div>
    </aside>
  </div>
</template>
