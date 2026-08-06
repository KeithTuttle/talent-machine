<script setup lang="ts">
// Per-number editor opened from the overview grid (and the planner): title,
// teach status, choreographer, cast, costumes (one or several "looks", with who
// wears each) + per-kid sizes, and formations. Cast toggles are emitted so the
// parent keeps its shared state in sync; costume data is loaded/saved here.
import { computed, ref, watch } from 'vue'
import { AlertTriangle, Plus, Trash2, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { useStaffStore } from '@/stores/staff'
import StaffPicker from '@/components/StaffPicker.vue'
import ColorDot from '@/components/ColorDot.vue'
import FormationEditor from '@/components/FormationEditor.vue'
import { teachIcon, TEACH_STATUSES, TEACH_LABELS } from '@/lib/teach'
import type {
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
const roles = ref<Role[]>([])
const numberChars = ref<NumberCharacter[]>([])
const tab = ref<'cast' | 'costumes' | 'formations'>('cast')

watch(
  () => props.number?.id,
  async (id) => {
    pieces.value = []
    assignments.value = []
    numberChars.value = []
    tab.value = 'cast'
    staff.fetch()
    if (id != null && props.number) {
      ;[pieces.value, assignments.value, roles.value, numberChars.value] = await Promise.all([
        api.get<CostumePiece[]>(`/costumepieces?numberId=${id}`).then((r) => r.data).catch(() => []),
        api.get<CostumeAssignment[]>(`/costumeassignments?numberId=${id}`).then((r) => r.data).catch(() => []),
        api.get<Role[]>(`/roles?productionId=${props.number.productionId}`).then((r) => r.data).catch(() => []),
        api.get<NumberCharacter[]>(`/numbercharacters?numberId=${id}`).then((r) => r.data).catch(() => []),
      ])
    }
  },
  { immediate: true },
)

// --- Featured characters (additive tag over the performer cast) --------------
const hasChar = (roleId: number) => numberChars.value.some((nc) => nc.roleId === roleId)
async function toggleChar(roleId: number) {
  if (!props.number) return
  const id = props.number.id
  if (hasChar(roleId)) {
    numberChars.value = numberChars.value.filter((nc) => nc.roleId !== roleId)
    await api.delete(`/numbercharacters?numberId=${id}&roleId=${roleId}`).catch(() => {})
  } else {
    numberChars.value.push({ musicalNumberId: id, roleId })
    await api.post('/numbercharacters', { musicalNumberId: id, roleId }).catch(() => {})
  }
}

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
  const prev = props.number.choreographerStaffId ?? null
  props.number.choreographerStaffId = staffId
  try {
    await api.put(`/numbers/${props.number.id}`, { ...props.number, choreographer: undefined })
  } catch {
    props.number.choreographerStaffId = prev
    toast.error("Couldn't save the choreographer — please try again.")
  }
}
function useCostumeLabel(label: string) {
  if (!props.number) return
  props.number.costumeLabel = label
  saveNumber()
}

// --- Costumes ----------------------------------------------------------------

/** Only when a number has MORE THAN ONE costume is there a choice to make — the
 * "who's in which costume" UI stays hidden until then, so the common
 * everyone-wears-the-same-thing number stays simple. */
const hasLooks = computed(() => pieces.value.length > 1)

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
    a = { id: 0, musicalNumberId: props.number.id, performerId, costumePieceId: null, size: null, notes: null }
    assignments.value.push(a)
  }
  Object.assign(a, patch)
  const { data } = await api.post<CostumeAssignment>('/costumeassignments', {
    id: a.id,
    musicalNumberId: props.number.id,
    performerId,
    costumePieceId: a.costumePieceId ?? null,
    size: a.size ?? null,
    notes: a.notes ?? null,
  })
  a.id = data.id
}

// --- Who's in which costume --------------------------------------------------
// Assignment reads costume-first ("who's wearing the bear?") rather than
// kid-first, because that's how a director thinks about a mixed number.

const wearersOfPiece = (pieceId: number) =>
  wearers.value.filter((pid) => assignmentOf(pid)?.costumePieceId === pieceId)

/** Kids in the number who aren't in any costume yet (only meaningful with 2+). */
const unassignedWearers = computed(() =>
  wearers.value.filter((pid) => assignmentOf(pid)?.costumePieceId == null),
)

const performerGender = (id: number) =>
  (props.cast.find((m) => m.performerId === id)?.performer ??
    props.performers.find((p) => p.id === id))?.gender ?? null

/** Put everyone in this number of one gender into a costume — the usual split. */
async function assignAllByGender(pieceId: number, gender: Gender) {
  for (const pid of wearers.value) {
    if (performerGender(pid) === gender && assignmentOf(pid)?.costumePieceId !== pieceId)
      await saveAssignment(pid, { costumePieceId: pieceId })
  }
}

async function addWearer(pieceId: number, e: Event) {
  const select = e.target as HTMLSelectElement
  const pid = Number(select.value)
  select.value = ''
  if (pid) await saveAssignment(pid, { costumePieceId: pieceId })
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

          <!-- Featured characters -->
          <div v-if="roles.length" class="space-y-1.5">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Featured characters</span>
            <div class="flex flex-wrap gap-1">
              <button
                v-for="r in roles"
                :key="r.id"
                class="rounded-full border px-2 py-0.5 text-xs transition-colors"
                :class="hasChar(r.id) ? 'border-primary bg-accent text-primary' : 'border-border text-muted-foreground hover:bg-accent'"
                @click="toggleChar(r.id)"
              >
                {{ r.name }}
              </button>
            </div>
          </div>

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
            <p class="text-[11px] leading-snug text-muted-foreground">
              Numbers sharing this name count as the same outfit — it drives the grid colors and
              the quick-change warnings.
            </p>
            <div v-if="costumeLabels.length" class="flex flex-wrap gap-1 pt-1">
              <span class="text-xs text-muted-foreground">Reuse:</span>
              <button v-for="c in costumeLabels" :key="c" class="rounded-full border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="useCostumeLabel(c)">{{ c }}</button>
            </div>
          </label>

          <!-- What they wear -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">What they wear</span>
              <button class="flex items-center gap-1 rounded-md border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="addPiece">
                <Plus class="h-3.5 w-3.5" /> {{ pieces.length === 0 ? 'Add a costume' : 'Add another costume' }}
              </button>
            </div>
            <p v-if="pieces.length === 0" class="rounded-md border border-dashed border-border p-3 text-center text-xs leading-relaxed text-muted-foreground">
              Describe what this number wears. Add a second costume only if different kids wear
              different things (e.g. some bears, some soldiers).
            </p>
            <div v-for="p in pieces" :key="p.id" class="space-y-1.5 rounded-md border border-border p-2.5">
              <div class="flex items-center gap-2">
                <input v-model="p.label" placeholder="Costume name (e.g. Bear)" class="w-36 shrink-0 rounded-md border border-border bg-background px-2 py-1 text-xs font-medium focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.description" placeholder="Description" class="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none" @change="savePiece(p)" />
                <button class="rounded p-1 text-muted-foreground hover:text-destructive" title="Delete this costume" @click="deletePiece(p)"><Trash2 class="h-3.5 w-3.5" /></button>
              </div>
              <div class="grid grid-cols-2 gap-1.5">
                <input v-model="p.accessories" placeholder="Accessories" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.shoes" placeholder="Shoes" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.photoUrl" placeholder="Photo URL" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
                <input v-model="p.vendorUrl" placeholder="Vendor URL" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="savePiece(p)" />
              </div>

              <!-- Who's in this costume — only once there's more than one to choose from -->
              <div v-if="hasLooks" class="space-y-1.5 border-t border-border pt-1.5">
                <div class="flex flex-wrap items-center gap-1">
                  <span class="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Wearing this</span>
                  <span v-if="wearersOfPiece(p.id).length === 0" class="text-xs italic text-muted-foreground">nobody yet</span>
                  <span v-for="pid in wearersOfPiece(p.id)" :key="pid" class="flex items-center gap-1 rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground">
                    {{ performerName(pid) }}
                    <button class="hover:text-destructive" :aria-label="`Take ${performerName(pid)} out of this costume`" @click="saveAssignment(pid, { costumePieceId: null })">
                      <X class="h-3 w-3" />
                    </button>
                  </span>
                </div>
                <div class="flex flex-wrap items-center gap-1.5">
                  <select :value="''" class="rounded-md border border-dashed border-border bg-transparent px-2 py-1 text-xs text-muted-foreground focus:outline-none" @change="addWearer(p.id, $event)">
                    <option value="">+ add kids…</option>
                    <option v-for="pid in wearers.filter((w) => assignmentOf(w)?.costumePieceId !== p.id)" :key="pid" :value="pid">
                      {{ performerName(pid) }}
                    </option>
                  </select>
                  <button class="rounded-full border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="assignAllByGender(p.id, 'Male')">all boys</button>
                  <button class="rounded-full border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="assignAllByGender(p.id, 'Female')">all girls</button>
                </div>
              </div>
            </div>
            <p v-if="hasLooks && unassignedWearers.length > 0" class="flex items-start gap-1 text-xs text-orange-600 dark:text-orange-400">
              <AlertTriangle class="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <span>Not in a costume yet: {{ unassignedWearers.map(performerName).join(', ') }}</span>
            </p>
          </div>

          <!-- Per-kid sizes -->
          <div class="space-y-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Sizes &amp; notes</span>
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
