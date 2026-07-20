<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { CalendarOff, ChevronLeft, ChevronRight, Plus, Trash2, Upload } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { ageOn } from '@/lib/age'
import { tint } from '@/lib/colors'
import { WEEKDAYS, conflictLabel, occursOn, toIsoDate, parseDate } from '@/lib/conflicts'
import ColorDot from '@/components/ColorDot.vue'
import CsvImportDialog from '@/components/CsvImportDialog.vue'
import { useScopeStore } from '@/stores/scope'
import type { CastGroup, CastMembership, Conflict, Weekday } from '@/types'

const scope = useScopeStore()

const cast = ref<CastMembership[]>([])
const groups = ref<CastGroup[]>([])
const conflicts = ref<Conflict[]>([])

const lens = ref<'calendar' | 'kid' | 'date'>('calendar')
const search = ref('')
const groupFilter = ref('') // '' or 'cg:<id>'
const importOpen = ref(false)

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
    cast.value = []
    groups.value = []
    conflicts.value = []
    return
  }
  ;[cast.value, groups.value, conflicts.value] = await Promise.all([
    safeGet<CastMembership>(`/castmemberships?productionId=${pid}`),
    safeGet<CastGroup>(`/castgroups?productionId=${pid}`),
    safeGet<Conflict>(`/conflicts?productionId=${pid}`),
  ])
}
onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Filtering ---------------------------------------------------------------

const memberOf = (performerId: number) => cast.value.find((m) => m.performerId === performerId)
const performerName = (performerId: number) => {
  const p = memberOf(performerId)?.performer
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${performerId}`
}
const firstName = (performerId: number) =>
  memberOf(performerId)?.performer?.firstName ?? `#${performerId}`
const groupColor = (performerId: number) => {
  const m = memberOf(performerId)
  return groups.value.find((g) => g.id === m?.castGroupId)?.color ?? null
}

function memberPassesFilter(m: CastMembership): boolean {
  if (groupFilter.value.startsWith('cg:') && m.castGroupId !== Number(groupFilter.value.slice(3)))
    return false
  const q = search.value.trim().toLowerCase()
  if (q) {
    const name = performerName(m.performerId).toLowerCase()
    const inConflictReason = conflicts.value.some(
      (c) => c.performerId === m.performerId && (c.reason ?? '').toLowerCase().includes(q),
    )
    if (!name.includes(q) && !inConflictReason) return false
  }
  return true
}

const filteredCast = computed(() =>
  cast.value
    .filter(memberPassesFilter)
    .sort((a, b) => performerName(a.performerId).localeCompare(performerName(b.performerId))),
)

const filteredConflicts = computed(() => {
  const allowed = new Set(filteredCast.value.map((m) => m.performerId))
  const q = search.value.trim().toLowerCase()
  return conflicts.value.filter((c) => {
    if (!allowed.has(c.performerId)) return false
    // When searching, keep a kid's conflicts if the name matched OR this reason matches.
    if (q && !performerName(c.performerId).toLowerCase().includes(q))
      return (c.reason ?? '').toLowerCase().includes(q)
    return true
  })
})

const conflictsOn = (date: Date) => filteredConflicts.value.filter((c) => occursOn(c, date))

// --- Calendar ----------------------------------------------------------------

const today = new Date()
const calYear = ref(today.getFullYear())
const calMonth = ref(today.getMonth())
const selectedDay = ref<string | null>(null)

const monthLabel = computed(() =>
  new Date(calYear.value, calMonth.value, 1).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  }),
)

function shiftMonth(delta: number) {
  const d = new Date(calYear.value, calMonth.value + delta, 1)
  calYear.value = d.getFullYear()
  calMonth.value = d.getMonth()
}

/** Weeks (Mon-first) covering the visible month; each cell = { date, inMonth }. */
const calendarWeeks = computed(() => {
  const first = new Date(calYear.value, calMonth.value, 1)
  const start = new Date(first)
  start.setDate(first.getDate() - ((first.getDay() + 6) % 7)) // back to Monday
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

const isToday = (d: Date) => toIsoDate(d) === toIsoDate(today)

const selectedDayConflicts = computed(() => {
  const d = parseDate(selectedDay.value)
  return d ? conflictsOn(d) : []
})

// --- By-date lens ------------------------------------------------------------

/** Next 8 weeks of days that have at least one conflict. */
const upcomingDays = computed(() => {
  const out: { date: Date; conflicts: Conflict[] }[] = []
  const cursor = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  for (let i = 0; i < 56; i++) {
    const hits = conflictsOn(cursor)
    if (hits.length > 0) out.push({ date: new Date(cursor), conflicts: hits })
    cursor.setDate(cursor.getDate() + 1)
  }
  return out
})

// --- Add / edit / delete -----------------------------------------------------

const addFor = ref<number | null>(null) // performerId with the open add form
const formType = ref<'OneOff' | 'Weekly'>('OneOff')
const formStart = ref('')
const formEnd = ref('')
const formWeekday = ref<Weekday>('Tuesday')
const formReason = ref('')

function openAdd(performerId: number) {
  addFor.value = addFor.value === performerId ? null : performerId
  formType.value = 'OneOff'
  formStart.value = ''
  formEnd.value = ''
  formReason.value = ''
}

async function addConflict() {
  const pid = scope.selectedProductionId
  if (pid === null || addFor.value === null || !formStart.value) return
  const { data } = await api.post<Conflict>('/conflicts', {
    id: 0,
    productionId: pid,
    performerId: addFor.value,
    type: formType.value,
    startDate: formStart.value,
    endDate: formEnd.value || null,
    weekday: formType.value === 'Weekly' ? formWeekday.value : null,
    reason: formReason.value.trim() || null,
  })
  conflicts.value.push(data)
  addFor.value = null
}

async function removeConflict(c: Conflict) {
  const ok = await confirm({
    title: `Delete this conflict for ${performerName(c.performerId)}?`,
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/conflicts/${c.id}`)
  conflicts.value = conflicts.value.filter((x) => x.id !== c.id)
}

function onImported(imported: Conflict[]) {
  conflicts.value.push(...imported)
}

const kidConflicts = (performerId: number) =>
  filteredConflicts.value.filter((c) => c.performerId === performerId)

const showDate = computed(() => scope.selectedProduction?.showDate)
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <CalendarOff class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production, then track the kids' conflicts here.
      </p>
    </div>

    <div v-else class="mx-auto max-w-5xl space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">Conflicts</h1>
          <p class="text-sm text-muted-foreground">
            {{ scope.selectedProduction?.title }} — who's unavailable, and when.
          </p>
        </div>
        <button
          class="flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
          @click="importOpen = true"
        >
          <Upload class="h-4 w-4" /> Import CSV
        </button>
      </div>

      <!-- Shared controls -->
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex rounded-md border border-border text-sm">
          <button
            v-for="l in ([['calendar', 'Calendar'], ['kid', 'By kid'], ['date', 'By date']] as const)"
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
          placeholder="Search kids or reasons…"
          class="min-w-44 flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring sm:flex-none"
        />
        <select
          v-model="groupFilter"
          class="rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        >
          <option value="">All groups</option>
          <option v-for="g in groups" :key="g.id" :value="`cg:${g.id}`">{{ g.name }}</option>
        </select>
      </div>

      <!-- ============ Calendar lens ============ -->
      <div v-if="lens === 'calendar'" class="space-y-3">
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
                  :class="[
                    isToday(cell.date) ? 'bg-primary font-semibold text-primary-foreground' : '',
                    showDate === toIsoDate(cell.date) ? 'font-bold text-primary underline' : '',
                  ]"
                >
                  {{ cell.date.getDate() }}
                </span>
                <span
                  v-for="c in conflictsOn(cell.date).slice(0, 3)"
                  :key="c.id"
                  class="mt-0.5 block truncate rounded px-1 text-[11px] leading-4"
                  :style="{ backgroundColor: tint(groupColor(c.performerId)) ?? 'hsl(var(--muted))' }"
                >
                  {{ firstName(c.performerId) }}
                </span>
                <span v-if="conflictsOn(cell.date).length > 3" class="block px-1 text-[10px] text-muted-foreground">
                  +{{ conflictsOn(cell.date).length - 3 }} more
                </span>
              </button>
            </div>
          </div>
        </div>

        <!-- Day detail -->
        <div v-if="selectedDay" class="rounded-lg border border-border p-4">
          <h3 class="text-sm font-semibold">
            {{ parseDate(selectedDay)?.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' }) }}
          </h3>
          <p v-if="selectedDayConflicts.length === 0" class="mt-1 text-sm text-muted-foreground">
            Everybody's available. 🎉
          </p>
          <ul v-else class="mt-2 space-y-1">
            <li v-for="c in selectedDayConflicts" :key="c.id" class="flex items-center gap-2 text-sm">
              <ColorDot :color="groupColor(c.performerId)" size="sm" />
              <span class="font-medium">{{ performerName(c.performerId) }}</span>
              <span class="text-xs text-muted-foreground">{{ conflictLabel(c) }}</span>
              <span v-if="c.reason" class="truncate text-xs text-muted-foreground">— {{ c.reason }}</span>
            </li>
          </ul>
        </div>
      </div>

      <!-- ============ By-kid lens ============ -->
      <div v-else-if="lens === 'kid'" class="space-y-2">
        <p v-if="filteredCast.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          {{ cast.length === 0 ? 'No cast yet — add performers in the Planner first.' : 'No matches.' }}
        </p>
        <div v-for="m in filteredCast" :key="m.id" class="rounded-lg border border-border">
          <div class="flex items-center gap-2 px-4 py-2.5">
            <ColorDot :color="groupColor(m.performerId)" size="sm" />
            <span class="flex-1 text-sm font-medium">
              {{ performerName(m.performerId) }}
              <span v-if="ageOn(m.performer?.dateOfBirth, showDate) !== null" class="ml-1 text-xs font-normal text-muted-foreground">
                {{ ageOn(m.performer?.dateOfBirth, showDate) }}
              </span>
            </span>
            <span class="text-xs text-muted-foreground">
              {{ kidConflicts(m.performerId).length }} conflict{{ kidConflicts(m.performerId).length === 1 ? '' : 's' }}
            </span>
            <button
              class="flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
              @click="openAdd(m.performerId)"
            >
              <Plus class="h-3.5 w-3.5" /> Add
            </button>
          </div>

          <!-- Add form -->
          <form
            v-if="addFor === m.performerId"
            class="flex flex-wrap items-center gap-2 border-t border-border bg-muted/30 px-4 py-2.5"
            @submit.prevent="addConflict"
          >
            <div class="flex rounded-md border border-border text-xs">
              <button
                v-for="t in ([['OneOff', 'One-off'], ['Weekly', 'Weekly']] as const)"
                :key="t[0]"
                type="button"
                class="px-2 py-1 font-medium"
                :class="formType === t[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground'"
                @click="formType = t[0]"
              >
                {{ t[1] }}
              </button>
            </div>
            <select
              v-if="formType === 'Weekly'"
              v-model="formWeekday"
              class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
            >
              <option v-for="w in WEEKDAYS" :key="w" :value="w">{{ w }}</option>
            </select>
            <input v-model="formStart" type="date" required class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" :aria-label="formType === 'Weekly' ? 'From' : 'Start date'" />
            <span class="text-xs text-muted-foreground">to</span>
            <input v-model="formEnd" type="date" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" :aria-label="formType === 'Weekly' ? 'Until (optional)' : 'End date (optional)'" />
            <input v-model="formReason" placeholder="Reason" class="min-w-28 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring" />
            <button type="submit" class="rounded-md bg-primary px-2.5 py-1 text-xs font-medium text-primary-foreground hover:opacity-90">Save</button>
          </form>

          <ul v-if="kidConflicts(m.performerId).length > 0" class="divide-y divide-border border-t border-border">
            <li
              v-for="c in kidConflicts(m.performerId)"
              :key="c.id"
              class="flex items-center gap-2 px-4 py-2 text-sm"
            >
              <span
                class="rounded px-1.5 py-0.5 text-xs font-medium"
                :class="c.type === 'Weekly' ? 'bg-accent text-accent-foreground' : 'bg-muted text-muted-foreground'"
              >
                {{ conflictLabel(c) }}
              </span>
              <span class="flex-1 truncate text-xs text-muted-foreground">{{ c.reason }}</span>
              <button
                class="rounded p-1 text-muted-foreground hover:text-destructive"
                aria-label="Delete conflict"
                @click="removeConflict(c)"
              >
                <Trash2 class="h-3.5 w-3.5" />
              </button>
            </li>
          </ul>
        </div>
      </div>

      <!-- ============ By-date lens ============ -->
      <div v-else class="space-y-2">
        <p v-if="upcomingDays.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          No conflicts in the next 8 weeks{{ search || groupFilter ? ' (matching your filters)' : '' }}.
        </p>
        <div v-for="day in upcomingDays" :key="toIsoDate(day.date)" class="rounded-lg border border-border px-4 py-2.5">
          <h3 class="text-sm font-semibold">
            {{ day.date.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' }) }}
            <span class="ml-1 text-xs font-normal text-muted-foreground">{{ day.conflicts.length }} out</span>
          </h3>
          <ul class="mt-1 space-y-0.5">
            <li v-for="c in day.conflicts" :key="c.id" class="flex items-center gap-2 text-sm">
              <ColorDot :color="groupColor(c.performerId)" size="sm" />
              {{ performerName(c.performerId) }}
              <span v-if="c.reason" class="truncate text-xs text-muted-foreground">— {{ c.reason }}</span>
              <span v-if="c.type === 'Weekly'" class="text-xs text-muted-foreground">({{ conflictLabel(c) }})</span>
            </li>
          </ul>
        </div>
      </div>

      <CsvImportDialog
        :open="importOpen"
        :production-id="scope.selectedProductionId"
        :cast="cast"
        @close="importOpen = false"
        @imported="onImported"
      />
    </div>
  </div>
</template>
