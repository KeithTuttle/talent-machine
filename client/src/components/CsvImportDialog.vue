<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from 'reka-ui'
import { CheckCircle2, AlertTriangle, Copy, Sparkles, Loader2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { conflictLabel } from '@/lib/conflicts'
import type { AiImportResult } from '@/types'
import {
  sheetToRows,
  looksLikeHeader,
  guessMapping,
  matchPerformer,
  parseDateCell,
  parseWeekdays,
  expandRow,
  isDuplicate,
  rowStatus,
  type ColumnMap,
  type ReviewRow,
} from '@/lib/conflictImport'
import type { CastMembership, Conflict } from '@/types'

const props = defineProps<{
  open: boolean
  productionId: number
  cast: CastMembership[]
  existing: Conflict[]
}>()
const emit = defineEmits<{ close: []; imported: [conflicts: Conflict[]] }>()

const fileName = ref('')
const rawRows = ref<string[][]>([])
const hasHeader = ref(true)
const mapping = ref<ColumnMap>({ name: null, start: null, end: null, reason: null, weekday: null })
const reviewRows = ref<ReviewRow[]>([])
const importing = ref(false)
const aiLoading = ref(false)

const TEMPLATE =
  'Name,Start date,End date,Reason,Weekday\n' +
  'Jane Doe,2026-06-12,2026-06-14,Family vacation,\n' +
  'Sam Smith,2026-06-01,2026-07-30,Dance class,"Tue, Thu"\n'
const templateHref = `data:text/csv;charset=utf-8,${encodeURIComponent(TEMPLATE)}`

const castOptions = computed(() =>
  props.cast
    .filter((m) => m.performer)
    .map((m) => ({ id: m.performerId, name: `${m.performer!.firstName} ${m.performer!.lastName}`.trim() }))
    .sort((a, b) => a.name.localeCompare(b.name)),
)

/** Header labels (or synthesized "Column N") for the mapping dropdowns. */
const columns = computed(() => {
  const width = rawRows.value.reduce((m, r) => Math.max(m, r.length), 0)
  const header = hasHeader.value ? rawRows.value[0] ?? [] : []
  return Array.from({ length: width }, (_, i) => (header[i]?.trim() ? header[i] : `Column ${i + 1}`))
})

const dataRows = computed(() => (hasHeader.value ? rawRows.value.slice(1) : rawRows.value))

async function onFile(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  fileName.value = file.name
  try {
    rawRows.value = await sheetToRows(file)
  } catch {
    toast.error("Couldn't read that file — is it a valid CSV or Excel sheet?")
    return
  }
  hasHeader.value = rawRows.value.length > 0 && looksLikeHeader(rawRows.value[0])
  mapping.value = hasHeader.value
    ? guessMapping(rawRows.value[0])
    : { name: 0, start: 1, end: 2, reason: 3, weekday: 4 }
  rebuild()
}

/** (Re)derive the review rows from the current mapping. Resets manual edits. */
function rebuild() {
  const get = (cells: string[], idx: number | null) => (idx != null && idx >= 0 ? cells[idx] ?? '' : '')
  reviewRows.value = dataRows.value
    .map((cells): ReviewRow => {
      const name = get(cells, mapping.value.name)
      return {
        name,
        performerId: matchPerformer(name, props.cast),
        startDate: parseDateCell(get(cells, mapping.value.start)),
        endDate: parseDateCell(get(cells, mapping.value.end)),
        weekdays: parseWeekdays(get(cells, mapping.value.weekday)),
        reason: get(cells, mapping.value.reason) || null,
        include: true,
      }
    })
    .filter((r) => r.name.trim() || r.startDate || r.weekdays.length)
  // Default: exclude rows that already exist.
  for (const r of reviewRows.value) if (rowStatus(r, props.existing) === 'duplicate') r.include = false
}

function onMappingChange(field: keyof ColumnMap, e: Event) {
  const v = (e.target as HTMLSelectElement).value
  mapping.value[field] = v === '' ? null : Number(v)
  rebuild()
}

/** Ask Gemini to read a messy sheet and fill the review table with its proposals. */
async function runAi() {
  aiLoading.value = true
  try {
    const { data } = await api.post<AiImportResult>('/conflicts/import/ai', {
      productionId: props.productionId,
      rows: rawRows.value,
    })
    if (!data.configured) {
      toast.error('AI isn’t configured on the server (no Gemini key).')
      return
    }
    if (!data.ok) {
      toast.error("The AI couldn't read that sheet — try again, or map the columns by hand.")
      return
    }
    const byName = new Map(castOptions.value.map((o) => [o.name.toLowerCase(), o.id]))
    reviewRows.value = data.rows.map((r): ReviewRow => ({
      name: r.performerName,
      // Prefer the AI's roster match; fall back to our own matcher on the raw name.
      performerId: byName.get(r.matchedName.toLowerCase()) ?? matchPerformer(r.performerName, props.cast),
      startDate: r.startDate || null,
      endDate: r.endDate || null,
      weekdays: r.weekdays,
      reason: r.reason || null,
      include: true,
    }))
    for (const row of reviewRows.value) if (rowStatus(row, props.existing) === 'duplicate') row.include = false
    toast.success(`AI found ${data.rows.length} conflict${data.rows.length === 1 ? '' : 's'} — review below.`)
  } catch {
    toast.error('AI cleanup failed — is the server running?')
  } finally {
    aiLoading.value = false
  }
}

const statusOf = (r: ReviewRow) => rowStatus(r, props.existing)
const typeBadge = (r: ReviewRow) =>
  r.weekdays.length > 0
    ? `Weekly ×${r.weekdays.length}`
    : r.startDate
      ? conflictLabel({ type: 'OneOff', startDate: r.startDate, endDate: r.endDate } as Conflict)
      : 'One-off'

const readyCount = computed(() => reviewRows.value.filter((r) => statusOf(r) === 'ready').length)
const attentionCount = computed(
  () => reviewRows.value.filter((r) => ['needs-performer', 'needs-date'].includes(statusOf(r))).length,
)
const dupeCount = computed(() => reviewRows.value.filter((r) => statusOf(r) === 'duplicate').length)

/** The conflicts that will actually be created (included rows, expanded, non-duplicate). */
const toImport = computed(() =>
  reviewRows.value
    .filter((r) => r.include)
    .flatMap((r) => expandRow(r))
    .filter((c) => !isDuplicate(c, props.existing)),
)

async function doImport() {
  if (toImport.value.length === 0) return
  importing.value = true
  try {
    const { data } = await api.post<Conflict[]>('/conflicts/bulk', {
      productionId: props.productionId,
      conflicts: toImport.value.map((c) => ({ id: 0, productionId: props.productionId, ...c })),
    })
    toast.success(`Imported ${data.length} conflict${data.length === 1 ? '' : 's'}`)
    emit('imported', data)
    reset()
    emit('close')
  } finally {
    importing.value = false
  }
}

function reset() {
  fileName.value = ''
  rawRows.value = []
  reviewRows.value = []
  mapping.value = { name: null, start: null, end: null, reason: null, weekday: null }
}

const FIELDS: { key: keyof ColumnMap; label: string; required?: boolean }[] = [
  { key: 'name', label: 'Name', required: true },
  { key: 'start', label: 'Start date' },
  { key: 'end', label: 'End date' },
  { key: 'weekday', label: 'Weekday(s)' },
  { key: 'reason', label: 'Reason' },
]
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) { reset(); emit('close') } }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[88vh] w-[calc(100%-2rem)] max-w-3xl -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">Import conflicts (CSV or Excel)</DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          Upload a spreadsheet of kids' conflicts — any column order. We'll map the columns,
          match names to your cast, and let you fix anything before importing.
          <a :href="templateHref" download="conflicts-template.csv" class="text-primary hover:underline">Download a template</a>.
        </DialogDescription>

        <!-- Step 1: file -->
        <input
          type="file"
          accept=".csv,.txt,.xlsx,.xls,text/csv"
          class="mt-4 block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border file:border-border file:bg-secondary file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-foreground hover:file:bg-accent"
          @change="onFile"
        />

        <!-- Step 2: mapping -->
        <div v-if="rawRows.length > 0" class="mt-4 space-y-3">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Match columns</span>
            <div class="flex items-center gap-3">
              <button
                class="flex items-center gap-1.5 rounded-md border border-primary/40 bg-primary/5 px-2.5 py-1 text-xs font-medium text-primary hover:bg-primary/10 disabled:opacity-50"
                title="Let AI read a messy sheet and fill the review table"
                :disabled="aiLoading"
                @click="runAi"
              >
                <Loader2 v-if="aiLoading" class="h-3.5 w-3.5 animate-spin" />
                <Sparkles v-else class="h-3.5 w-3.5" />
                {{ aiLoading ? 'Reading…' : 'Clean up with AI' }}
              </button>
              <label class="flex items-center gap-1.5 text-xs text-muted-foreground">
                <input type="checkbox" v-model="hasHeader" @change="rebuild" /> First row is a header
              </label>
            </div>
          </div>
          <div class="grid gap-2 sm:grid-cols-5">
            <label v-for="f in FIELDS" :key="f.key" class="space-y-1">
              <span class="text-xs font-medium">{{ f.label }}<span v-if="f.required" class="text-destructive">*</span></span>
              <select
                class="w-full rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                :value="mapping[f.key] ?? ''"
                @change="onMappingChange(f.key, $event)"
              >
                <option value="">—</option>
                <option v-for="(c, i) in columns" :key="i" :value="i">{{ c }}</option>
              </select>
            </label>
          </div>
        </div>

        <!-- Step 3: review -->
        <div v-if="reviewRows.length > 0" class="mt-4 space-y-3">
          <p class="flex flex-wrap gap-x-4 gap-y-1 text-sm">
            <span class="font-medium text-emerald-600 dark:text-emerald-400">{{ readyCount }} ready</span>
            <span v-if="attentionCount > 0" class="font-medium text-amber-600 dark:text-amber-500">{{ attentionCount }} need attention</span>
            <span v-if="dupeCount > 0" class="font-medium text-muted-foreground">{{ dupeCount }} duplicate</span>
            <span class="text-xs text-muted-foreground">{{ fileName }}</span>
          </p>

          <ul class="max-h-72 space-y-1.5 overflow-y-auto rounded-md border border-border p-2">
            <li
              v-for="(r, i) in reviewRows"
              :key="i"
              class="rounded-md border border-border/60 p-2 text-xs"
              :class="r.include ? '' : 'opacity-50'"
            >
              <div class="flex items-center gap-2">
                <input type="checkbox" v-model="r.include" class="shrink-0" :aria-label="`Include ${r.name}`" />
                <CheckCircle2 v-if="statusOf(r) === 'ready'" class="h-4 w-4 shrink-0 text-emerald-500" />
                <Copy v-else-if="statusOf(r) === 'duplicate'" class="h-4 w-4 shrink-0 text-muted-foreground" />
                <AlertTriangle v-else class="h-4 w-4 shrink-0 text-amber-500" />
                <span class="w-28 shrink-0 truncate font-medium" :title="r.name">{{ r.name || '(no name)' }}</span>
                <span class="text-muted-foreground">→</span>
                <select
                  v-model="r.performerId"
                  class="min-w-0 flex-1 rounded-md border bg-background px-1.5 py-1 focus:outline-none"
                  :class="r.performerId == null ? 'border-amber-500' : 'border-border'"
                >
                  <option :value="null">— unmatched —</option>
                  <option v-for="o in castOptions" :key="o.id" :value="o.id">{{ o.name }}</option>
                </select>
                <span class="shrink-0 rounded-full bg-muted px-2 py-0.5 text-[11px] text-muted-foreground">{{ typeBadge(r) }}</span>
              </div>
              <div class="mt-1.5 flex flex-wrap items-center gap-2 pl-6 text-muted-foreground">
                <template v-if="r.weekdays.length === 0">
                  <span>From</span>
                  <input type="date" v-model="r.startDate" class="rounded border border-border bg-background px-1 py-0.5 focus:outline-none" />
                  <span>to</span>
                  <input type="date" v-model="r.endDate" class="rounded border border-border bg-background px-1 py-0.5 focus:outline-none" />
                </template>
                <template v-else>
                  <span class="rounded bg-accent px-1.5 py-0.5 text-foreground">{{ r.weekdays.join(', ') }}</span>
                  <span>from</span>
                  <input type="date" v-model="r.startDate" class="rounded border border-border bg-background px-1 py-0.5 focus:outline-none" />
                  <span v-if="r.endDate || true">until</span>
                  <input type="date" v-model="r.endDate" class="rounded border border-border bg-background px-1 py-0.5 focus:outline-none" />
                </template>
                <span v-if="r.reason" class="italic">“{{ r.reason }}”</span>
                <span v-if="statusOf(r) === 'duplicate'" class="text-[11px]">already imported</span>
              </div>
            </li>
          </ul>

          <div class="flex justify-end gap-2">
            <button class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent" @click="reset(); emit('close')">
              Cancel
            </button>
            <button
              class="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="toImport.length === 0 || importing"
              @click="doImport"
            >
              {{ importing ? 'Importing…' : `Import ${toImport.length}` }}
            </button>
          </div>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
