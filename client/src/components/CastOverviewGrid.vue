<script setup lang="ts">
// The kids × numbers matrix, dance-manager style: columns are numbers in running
// order (under act headers), rows are cast members grouped by cast group with
// hide/show chips. Gender-colored cell dots, a costume-color row, column hover
// highlight, teach-status icons, a legend, and a costume summary + quick-change
// panel below. Click a name → member drawer; click a number → number drawer.
import { computed, ref, watch } from 'vue'
import { AlertTriangle, ChevronDown, ChevronRight, FileDown, SlidersHorizontal } from 'lucide-vue-next'
import ColorDot from '@/components/ColorDot.vue'
import { GENDER_COLORS, GENDER_FALLBACK, costumeColorMap, tint } from '@/lib/colors'
import { teachIcon } from '@/lib/teach'
import type {
  Act,
  CastGroup,
  CastMembership,
  CostumeAssignment,
  Costume,
  MusicalNumber,
  NumberCast,
} from '@/types'

const props = defineProps<{
  numbers: MusicalNumber[]
  acts: Act[]
  cast: CastMembership[]
  groups: CastGroup[]
  numberCasts: NumberCast[]
  costumeCatalog: Costume[]
  costumeAssignments: CostumeAssignment[]
  performerName: (id: number) => string
  performerAge: (id: number) => number | null
}>()
const emit = defineEmits<{
  toggle: [numberId: number, performerId: number]
  openMember: [member: CastMembership]
  openNumber: [number: MusicalNumber]
  costumePdf: []
  quickChangePdf: []
  costumePlotPdf: []
}>()

const PREFS_KEY = 'planner.gridPrefs'
const stored = (() => {
  try {
    return JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as {
      hiddenRowGroups?: (number | null)[]
      filtersOpen?: boolean
    }
  } catch {
    return {}
  }
})()

const hiddenRowGroups = ref<(number | null)[]>(stored.hiddenRowGroups ?? [])
const filtersOpen = ref<boolean>(stored.filtersOpen ?? true)
const search = ref('')
const hoveredNumberId = ref<number | null>(null)

function savePrefs() {
  localStorage.setItem(
    PREFS_KEY,
    JSON.stringify({ hiddenRowGroups: hiddenRowGroups.value, filtersOpen: filtersOpen.value }),
  )
}
watch(hiddenRowGroups, savePrefs, { deep: true })
watch(filtersOpen, savePrefs)

// --- Columns: running order (act headers when acts exist) --------------------

interface ColumnGroup {
  header: string | null
  numbers: MusicalNumber[]
}

/** Numbers in running order across acts, then unassigned last. */
const orderedNumbers = computed(() => {
  const ordered = [...props.numbers].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
  if (props.acts.length === 0) return ordered
  const out: MusicalNumber[] = []
  for (const act of [...props.acts].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id))
    out.push(...ordered.filter((n) => n.actId === act.id))
  out.push(...ordered.filter((n) => n.actId == null))
  return out
})

const columnGroups = computed<ColumnGroup[]>(() => {
  const ordered = orderedNumbers.value
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

// --- Costume colors ----------------------------------------------------------

const costumeColors = computed(() => costumeColorMap(orderedNumbers.value.map((n) => n.costumeLabel)))
const costumeColorOf = (n: MusicalNumber) => {
  const key = n.costumeLabel?.trim().toLowerCase()
  return key ? costumeColors.value.get(key) ?? null : null
}
const hasCostumes = computed(() => orderedNumbers.value.some((n) => n.costumeLabel?.trim()))

// --- Rows: cast members grouped by cast group --------------------------------

interface RowGroup {
  key: number | null
  name: string
  color: string | null | undefined
  members: CastMembership[]
}

const rowGroups = computed<RowGroup[]>(() => {
  const sortMembers = (ms: CastMembership[]) =>
    [...ms].sort((a, b) => props.performerName(a.performerId).localeCompare(props.performerName(b.performerId)))
  const out: RowGroup[] = props.groups.map((g) => ({
    key: g.id,
    name: g.name,
    color: g.color,
    members: sortMembers(props.cast.filter((m) => (m.castGroupId ?? null) === g.id)),
  }))
  const ungrouped = sortMembers(props.cast.filter((m) => (m.castGroupId ?? null) === null))
  if (ungrouped.length > 0) out.push({ key: null, name: 'Ungrouped', color: null, members: ungrouped })
  return out.filter((g) => g.members.length > 0)
})

const visibleRowGroups = computed(() => rowGroups.value.filter((g) => !hiddenRowGroups.value.includes(g.key)))

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

const castSet = computed(() => {
  const s = new Set<string>()
  for (const c of props.numberCasts) s.add(`${c.musicalNumberId}:${c.performerId}`)
  return s
})
const isCast = (numberId: number, performerId: number) => castSet.value.has(`${numberId}:${performerId}`)
const dotColor = (m: CastMembership) => GENDER_COLORS[m.performer?.gender ?? ''] ?? GENDER_FALLBACK

const countByPerformer = computed(() => {
  const map = new Map<number, number>()
  for (const c of props.numberCasts) map.set(c.performerId, (map.get(c.performerId) ?? 0) + 1)
  return map
})
const busyThreshold = computed(() => {
  const counts = [...countByPerformer.value.values()]
  if (counts.length === 0) return Infinity
  const avg = counts.reduce((a, b) => a + b, 0) / counts.length
  return Math.max(3, Math.ceil(avg * 1.5))
})
const countByNumber = (numberId: number) => props.numberCasts.filter((c) => c.musicalNumberId === numberId).length
const notYetCast = computed(() => props.cast.filter((m) => (countByPerformer.value.get(m.performerId) ?? 0) === 0))

// --- Costume summary + quick-change -----------------------------------------

const costumeSummary = computed(() => {
  const groups = new Map<string, { color: string | null; numbers: MusicalNumber[] }>()
  for (const n of orderedNumbers.value) {
    const label = n.costumeLabel?.trim()
    if (!label) continue
    const key = label.toLowerCase()
    if (!groups.has(key)) groups.set(key, { color: costumeColorOf(n), numbers: [] })
    groups.get(key)!.numbers.push(n)
  }
  return [...groups.entries()].map(([, v], i) => ({
    label: v.numbers[0].costumeLabel!,
    color: v.color,
    numbers: v.numbers,
    key: i,
  }))
})

const costumeById = computed(() => {
  const m = new Map<number, Costume>()
  for (const c of props.costumeCatalog) m.set(c.id, c)
  return m
})
const assignmentByKey = computed(() => {
  const m = new Map<string, CostumeAssignment>()
  for (const a of props.costumeAssignments) m.set(`${a.musicalNumberId}:${a.performerId}`, a)
  return m
})

/** A performer's costume in a number. An assigned catalog costume compares by its
 * ID, so two numbers wearing the same costume always match — no spelling to get
 * wrong. Falls back to the number's costume label text. Empty string = unknown
 * (don't compare). This is what makes changes per-performer: a kid who stays a
 * "Bear" across two numbers isn't flagged even if the numbers' overall labels
 * differ, and only the kids who actually change are listed.
 *
 * Mirrors CostumeChanges.Identity on the server (used by the printed sheets). */
function costumeIdentity(n: MusicalNumber, performerId: number): string {
  const a = assignmentByKey.value.get(`${n.id}:${performerId}`)
  if (a?.costumeId != null && costumeById.value.has(a.costumeId)) return `c:${a.costumeId}`
  const label = n.costumeLabel?.trim().toLowerCase()
  return label ? `l:${label}` : ''
}

interface CostumeChange {
  a: MusicalNumber
  b: MusicalNumber
  performers: string[]
  /** Numbers of the running order between the two; 0 = back-to-back. */
  buffer: number
}

/**
 * Every costume change in the show, per performer — comparing each kid's
 * CONSECUTIVE APPEARANCES, not just adjacent numbers, so a change with a number
 * of breathing room in between is still caught (just ranked calmer). An act break
 * between them means an intermission, so it never counts.
 *
 * Mirrors Services/CostumeChanges.cs on the server (used by the printed sheets) —
 * keep the two in step.
 */
const costumeChanges = computed<CostumeChange[]>(() => {
  const list = orderedNumbers.value
  const position = new Map(list.map((n, i) => [n.id, i]))
  // Group by the pair of numbers so kids changing at the same moment share a row.
  const byMoment = new Map<string, CostumeChange>()

  for (const m of props.cast) {
    const pid = m.performerId
    const appearances = list.filter((n) => isCast(n.id, pid))
    for (let i = 0; i < appearances.length - 1; i++) {
      const a = appearances[i], b = appearances[i + 1]
      if (a.actId != null && b.actId != null && a.actId !== b.actId) continue
      const ia = costumeIdentity(a, pid)
      const ib = costumeIdentity(b, pid)
      if (ia === '' || ib === '' || ia === ib) continue

      const key = `${a.id}:${b.id}`
      const existing = byMoment.get(key)
      const name = props.performerName(pid)
      if (existing) existing.performers.push(name)
      else
        byMoment.set(key, {
          a, b, performers: [name],
          buffer: (position.get(b.id) ?? 0) - (position.get(a.id) ?? 0) - 1,
        })
    }
  }

  return [...byMoment.values()]
    .map((c) => ({ ...c, performers: c.performers.sort() }))
    .sort((x, y) => x.buffer - y.buffer || (position.get(x.a.id) ?? 0) - (position.get(y.a.id) ?? 0))
})

/** Back-to-back changes — the ones that actually need a dresser standing by. */
const quickChangeCount = computed(() => costumeChanges.value.filter((c) => c.buffer === 0).length)

const bufferLabel = (buffer: number) =>
  buffer === 0 ? 'back-to-back' : buffer === 1 ? '1 number' : `${buffer} numbers`
const bufferClass = (buffer: number) =>
  buffer === 0
    ? 'bg-destructive/15 text-destructive'
    : buffer === 1
      ? 'bg-orange-500/15 text-orange-600 dark:text-orange-400'
      : 'bg-muted text-muted-foreground'
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col gap-3">
    <!-- Filter toggle (always visible so the grid can take the full height) -->
    <div class="flex items-center gap-2">
      <button
        class="flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs font-medium hover:bg-accent"
        :aria-expanded="filtersOpen"
        :title="filtersOpen ? 'Hide filters' : 'Show filters'"
        @click="filtersOpen = !filtersOpen"
      >
        <SlidersHorizontal class="h-3.5 w-3.5" /> Filters
        <component :is="filtersOpen ? ChevronDown : ChevronRight" class="h-3.5 w-3.5" />
      </button>
      <div class="ml-auto flex flex-wrap items-center gap-1.5">
        <button
          class="flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1 text-xs font-medium hover:bg-accent"
          title="Every number in running order with its costumes and who wears them"
          @click="emit('costumePdf')"
        >
          <FileDown class="h-3.5 w-3.5" /> Costume sheet
        </button>
        <button
          class="flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1 text-xs font-medium hover:bg-accent"
          title="The dressers' wing sheet — every change, most urgent first"
          @click="emit('quickChangePdf')"
        >
          <FileDown class="h-3.5 w-3.5" /> Quick changes
        </button>
        <button
          class="flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1 text-xs font-medium hover:bg-accent"
          title="One block per kid — what they wear, in order"
          @click="emit('costumePlotPdf')"
        >
          <FileDown class="h-3.5 w-3.5" /> Per-kid plot
        </button>
      </div>
    </div>

    <template v-if="filtersOpen">
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
      </div>

      <!-- Legend -->
      <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
        <span class="flex items-center gap-1"><span class="inline-block h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_COLORS.Male }" /> boy</span>
        <span class="flex items-center gap-1"><span class="inline-block h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_COLORS.Female }" /> girl</span>
        <span class="flex items-center gap-1"><span class="inline-block h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_FALLBACK }" /> other</span>
        <span class="flex items-center gap-1"><AlertTriangle class="h-3.5 w-3.5 text-orange-500" /> heavily cast</span>
        <span v-if="hasCostumes" class="flex items-center gap-1"><span class="inline-block h-2.5 w-4 rounded-sm bg-muted-foreground/40" /> costume (same color = same costume)</span>
        <span class="ml-auto italic">Click a cell to cast · a name for details · a number to open it</span>
      </div>
    </template>

    <p v-if="cast.length === 0 || numbers.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      {{ cast.length === 0 ? 'Add performers to the cast first (Plan view).' : 'Add numbers first (Plan view).' }}
    </p>

    <!-- One scroll region: the tall grid dominates, summary panels flow below it.
         Sticky headers/first column pin to this container while scrolling. -->
    <div v-else class="min-h-0 flex-1 overflow-auto">
      <div class="w-max rounded-lg border border-border">
        <table class="border-separate border-spacing-0 text-sm" @mouseleave="hoveredNumberId = null">
          <thead>
            <!-- Act headers -->
            <tr v-if="hasActHeaders">
              <th class="sticky left-0 top-0 z-30 border-b border-r border-border bg-background" />
              <template v-for="cg in columnGroups" :key="cg.header ?? 'flat'">
                <th :colspan="cg.numbers.length" class="sticky top-0 z-20 border-b border-r border-border bg-muted px-2 py-1 text-left text-xs font-semibold">
                  {{ cg.header }}
                </th>
              </template>
              <th class="sticky top-0 z-20 border-b border-border bg-background" />
            </tr>
            <!-- Number titles (vertical, clickable) -->
            <tr>
              <th class="sticky left-0 z-30 border-b border-r border-border bg-background px-3 py-2 text-left text-xs font-semibold" :class="hasActHeaders ? 'top-[25px]' : 'top-0'">
                Performer
              </th>
              <th
                v-for="n in allColumns"
                :key="n.id"
                class="sticky z-20 border-b border-r border-border bg-background px-1 pb-2 pt-3 align-bottom"
                :class="[hasActHeaders ? 'top-[25px]' : 'top-0', hoveredNumberId === n.id ? 'shadow-[inset_0_0_0_9999px_hsl(var(--primary)/0.1)]' : '']"
                @mouseenter="hoveredNumberId = n.id"
              >
                <button class="mx-auto flex flex-col items-center gap-1" :title="`Open ${n.title}`" @click="emit('openNumber', n)">
                  <component :is="teachIcon(n.teachStatus)!.icon" v-if="teachIcon(n.teachStatus)" class="h-3 w-3" :class="teachIcon(n.teachStatus)!.class" />
                  <span class="max-h-32 truncate text-xs font-medium [writing-mode:vertical-rl]">{{ n.title }}</span>
                </button>
              </th>
              <th class="sticky z-20 border-b border-border bg-background px-2 text-xs font-semibold" :class="hasActHeaders ? 'top-[25px]' : 'top-0'">#</th>
            </tr>
            <!-- Costume color row — scrolls with the body (sticky-left only, like the
                 other row labels); pinning just its label while the swatches scrolled
                 away read as a detached "Costume" tag. -->
            <tr v-if="hasCostumes">
              <th class="sticky left-0 z-20 border-b border-r border-border bg-background px-3 py-1 text-right text-[10px] uppercase text-muted-foreground">
                Costume
              </th>
              <td
                v-for="n in allColumns"
                :key="n.id"
                class="border-b border-r border-border px-1 py-1"
                :class="hoveredNumberId === n.id ? 'bg-primary/10' : ''"
                :title="n.costumeLabel ?? 'No costume set'"
                @mouseenter="hoveredNumberId = n.id"
              >
                <span
                  class="mx-auto block h-2.5 w-5 rounded-sm"
                  :class="costumeColorOf(n) ? '' : 'border border-dashed border-border'"
                  :style="costumeColorOf(n) ? { backgroundColor: costumeColorOf(n)! } : {}"
                />
              </td>
              <td class="border-b border-border" />
            </tr>
          </thead>
          <tbody>
            <template v-for="g in visibleRowGroups" :key="g.key ?? 'ungrouped'">
              <tr>
                <td :colspan="allColumns.length + 2" class="border-b border-border px-3 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground" :style="{ backgroundColor: tint(g.color, 0.12) ?? 'hsl(var(--muted))' }">
                  <span class="flex items-center gap-1.5"><ColorDot :color="g.color" size="sm" /> {{ g.name }}</span>
                </td>
              </tr>
              <tr v-for="m in g.members" :key="m.id" :class="rowDimmed(m) ? 'opacity-30' : ''">
                <td class="sticky left-0 z-10 whitespace-nowrap border-b border-r border-border bg-background px-3 py-1.5">
                  <button class="hover:text-primary hover:underline" :title="`Open ${performerName(m.performerId)}'s notes & conflicts`" @click="emit('openMember', m)">
                    {{ performerName(m.performerId) }}
                  </button>
                  <span v-if="performerAge(m.performerId) !== null" class="ml-1 text-xs text-muted-foreground">{{ performerAge(m.performerId) }}</span>
                </td>
                <td
                  v-for="n in allColumns"
                  :key="n.id"
                  class="cursor-pointer border-b border-r border-border text-center hover:bg-accent/50"
                  :class="hoveredNumberId === n.id ? 'bg-primary/5' : ''"
                  :title="`${performerName(m.performerId)} — ${n.title}`"
                  @mouseenter="hoveredNumberId = n.id"
                  @click="emit('toggle', n.id, m.performerId)"
                >
                  <span v-if="isCast(n.id, m.performerId)" class="mx-auto block h-2.5 w-2.5 rounded-full" :style="{ backgroundColor: dotColor(m) }" />
                </td>
                <td class="whitespace-nowrap border-b border-border px-2 text-center text-xs tabular-nums">
                  <span :class="(countByPerformer.get(m.performerId) ?? 0) === 0 ? 'font-semibold text-destructive' : ''">
                    {{ countByPerformer.get(m.performerId) ?? 0 }}
                  </span>
                  <AlertTriangle v-if="(countByPerformer.get(m.performerId) ?? 0) >= busyThreshold" class="ml-0.5 inline h-3 w-3 text-orange-500" title="Heavily cast (busy)" />
                </td>
              </tr>
            </template>
            <!-- Per-number totals -->
            <tr>
              <td class="sticky left-0 z-10 border-r border-border bg-background px-3 py-1.5 text-xs font-semibold text-muted-foreground">Total</td>
              <td v-for="n in allColumns" :key="n.id" class="border-r border-border text-center text-xs tabular-nums text-muted-foreground" :class="hoveredNumberId === n.id ? 'bg-primary/5' : ''">
                {{ countByNumber(n.id) }}
              </td>
              <td />
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Summary panels — flow below the grid, pinned to the left as you scroll right -->
      <div class="sticky left-0 mt-3 grid max-w-4xl gap-3 pb-1 text-sm md:grid-cols-2">
        <div v-if="notYetCast.length > 0" class="rounded-lg border border-border p-3">
          <h3 class="text-xs font-semibold uppercase tracking-wide text-destructive">Not yet cast ({{ notYetCast.length }})</h3>
          <p class="mt-1 text-xs text-muted-foreground">{{ notYetCast.map((m) => performerName(m.performerId)).join(', ') }}</p>
        </div>
        <div v-if="costumeChanges.length > 0" class="rounded-lg border border-border p-3">
          <h3 class="flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-orange-600 dark:text-orange-400">
            <AlertTriangle class="h-3.5 w-3.5" /> Costume changes ({{ costumeChanges.length }})
            <span v-if="quickChangeCount > 0" class="font-normal normal-case text-muted-foreground">
              — {{ quickChangeCount }} back-to-back
            </span>
          </h3>
          <ul class="mt-1.5 space-y-1 text-xs text-muted-foreground">
            <li v-for="(cc, i) in costumeChanges" :key="i" class="flex flex-wrap items-baseline gap-x-1.5">
              <span class="rounded-full px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide" :class="bufferClass(cc.buffer)">
                {{ bufferLabel(cc.buffer) }}
              </span>
              <span class="font-medium text-foreground">{{ cc.a.title }} → {{ cc.b.title }}</span>
              <span>{{ cc.performers.join(', ') }}</span>
            </li>
          </ul>
        </div>
        <div v-if="costumeSummary.length > 0" class="rounded-lg border border-border p-3 md:col-span-2">
          <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Costumes</h3>
          <div class="mt-1.5 flex flex-wrap gap-2">
            <span v-for="c in costumeSummary" :key="c.key" class="flex items-center gap-1.5 rounded-full border border-border px-2 py-0.5 text-xs">
              <span class="inline-block h-2.5 w-2.5 rounded-full" :style="{ backgroundColor: c.color ?? '#94a3b8' }" />
              {{ c.label }}
              <span class="text-muted-foreground">· {{ c.numbers.length }}</span>
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
