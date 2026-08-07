<script setup lang="ts">
// Read-only production home. One aggregate call (/api/dashboard) feeds three
// cards: countdown + rollups, this week's rehearsals, and attendance flags.
import { computed, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  LayoutDashboard,
  Shirt,
  Users,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { formatTime } from '@/lib/rehearsals'
import { useScopeStore } from '@/stores/scope'
import type { Dashboard } from '@/types'

const scope = useScopeStore()
const data = ref<Dashboard | null>(null)
const loading = ref(false)

async function load() {
  const pid = scope.selectedProductionId
  if (pid === null) {
    data.value = null
    return
  }
  loading.value = true
  try {
    const { data: d } = await api.get<Dashboard>(`/dashboard?productionId=${pid}`)
    data.value = d
  } catch {
    data.value = null
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(() => scope.selectedProductionId, load)

const pct = (n: number, d: number) => (d === 0 ? 0 : Math.round((100 * n) / d))

// A number counts as taught once it has any status at all — marking one "Taught"
// has to move this bar, or the tracker looks broken. "Complete" is the further
// milestone, shown as the darker segment inside the same bar rather than as the
// whole story: previously it WAS the whole story, so a show could be entirely
// taught and still read 0.
const taughtCount = computed(() =>
  data.value ? data.value.rollups.numbersTotal - data.value.rollups.notTaught : 0,
)
const teachPct = computed(() =>
  data.value ? pct(taughtCount.value, data.value.rollups.numbersTotal) : 0,
)
const teachCompletePct = computed(() =>
  data.value ? pct(data.value.rollups.teachComplete, data.value.rollups.numbersTotal) : 0,
)
const rolesPct = computed(() =>
  data.value ? pct(data.value.rollups.rolesCast, data.value.rollups.rolesTotal) : 0,
)
const castPct = computed(() =>
  data.value ? pct(data.value.rollups.numbersWithCast, data.value.rollups.numbersTotal) : 0,
)
const costumeReadyPct = computed(() =>
  data.value ? pct(data.value.costumes.ready, data.value.costumes.total) : 0,
)
const fittingPct = computed(() =>
  data.value ? pct(data.value.costumes.fittingsDone, data.value.costumes.fittingsTotal) : 0,
)

const countdownLabel = computed(() => {
  const d = data.value?.countdown.daysToOpen
  if (d == null) return null
  if (d < 0) return `${Math.abs(d)} days ago`
  if (d === 0) return 'Today!'
  return `${d}`
})

function weekday(date: string) {
  const d = new Date(date + 'T00:00:00')
  return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <LayoutDashboard class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production to see its dashboard.
      </p>
    </div>

    <div v-else-if="data" class="mx-auto max-w-5xl space-y-4">
      <div>
        <h1 class="font-display text-2xl font-bold">{{ data.title }}</h1>
        <p class="text-sm text-muted-foreground">Production dashboard</p>
      </div>

      <!-- Countdown + rollups -->
      <div class="grid gap-4 md:grid-cols-3">
        <!-- Countdown hero -->
        <div class="flex flex-col justify-center rounded-lg border border-border bg-card p-5 text-center">
          <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Opening</span>
          <template v-if="countdownLabel !== null">
            <span class="font-display mt-1 text-5xl font-bold leading-none text-primary">
              {{ countdownLabel }}
            </span>
            <span v-if="data.countdown.daysToOpen! > 0" class="mt-1 text-sm text-muted-foreground">days to go</span>
            <span class="mt-2 text-xs text-muted-foreground">{{ data.countdown.openingDate }}</span>
          </template>
          <span v-else class="font-display mt-2 text-2xl font-semibold text-muted-foreground">No date set</span>
        </div>

        <!-- Rollups -->
        <div class="rounded-lg border border-border bg-card p-5 md:col-span-2">
          <div class="space-y-3">
            <div>
              <div class="mb-1 flex items-center justify-between text-sm">
                <span class="font-medium">Numbers taught</span>
                <span class="text-muted-foreground">{{ taughtCount }}/{{ data.rollups.numbersTotal }} taught</span>
              </div>
              <!-- Pale fill = taught at all; solid = polished to Complete. -->
              <div class="relative h-2 overflow-hidden rounded-full bg-muted">
                <div class="absolute inset-y-0 left-0 rounded-full bg-green-500/40" :style="{ width: teachPct + '%' }" />
                <div class="absolute inset-y-0 left-0 rounded-full bg-green-500" :style="{ width: teachCompletePct + '%' }" />
              </div>
            </div>
            <div>
              <div class="mb-1 flex items-center justify-between text-sm">
                <span class="font-medium">Roles cast</span>
                <span class="text-muted-foreground">{{ data.rollups.rolesCast }}/{{ data.rollups.rolesTotal }}</span>
              </div>
              <div class="h-2 overflow-hidden rounded-full bg-muted">
                <div class="h-full rounded-full bg-primary" :style="{ width: rolesPct + '%' }" />
              </div>
            </div>
            <div>
              <div class="mb-1 flex items-center justify-between text-sm">
                <span class="font-medium">Numbers with cast</span>
                <span class="text-muted-foreground">{{ data.rollups.numbersWithCast }}/{{ data.rollups.numbersTotal }}</span>
              </div>
              <div class="h-2 overflow-hidden rounded-full bg-muted">
                <div class="h-full rounded-full bg-blue-500" :style="{ width: castPct + '%' }" />
              </div>
            </div>
            <div class="flex flex-wrap gap-x-6 gap-y-1 pt-1 text-sm text-muted-foreground">
              <span><Users class="mr-1 inline h-4 w-4" />{{ data.rollups.performersInShow }} performers</span>
              <span v-if="data.rollups.teachComplete > 0">{{ data.rollups.teachComplete }} complete</span>
              <span v-if="data.rollups.notTaught > 0">{{ data.rollups.notTaught }} not taught yet</span>
              <span v-if="data.rollups.teachNeedsReview > 0" class="text-amber-600 dark:text-amber-500">
                {{ data.rollups.teachNeedsReview }} need review
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Costume readiness (only once there are costumes to track) -->
      <div v-if="data.costumes.total > 0" class="rounded-lg border border-border bg-card p-5">
        <h2 class="mb-3 flex items-center gap-2 font-display text-lg font-semibold">
          <Shirt class="h-5 w-5 text-primary" /> Costumes
        </h2>
        <div class="space-y-3">
          <div>
            <div class="mb-1 flex items-center justify-between text-sm">
              <span class="font-medium">Costumes ready</span>
              <span class="text-muted-foreground">{{ data.costumes.ready }}/{{ data.costumes.total }}</span>
            </div>
            <div class="h-2 overflow-hidden rounded-full bg-muted">
              <div class="h-full rounded-full bg-green-500" :style="{ width: costumeReadyPct + '%' }" />
            </div>
          </div>
          <div v-if="data.costumes.fittingsTotal > 0">
            <div class="mb-1 flex items-center justify-between text-sm">
              <span class="font-medium">Fittings done</span>
              <span class="text-muted-foreground">{{ data.costumes.fittingsDone }}/{{ data.costumes.fittingsTotal }}</span>
            </div>
            <div class="h-2 overflow-hidden rounded-full bg-muted">
              <div class="h-full rounded-full bg-primary" :style="{ width: fittingPct + '%' }" />
            </div>
          </div>
          <div class="flex flex-wrap gap-x-6 gap-y-1 pt-1 text-sm text-muted-foreground">
            <span v-if="data.costumes.needed > 0" class="text-destructive">
              {{ data.costumes.needed }} still needed
            </span>
            <span v-if="data.costumes.sourced > 0" class="text-amber-600 dark:text-amber-500">
              {{ data.costumes.sourced }} sourced, not ready
            </span>
            <span v-if="data.costumes.quickChanges > 0">
              <AlertTriangle class="mr-1 inline h-4 w-4 text-orange-500" />
              {{ data.costumes.quickChanges }} back-to-back
              {{ data.costumes.quickChanges === 1 ? 'change' : 'changes' }}
            </span>
          </div>
        </div>
      </div>

      <div class="grid gap-4 md:grid-cols-2">
        <!-- This week -->
        <div class="rounded-lg border border-border bg-card p-5">
          <h2 class="mb-3 flex items-center gap-2 font-display text-lg font-semibold">
            <CalendarClock class="h-5 w-5 text-primary" /> This week
          </h2>
          <p v-if="data.thisWeek.length === 0" class="text-sm italic text-muted-foreground">
            No rehearsals in the next 7 days.
          </p>
          <ul v-else class="space-y-2">
            <li
              v-for="s in data.thisWeek"
              :key="s.id"
              class="flex items-center gap-3 rounded-md border border-border px-3 py-2 text-sm"
            >
              <div class="w-24 shrink-0">
                <div class="font-medium">{{ weekday(s.date) }}</div>
                <div class="text-xs text-muted-foreground">{{ formatTime(s.startTime) }}</div>
              </div>
              <div class="min-w-0 flex-1">
                <div class="truncate font-medium">{{ s.numberTitle ?? 'General session' }}</div>
                <div class="text-xs text-muted-foreground">{{ s.type }} · {{ s.attendees }} in the room</div>
              </div>
              <span
                v-if="s.conflicts > 0"
                class="flex shrink-0 items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-950 dark:text-amber-400"
                :title="`${s.conflicts} cast member(s) have a conflict this day`"
              >
                <AlertTriangle class="h-3.5 w-3.5" /> {{ s.conflicts }}
              </span>
            </li>
          </ul>
        </div>

        <!-- Attendance flags -->
        <div class="rounded-lg border border-border bg-card p-5">
          <h2 class="mb-3 flex items-center gap-2 font-display text-lg font-semibold">
            <CheckCircle2 class="h-5 w-5 text-primary" /> Attendance
          </h2>
          <template v-if="data.attendance.recordedSessions > 0">
            <div class="mb-3 flex items-baseline gap-2">
              <span class="font-display text-3xl font-bold" :class="data.attendance.avgPercent >= 85 ? 'text-green-600 dark:text-green-500' : 'text-amber-600 dark:text-amber-500'">
                {{ data.attendance.avgPercent }}%
              </span>
              <span class="text-sm text-muted-foreground">
                average across {{ data.attendance.recordedSessions }}
                {{ data.attendance.recordedSessions === 1 ? 'session' : 'sessions' }}
              </span>
            </div>
            <p v-if="data.attendance.atRisk.length === 0" class="text-sm text-muted-foreground">
              No one below 75% — nice.
            </p>
            <div v-else class="space-y-1.5">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">At risk (unexcused)</span>
              <div
                v-for="p in data.attendance.atRisk"
                :key="p.performerId"
                class="flex items-center gap-2 text-sm"
              >
                <AlertTriangle class="h-3.5 w-3.5 shrink-0 text-amber-600 dark:text-amber-500" />
                <span class="flex-1 truncate">{{ p.name }}</span>
                <span class="text-muted-foreground">{{ p.present }}/{{ p.total }} present</span>
                <span class="w-10 text-right font-medium">{{ p.percent }}%</span>
              </div>
            </div>
          </template>
          <p v-else class="text-sm italic text-muted-foreground">
            No attendance recorded yet — mark some in Rehearsals.
          </p>
        </div>
      </div>
    </div>

    <div v-else-if="loading" class="mx-auto mt-16 max-w-md text-center text-sm text-muted-foreground">
      Loading…
    </div>
  </div>
</template>
