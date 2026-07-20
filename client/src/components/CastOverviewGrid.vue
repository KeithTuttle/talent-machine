<script setup lang="ts">
// The kids × numbers matrix, dance-manager style: columns are numbers in
// running order (grouped under act headers when acts exist), rows are cast
// members grouped by cast group with hide/show chips, cells toggle casting
// with gender-colored dots. Sticky headers + first column; fills the viewport.
import { computed, ref, watch } from 'vue'
import { AlertTriangle } from 'lucide-vue-next'
import ColorDot from '@/components/ColorDot.vue'
import { GENDER_COLORS, GENDER_FALLBACK, tint } from '@/lib/colors'
import type { Act, CastGroup, CastMembership, MusicalNumber, NumberCast } from '@/types'

const props = defineProps<{
  numbers: MusicalNumber[]
  acts: Act[]
  cast: CastMembership[]
  groups: CastGroup[]
  numberCasts: NumberCast[]
  performerName: (id: number) => string
  performerAge: (id: number) => number | null
}>()
const emit = defineEmits<{
  toggle: [numberId: number, performerId: number]
  openMember: [member: CastMembership]
}>()

const PREFS_KEY = 'planner.gridPrefs'
const stored = (() => {
  try {
    return JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as {
      hiddenRowGroups?: (number | null)[]
    }
  } catch {
    return {}
  }
})()

const hiddenRowGroups = ref<(number | null)[]>(stored.hiddenRowGroups ?? [])
const search = ref('')

watch(hiddenRowGroups, () => {
  localStorage.setItem(PREFS_KEY, JSON.stringify({ hiddenRowGroups: hiddenRowGroups.value }))
}, { deep: true })

// --- Columns: running order (act headers when acts exist) --------------------

interface ColumnGroup {
  header: string | null
  numbers: MusicalNumber[]
}

const columnGroups = computed<ColumnGroup[]>(() => {
  const ordered = [...props.numbers].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
  if (props.acts.length === 0) return [{ header: null, numbers: ordered }]
  const out: ColumnGroup[] = [...props.acts]
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
    .map((act) => ({ header: act.name, numbers: ordered.filter((n) => n.actId === act.id) }))
  const unassigned = ordered.filter((n) => n.actId == null)
  if (unassigned.length > 0) out.push({ header: 'Unassigned', numbers: unassigned })
  return out.filter((g) => g.numbers.length > 0)
})

const allColumns = computed(() => columnGroups.value.flatMap((g) => g.numbers))
const hasActHeaders = computed(() => props.acts.length > 0)

// --- Rows: cast members grouped by cast group --------------------------------

interface RowGroup {
  key: number | null
  name: string
  color: string | null | undefined
  members: CastMembership[]
}

const rowGroups = computed<RowGroup[]>(() => {
  const sortMembers = (ms: CastMembership[]) =>
    [...ms].sort((a, b) =>
      props.performerName(a.performerId).localeCompare(props.performerName(b.performerId)),
    )
  const out: RowGroup[] = props.groups.map((g) => ({
    key: g.id,
    name: g.name,
    color: g.color,
    members: sortMembers(props.cast.filter((m) => (m.castGroupId ?? null) === g.id)),
  }))
  const ungrouped = sortMembers(props.cast.filter((m) => (m.castGroupId ?? null) === null))
  if (ungrouped.length > 0)
    out.push({ key: null, name: 'Ungrouped', color: null, members: ungrouped })
  return out.filter((g) => g.members.length > 0)
})

const visibleRowGroups = computed(() =>
  rowGroups.value.filter((g) => !hiddenRowGroups.value.includes(g.key)),
)

function toggleRowGroup(key: number | null) {
  hiddenRowGroups.value = hiddenRowGroups.value.includes(key)
    ? hiddenRowGroups.value.filter((k) => k !== key)
    : [...hiddenRowGroups.value, key]
}

const rowDimmed = (m: CastMembership) => {
  const q = search.value.trim().toLowerCase()
  return q !== '' && !props.performerName(m.performerId).toLowerCase().includes(q)
}

// --- Cells & totals ----------------------------------------------------------

const isCast = (numberId: number, performerId: number) =>
  props.numberCasts.some((c) => c.musicalNumberId === numberId && c.performerId === performerId)

const dotColor = (m: CastMembership) =>
  GENDER_COLORS[m.performer?.gender ?? ''] ?? GENDER_FALLBACK

const countByPerformer = computed(() => {
  const map = new Map<number, number>()
  for (const c of props.numberCasts) map.set(c.performerId, (map.get(c.performerId) ?? 0) + 1)
  return map
})

/** dance-manager's load heuristic: busy when ≥ max(3, ceil(avg × 1.5)). */
const busyThreshold = computed(() => {
  const counts = [...countByPerformer.value.values()]
  if (counts.length === 0) return Infinity
  const avg = counts.reduce((a, b) => a + b, 0) / counts.length
  return Math.max(3, Math.ceil(avg * 1.5))
})

const countByNumber = (numberId: number) =>
  props.numberCasts.filter((c) => c.musicalNumberId === numberId).length

/** Kids not cast in anything yet — worth a director's glance. */
const notYetCast = computed(() =>
  props.cast.filter((m) => (countByPerformer.value.get(m.performerId) ?? 0) === 0),
)
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col gap-3">
    <!-- Controls -->
    <div class="flex flex-wrap items-center gap-2">
      <input
        v-model="search"
        placeholder="Find a kid…"
        class="min-w-36 rounded-md border border-border bg-background px-2.5 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
      />
      <button
        v-for="g in rowGroups"
        :key="g.key ?? 'ungrouped'"
        class="flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs"
        :class="hiddenRowGroups.includes(g.key) ? 'border-border text-muted-foreground line-through' : 'border-transparent bg-accent text-accent-foreground'"
        :title="hiddenRowGroups.includes(g.key) ? `Show ${g.name}` : `Hide ${g.name}`"
        @click="toggleRowGroup(g.key)"
      >
        <ColorDot :color="g.color" size="sm" /> {{ g.name }}
      </button>
      <span v-if="notYetCast.length > 0" class="ml-auto flex items-center gap-1 text-xs text-destructive">
        <AlertTriangle class="h-3.5 w-3.5" />
        Not cast yet: {{ notYetCast.map((m) => performerName(m.performerId)).join(', ') }}
      </span>
    </div>

    <p v-if="cast.length === 0 || numbers.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      {{ cast.length === 0 ? 'Add performers to the cast first (Plan view).' : 'Add numbers first (Plan view).' }}
    </p>

    <!-- The grid — fills the remaining viewport height -->
    <div v-else class="min-h-0 flex-1 overflow-auto rounded-lg border border-border">
      <table class="border-separate border-spacing-0 text-sm">
        <thead>
          <!-- Act headers -->
          <tr v-if="hasActHeaders">
            <th class="sticky left-0 top-0 z-30 border-b border-r border-border bg-background" />
            <template v-for="cg in columnGroups" :key="cg.header ?? 'flat'">
              <th
                :colspan="cg.numbers.length"
                class="sticky top-0 z-20 border-b border-r border-border bg-muted px-2 py-1 text-left text-xs font-semibold"
              >
                {{ cg.header }}
              </th>
            </template>
            <th class="sticky top-0 z-20 border-b border-border bg-background" />
          </tr>
          <!-- Number titles (vertical) -->
          <tr>
            <th
              class="sticky left-0 z-30 border-b border-r border-border bg-background px-3 py-2 text-left text-xs font-semibold"
              :class="hasActHeaders ? 'top-[25px]' : 'top-0'"
            >
              Performer
            </th>
            <th
              v-for="n in allColumns"
              :key="n.id"
              class="sticky z-20 border-b border-r border-border bg-background px-1 pb-2 pt-3 align-bottom"
              :class="hasActHeaders ? 'top-[25px]' : 'top-0'"
            >
              <span class="mx-auto block max-h-36 truncate text-xs font-medium [writing-mode:vertical-rl]">
                {{ n.title }}
              </span>
            </th>
            <th
              class="sticky z-20 border-b border-border bg-background px-2 text-xs font-semibold"
              :class="hasActHeaders ? 'top-[25px]' : 'top-0'"
            >#</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="g in visibleRowGroups" :key="g.key ?? 'ungrouped'">
            <tr>
              <td
                :colspan="allColumns.length + 2"
                class="border-b border-border px-3 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                :style="{ backgroundColor: tint(g.color, 0.12) ?? 'hsl(var(--muted))' }"
              >
                <span class="flex items-center gap-1.5"><ColorDot :color="g.color" size="sm" /> {{ g.name }}</span>
              </td>
            </tr>
            <tr v-for="m in g.members" :key="m.id" :class="rowDimmed(m) ? 'opacity-30' : ''">
              <td class="sticky left-0 z-10 whitespace-nowrap border-b border-r border-border bg-background px-3 py-1.5">
                <button
                  class="hover:text-primary hover:underline"
                  :title="`Open ${performerName(m.performerId)}'s notes & conflicts`"
                  @click="emit('openMember', m)"
                >
                  {{ performerName(m.performerId) }}
                </button>
                <span v-if="performerAge(m.performerId) !== null" class="ml-1 text-xs text-muted-foreground">
                  {{ performerAge(m.performerId) }}
                </span>
              </td>
              <td
                v-for="n in allColumns"
                :key="n.id"
                class="cursor-pointer border-b border-r border-border text-center hover:bg-accent/50"
                :title="`${performerName(m.performerId)} — ${n.title}`"
                @click="emit('toggle', n.id, m.performerId)"
              >
                <span
                  v-if="isCast(n.id, m.performerId)"
                  class="mx-auto block h-2.5 w-2.5 rounded-full"
                  :style="{ backgroundColor: dotColor(m) }"
                />
              </td>
              <td class="whitespace-nowrap border-b border-border px-2 text-center text-xs tabular-nums">
                <span :class="(countByPerformer.get(m.performerId) ?? 0) === 0 ? 'font-semibold text-destructive' : ''">
                  {{ countByPerformer.get(m.performerId) ?? 0 }}
                </span>
                <AlertTriangle
                  v-if="(countByPerformer.get(m.performerId) ?? 0) >= busyThreshold"
                  class="ml-0.5 inline h-3 w-3 text-orange-500"
                  aria-label="Heavily cast"
                  title="Heavily cast (busy)"
                />
              </td>
            </tr>
          </template>
          <!-- Per-number totals -->
          <tr>
            <td class="sticky left-0 z-10 border-r border-border bg-background px-3 py-1.5 text-xs font-semibold text-muted-foreground">
              Total
            </td>
            <td v-for="n in allColumns" :key="n.id" class="border-r border-border text-center text-xs tabular-nums text-muted-foreground">
              {{ countByNumber(n.id) }}
            </td>
            <td />
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
