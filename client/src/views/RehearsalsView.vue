<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle,
  CalendarClock,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  FileDown,
  Mail,
  Plus,
  Sparkles,
  Trash2,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { parseDate, toIsoDate } from '@/lib/conflicts'
import {
  REHEARSAL_TYPES,
  REHEARSAL_TYPE_COLORS,
  addDays,
  conflictedAttendees,
  formatTime,
  resolveAttendees,
  startOfWeek,
  weekLabel,
  weekRange,
} from '@/lib/rehearsals'
import { tint } from '@/lib/colors'
import RehearsalEmailDialog from '@/components/RehearsalEmailDialog.vue'
import RehearsalSuggestDialog from '@/components/RehearsalSuggestDialog.vue'
import { useScopeStore } from '@/stores/scope'
import type {
  CastMembership,
  Conflict,
  Guardian,
  MusicalNumber,
  NumberCast,
  PerformerGuardian,
  Rehearsal,
  RehearsalAttendee,
  RehearsalType,
} from '@/types'

const scope = useScopeStore()

const slots = ref<Rehearsal[]>([])
const overrides = ref<RehearsalAttendee[]>([])
const numbers = ref<MusicalNumber[]>([])
const cast = ref<CastMembership[]>([])
const numberCasts = ref<NumberCast[]>([])
const conflicts = ref<Conflict[]>([])
const guardians = ref<Guardian[]>([])
const guardianLinks = ref<PerformerGuardian[]>([])

const lens = ref<'list' | 'calendar'>('list')
const search = ref('')
const typeFilter = ref<'' | RehearsalType>('')
const emailOpen = ref(false)
const suggestOpen = ref(false)
const aiHidden = ref(false) // only when the server says configured:false
const expandedSlotId = ref<number | null>(null)

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
    slots.value = []
    overrides.value = []
    numbers.value = []
    cast.value = []
    numberCasts.value = []
    conflicts.value = []
    return
  }
  ;[
    slots.value,
    overrides.value,
    numbers.value,
    cast.value,
    numberCasts.value,
    conflicts.value,
    guardians.value,
    guardianLinks.value,
  ] = await Promise.all([
    safeGet<Rehearsal>(`/rehearsals?productionId=${pid}`),
    safeGet<RehearsalAttendee>(`/rehearsalattendees?productionId=${pid}`),
    safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
    safeGet<CastMembership>(`/castmemberships?productionId=${pid}`),
    safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
    safeGet<Conflict>(`/conflicts?productionId=${pid}`),
    safeGet<Guardian>('/guardians'),
    safeGet<PerformerGuardian>('/performerguardians'),
  ])
}
onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Week navigation & filtering --------------------------------------------

const weekStart = ref(startOfWeek(new Date()))
const range = computed(() => weekRange(weekStart.value))
const label = computed(() => weekLabel(weekStart.value))
const isCurrentWeek = computed(() => toIsoDate(weekStart.value) === toIsoDate(startOfWeek(new Date())))

const performerName = (id: number) => {
  const p = cast.value.find((m) => m.performerId === id)?.performer
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}
const numberTitle = (id?: number | null) => numbers.value.find((n) => n.id === id)?.title ?? 'General'

function slotPassesFilter(s: Rehearsal): boolean {
  if (typeFilter.value && s.type !== typeFilter.value) return false
  const q = search.value.trim().toLowerCase()
  if (!q) return true
  return numberTitle(s.musicalNumberId).toLowerCase().includes(q) || (s.notes ?? '').toLowerCase().includes(q)
}

const weekSlots = computed(() =>
  slots.value
    .filter((s) => s.date >= range.value.from && s.date <= range.value.to && slotPassesFilter(s))
    .sort((a, b) => a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime)),
)

const weekDays = computed(() => {
  const byDate = new Map<string, Rehearsal[]>()
  for (const s of weekSlots.value) {
    if (!byDate.has(s.date)) byDate.set(s.date, [])
    byDate.get(s.date)!.push(s)
  }
  return [...byDate.entries()].map(([date, daySlots]) => ({ date, slots: daySlots }))
})

const attendeesOf = (s: Rehearsal) => resolveAttendees(s, numberCasts.value, overrides.value)
const warningsOf = (s: Rehearsal) =>
  conflictedAttendees(attendeesOf(s), s.date, conflicts.value).map(performerName)

const scheduledThisWeek = computed(() => {
  const ids = new Set<number>()
  for (const s of weekSlots.value) for (const id of attendeesOf(s)) ids.add(id)
  return [...ids]
})

const dayLabel = (date: string) =>
  parseDate(date)?.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' }) ?? date

// --- Add slot ----------------------------------------------------------------

const addOpen = ref(false)
const formDate = ref('')
const formStart = ref('10:00')
const formEnd = ref('11:00')
const formType = ref<RehearsalType>('Dance')
const formNumberId = ref<number | null>(null)
const formNotes = ref('')

const addWarnings = computed(() => {
  if (!formDate.value) return []
  const attendees =
    formNumberId.value != null
      ? numberCasts.value.filter((c) => c.musicalNumberId === formNumberId.value).map((c) => c.performerId)
      : []
  return conflictedAttendees(attendees, formDate.value, conflicts.value).map(performerName)
})

function openAdd() {
  addOpen.value = !addOpen.value
  // Default to Saturday of the viewed week — the typical rehearsal day.
  formDate.value ||= toIsoDate(addDays(weekStart.value, 5))
}

async function addSlot() {
  const pid = scope.selectedProductionId
  if (pid === null || !formDate.value) return
  const { data } = await api.post<Rehearsal>('/rehearsals', {
    id: 0,
    productionId: pid,
    date: formDate.value,
    startTime: formStart.value,
    endTime: formEnd.value,
    type: formType.value,
    musicalNumberId: formNumberId.value,
    notes: formNotes.value.trim() || null,
  })
  slots.value.push(data)
  formNotes.value = ''
  addOpen.value = false
}

async function saveSlot(s: Rehearsal) {
  await api.put(`/rehearsals/${s.id}`, s)
  toast.success('Saved')
}

async function deleteSlot(s: Rehearsal) {
  const ok = await confirm({
    title: `Delete the ${formatTime(s.startTime)} ${numberTitle(s.musicalNumberId)} slot?`,
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/rehearsals/${s.id}`)
  slots.value = slots.value.filter((x) => x.id !== s.id)
}

// --- Attendee overrides ------------------------------------------------------

async function setOverride(s: Rehearsal, performerId: number, isExcluded: boolean) {
  const existing = overrides.value.find(
    (o) => o.rehearsalId === s.id && o.performerId === performerId,
  )
  if (existing) existing.isExcluded = isExcluded
  else overrides.value.push({ rehearsalId: s.id, performerId, isExcluded })
  await api.post('/rehearsalattendees', { rehearsalId: s.id, performerId, isExcluded }).catch(() => {})
}

async function clearOverride(s: Rehearsal, performerId: number) {
  overrides.value = overrides.value.filter(
    (o) => !(o.rehearsalId === s.id && o.performerId === performerId),
  )
  await api
    .delete(`/rehearsalattendees?rehearsalId=${s.id}&performerId=${performerId}`)
    .catch(() => {})
}

const overrideOf = (s: Rehearsal, performerId: number) =>
  overrides.value.find((o) => o.rehearsalId === s.id && o.performerId === performerId)

/** Cast members not currently attending the slot (candidates to add). */
const addableTo = (s: Rehearsal) => {
  const attending = attendeesOf(s)
  return cast.value.filter((m) => !attending.has(m.performerId))
}

// --- PDF / Email / AI --------------------------------------------------------

async function downloadPdf() {
  try {
    const { data } = await api.get(
      `/rehearsals/pdf?productionId=${scope.selectedProductionId}&from=${range.value.from}&to=${range.value.to}`,
      { responseType: 'blob' },
    )
    const url = URL.createObjectURL(data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `rehearsals-${range.value.from}.pdf`
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    toast.error("Couldn't generate the PDF — is the server running?")
  }
}

function onSuggestSaved(created: Rehearsal[]) {
  slots.value.push(...created)
  toast.success(`Added ${created.length} slot${created.length === 1 ? '' : 's'}`)
}

// --- Calendar lens -----------------------------------------------------------

const today = new Date()
const calYear = ref(today.getFullYear())
const calMonth = ref(today.getMonth())
const selectedDay = ref<string | null>(null)

const monthLabel = computed(() =>
  new Date(calYear.value, calMonth.value, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
)
function shiftMonth(delta: number) {
  const d = new Date(calYear.value, calMonth.value + delta, 1)
  calYear.value = d.getFullYear()
  calMonth.value = d.getMonth()
}
const calendarWeeks = computed(() => {
  const first = new Date(calYear.value, calMonth.value, 1)
  const start = startOfWeek(first)
  const weeks: { date: Date; inMonth: boolean }[][] = []
  const cursor = new Date(start)
  do {
    const week: { date: Date; inMonth: boolean }[] = []
    for (let i = 0; i < 7; i++) {
      week.push({ date: new Date(cursor), inMonth: cursor.getMonth() === calMonth.value })
      cursor.setDate(cursor.getDate() + 1)
    }
    weeks.push(week)
  } while (cursor.getMonth() === calMonth.value)
  return weeks
})
const slotsOnDate = (iso: string) =>
  slots.value.filter((s) => s.date === iso && slotPassesFilter(s))
    .sort((a, b) => a.startTime.localeCompare(b.startTime))
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <CalendarClock class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production, then build its rehearsal schedule here.
      </p>
    </div>

    <div v-else class="mx-auto max-w-5xl space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">Rehearsals</h1>
          <p class="text-sm text-muted-foreground">{{ scope.selectedProduction?.title }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            class="flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="downloadPdf"
          >
            <FileDown class="h-4 w-4" /> PDF
          </button>
          <button
            class="flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="emailOpen = true"
          >
            <Mail class="h-4 w-4" /> Email
          </button>
          <button
            v-if="!aiHidden"
            class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90"
            @click="suggestOpen = true"
          >
            <Sparkles class="h-4 w-4" /> AI suggest
          </button>
        </div>
      </div>

      <!-- Controls -->
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex items-center gap-1 rounded-md border border-border">
          <button class="p-1.5 text-muted-foreground hover:bg-accent" aria-label="Previous week" @click="weekStart = addDays(weekStart, -7)">
            <ChevronLeft class="h-4 w-4" />
          </button>
          <button
            class="px-2 py-1 text-sm font-medium"
            :class="isCurrentWeek ? 'text-primary' : 'text-muted-foreground hover:text-foreground'"
            @click="weekStart = startOfWeek(new Date())"
          >
            {{ label }}
          </button>
          <button class="p-1.5 text-muted-foreground hover:bg-accent" aria-label="Next week" @click="weekStart = addDays(weekStart, 7)">
            <ChevronRight class="h-4 w-4" />
          </button>
        </div>
        <div class="flex rounded-md border border-border text-sm">
          <button
            v-for="l in ([['list', 'List'], ['calendar', 'Calendar']] as const)"
            :key="l[0]"
            class="px-3 py-1.5 font-medium"
            :class="lens === l[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="lens = l[0]"
          >
            {{ l[1] }}
          </button>
        </div>
        <input
          v-model="search"
          placeholder="Search numbers or notes…"
          class="min-w-40 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
        <select
          v-model="typeFilter"
          class="rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none"
        >
          <option value="">All types</option>
          <option v-for="t in REHEARSAL_TYPES" :key="t" :value="t">{{ t }}</option>
        </select>
        <button
          class="ml-auto flex items-center gap-1 rounded-md border border-border px-2.5 py-1.5 text-sm font-medium hover:bg-accent"
          @click="openAdd"
        >
          <Plus class="h-4 w-4" /> Add slot
        </button>
      </div>

      <!-- Add form -->
      <form
        v-if="addOpen"
        class="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-muted/30 p-3"
        @submit.prevent="addSlot"
      >
        <input v-model="formDate" type="date" required class="rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none" />
        <input v-model="formStart" type="time" required class="rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none" />
        <span class="text-xs text-muted-foreground">–</span>
        <input v-model="formEnd" type="time" required class="rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none" />
        <select v-model="formType" class="rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none">
          <option v-for="t in REHEARSAL_TYPES" :key="t" :value="t">{{ t }}</option>
        </select>
        <select v-model="formNumberId" class="min-w-36 flex-1 rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none">
          <option :value="null">General (pick kids manually)</option>
          <option v-for="n in numbers" :key="n.id" :value="n.id">{{ n.title }}</option>
        </select>
        <input v-model="formNotes" placeholder="Notes" class="min-w-32 flex-1 rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none" />
        <button type="submit" class="rounded-md bg-primary px-3 py-1 text-sm font-medium text-primary-foreground hover:opacity-90">Add</button>
        <p v-if="addWarnings.length > 0" class="flex w-full items-center gap-1 text-xs text-orange-600 dark:text-orange-400">
          <AlertTriangle class="h-3.5 w-3.5" /> Conflict that day: {{ addWarnings.join(', ') }}
        </p>
      </form>

      <!-- ============ List lens ============ -->
      <div v-if="lens === 'list'" class="space-y-4">
        <p v-if="weekDays.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          No rehearsals this week{{ search || typeFilter ? ' (matching your filters)' : '' }} — add a slot or let the AI draft a schedule.
        </p>
        <div v-for="day in weekDays" :key="day.date" class="rounded-lg border border-border">
          <h3 class="border-b border-border px-4 py-2.5 text-sm font-semibold">{{ dayLabel(day.date) }}</h3>
          <div v-for="s in day.slots" :key="s.id" class="border-b border-border px-4 py-2.5 text-sm last:border-b-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="font-medium tabular-nums">{{ formatTime(s.startTime) }}–{{ formatTime(s.endTime) }}</span>
              <span
                class="rounded px-1.5 py-0.5 text-xs font-medium"
                :style="{ backgroundColor: tint(REHEARSAL_TYPE_COLORS[s.type], 0.22), color: REHEARSAL_TYPE_COLORS[s.type] }"
              >
                {{ s.type }}
              </span>
              <span class="flex-1 truncate font-medium">{{ numberTitle(s.musicalNumberId) }}</span>
              <span v-if="warningsOf(s).length > 0" class="flex items-center gap-1 text-xs font-medium text-orange-600 dark:text-orange-400">
                <AlertTriangle class="h-3.5 w-3.5" /> {{ warningsOf(s).length }}
              </span>
              <button
                class="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                @click="expandedSlotId = expandedSlotId === s.id ? null : s.id"
              >
                {{ attendeesOf(s).size }} kids
                <ChevronUp v-if="expandedSlotId === s.id" class="h-3.5 w-3.5" />
                <ChevronDown v-else class="h-3.5 w-3.5" />
              </button>
              <button class="rounded p-1 text-muted-foreground hover:text-destructive" aria-label="Delete slot" @click="deleteSlot(s)">
                <Trash2 class="h-3.5 w-3.5" />
              </button>
            </div>
            <p v-if="s.notes" class="mt-1 text-xs italic text-muted-foreground">{{ s.notes }}</p>
            <p v-if="warningsOf(s).length > 0" class="mt-1 text-xs text-orange-600 dark:text-orange-400">
              ⚠ Conflict this day: {{ warningsOf(s).join(', ') }}
            </p>

            <!-- Expanded: edit + attendees -->
            <div v-if="expandedSlotId === s.id" class="mt-2 space-y-2 rounded-md bg-muted/30 p-2.5">
              <div class="flex flex-wrap items-center gap-2">
                <input v-model="s.date" type="date" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="saveSlot(s)" />
                <input v-model="s.startTime" type="time" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="saveSlot(s)" />
                <span class="text-xs text-muted-foreground">–</span>
                <input v-model="s.endTime" type="time" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="saveSlot(s)" />
                <select v-model="s.type" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="saveSlot(s)">
                  <option v-for="t in REHEARSAL_TYPES" :key="t" :value="t">{{ t }}</option>
                </select>
                <select v-model="s.musicalNumberId" class="min-w-32 rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" @change="saveSlot(s)">
                  <option :value="null">General</option>
                  <option v-for="n in numbers" :key="n.id" :value="n.id">{{ n.title }}</option>
                </select>
                <input v-model="s.notes" placeholder="Notes" class="min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="saveSlot(s)" />
              </div>
              <div class="flex flex-wrap gap-1.5">
                <span
                  v-for="pid in [...attendeesOf(s)].sort((a, b) => performerName(a).localeCompare(performerName(b)))"
                  :key="pid"
                  class="flex items-center gap-1 rounded-full bg-background px-2 py-0.5 text-xs"
                  :class="warningsOf(s).includes(performerName(pid)) ? 'text-orange-600 dark:text-orange-400' : ''"
                >
                  {{ performerName(pid) }}
                  <button
                    class="text-muted-foreground hover:text-destructive"
                    :aria-label="`Remove ${performerName(pid)} from this slot`"
                    @click="overrideOf(s, pid) && !overrideOf(s, pid)!.isExcluded ? clearOverride(s, pid) : setOverride(s, pid, true)"
                  >
                    ×
                  </button>
                </span>
                <select
                  v-if="addableTo(s).length > 0"
                  class="rounded-full border border-dashed border-border bg-transparent px-2 py-0.5 text-xs text-muted-foreground focus:outline-none"
                  :value="''"
                  @change="(e) => { const v = (e.target as HTMLSelectElement).value; if (v) { const pid = Number(v); overrideOf(s, pid)?.isExcluded ? clearOverride(s, pid) : setOverride(s, pid, false); (e.target as HTMLSelectElement).value = '' } }"
                >
                  <option value="">+ Add kid</option>
                  <option v-for="m in addableTo(s)" :key="m.performerId" :value="m.performerId">
                    {{ performerName(m.performerId) }}
                  </option>
                </select>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ============ Calendar lens ============ -->
      <div v-else class="space-y-3">
        <div class="flex items-center justify-between">
          <button class="rounded-md p-1.5 text-muted-foreground hover:bg-accent" aria-label="Previous month" @click="shiftMonth(-1)">
            <ChevronLeft class="h-4 w-4" />
          </button>
          <span class="font-display text-lg font-semibold">{{ monthLabel }}</span>
          <button class="rounded-md p-1.5 text-muted-foreground hover:bg-accent" aria-label="Next month" @click="shiftMonth(1)">
            <ChevronRight class="h-4 w-4" />
          </button>
        </div>
        <div class="overflow-x-auto">
          <div class="min-w-[640px]">
            <div class="grid grid-cols-7 border-b border-border pb-1 text-center text-xs font-medium text-muted-foreground">
              <span v-for="d in ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']" :key="d">{{ d }}</span>
            </div>
            <div v-for="(week, wi) in calendarWeeks" :key="wi" class="grid grid-cols-7">
              <button
                v-for="cell in week"
                :key="toIsoDate(cell.date)"
                class="min-h-20 border-b border-r border-border p-1 text-left align-top first:border-l hover:bg-accent/40"
                :class="[
                  cell.inMonth ? '' : 'bg-muted/40 text-muted-foreground',
                  selectedDay === toIsoDate(cell.date) ? 'ring-1 ring-inset ring-ring' : '',
                ]"
                @click="selectedDay = selectedDay === toIsoDate(cell.date) ? null : toIsoDate(cell.date)"
              >
                <span
                  class="inline-flex h-5 w-5 items-center justify-center rounded-full text-xs"
                  :class="toIsoDate(cell.date) === toIsoDate(today) ? 'bg-primary font-semibold text-primary-foreground' : ''"
                >
                  {{ cell.date.getDate() }}
                </span>
                <span
                  v-for="s in slotsOnDate(toIsoDate(cell.date)).slice(0, 3)"
                  :key="s.id"
                  class="mt-0.5 block truncate rounded px-1 text-[11px] leading-4"
                  :style="{ backgroundColor: tint(REHEARSAL_TYPE_COLORS[s.type], 0.22) }"
                >
                  {{ formatTime(s.startTime) }} {{ numberTitle(s.musicalNumberId) }}
                </span>
                <span v-if="slotsOnDate(toIsoDate(cell.date)).length > 3" class="block px-1 text-[10px] text-muted-foreground">
                  +{{ slotsOnDate(toIsoDate(cell.date)).length - 3 }} more
                </span>
              </button>
            </div>
          </div>
        </div>
        <div v-if="selectedDay" class="rounded-lg border border-border p-4">
          <h3 class="text-sm font-semibold">{{ dayLabel(selectedDay) }}</h3>
          <p v-if="slotsOnDate(selectedDay).length === 0" class="mt-1 text-sm text-muted-foreground">No rehearsals.</p>
          <ul v-else class="mt-2 space-y-1">
            <li v-for="s in slotsOnDate(selectedDay)" :key="s.id" class="flex flex-wrap items-center gap-2 text-sm">
              <span class="tabular-nums">{{ formatTime(s.startTime) }}–{{ formatTime(s.endTime) }}</span>
              <span
                class="rounded px-1.5 py-0.5 text-xs font-medium"
                :style="{ backgroundColor: tint(REHEARSAL_TYPE_COLORS[s.type], 0.22), color: REHEARSAL_TYPE_COLORS[s.type] }"
              >{{ s.type }}</span>
              <span class="font-medium">{{ numberTitle(s.musicalNumberId) }}</span>
              <span class="text-xs text-muted-foreground">{{ attendeesOf(s).size }} kids</span>
              <span v-if="warningsOf(s).length > 0" class="text-xs text-orange-600 dark:text-orange-400">
                ⚠ {{ warningsOf(s).join(', ') }}
              </span>
            </li>
          </ul>
        </div>
      </div>

      <RehearsalEmailDialog
        :open="emailOpen"
        :production-title="scope.selectedProduction?.title ?? ''"
        :week-label="label"
        :slots="weekSlots"
        :numbers="numbers"
        :cast="cast"
        :guardians="guardians"
        :links="guardianLinks"
        :scheduled-performer-ids="scheduledThisWeek"
        :on-download-pdf="downloadPdf"
        @close="emailOpen = false"
      />
      <RehearsalSuggestDialog
        :open="suggestOpen"
        :production-id="scope.selectedProductionId"
        :numbers="numbers"
        :number-casts="numberCasts"
        :conflicts="conflicts"
        :default-from="range.from"
        :default-to="range.to"
        :performer-name="performerName"
        @close="suggestOpen = false"
        @saved="onSuggestSaved"
        @unconfigured="aiHidden = true"
      />
    </div>
  </div>
</template>
