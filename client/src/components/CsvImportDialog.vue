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
import { CheckCircle2, AlertTriangle } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { WEEKDAYS, toIsoDate } from '@/lib/conflicts'
import type { CastMembership, Conflict, Weekday } from '@/types'

const props = defineProps<{ open: boolean; productionId: number; cast: CastMembership[] }>()
const emit = defineEmits<{ close: []; imported: [conflicts: Conflict[]] }>()

interface ParsedRow {
  raw: string
  name: string
  performerId: number | null
  conflict: Omit<Conflict, 'id' | 'productionId'> | null
  problem: string | null
}

const rows = ref<ParsedRow[]>([])
const fileName = ref('')
const importing = ref(false)

const matched = computed(() => rows.value.filter((r) => r.performerId !== null && r.conflict))
const unmatched = computed(() => rows.value.filter((r) => r.performerId === null || !r.conflict))

const TEMPLATE =
  'Name,Start date,End date,Reason,Weekday\n' +
  'Jane Doe,2026-06-12,2026-06-14,Family vacation,\n' +
  'Sam Smith,2026-06-01,2026-07-30,Dance class,Tuesday\n'
const templateHref = `data:text/csv;charset=utf-8,${encodeURIComponent(TEMPLATE)}`

/** Accept yyyy-MM-dd or M/D/yyyy (US spreadsheet default). */
function parseCellDate(cell: string): string | null {
  const t = cell.trim()
  if (!t) return null
  if (/^\d{4}-\d{2}-\d{2}$/.test(t)) return t
  const us = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2,4})$/.exec(t)
  if (us) {
    const year = us[3].length === 2 ? `20${us[3]}` : us[3]
    const d = new Date(Number(year), Number(us[1]) - 1, Number(us[2]))
    return Number.isNaN(d.getTime()) ? null : toIsoDate(d)
  }
  return null
}

function parseWeekday(cell: string): Weekday | null {
  const t = cell.trim().toLowerCase()
  if (!t) return null
  return WEEKDAYS.find((w) => w.toLowerCase().startsWith(t.slice(0, 3))) ?? null
}

function matchPerformer(name: string): number | null {
  const norm = name.trim().toLowerCase().replace(/\s+/g, ' ')
  if (!norm) return null
  for (const m of props.cast) {
    const p = m.performer
    if (!p) continue
    const firstLast = `${p.firstName} ${p.lastName}`.trim().toLowerCase().replace(/\s+/g, ' ')
    const lastFirst = `${p.lastName}, ${p.firstName}`.trim().toLowerCase().replace(/\s+/g, ' ')
    if (norm === firstLast || norm === lastFirst) return m.performerId
  }
  return null
}

function parseText(text: string) {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0)
  if (lines.length === 0) {
    rows.value = []
    return
  }
  // Delimiter: whichever of tab/;/, splits the first line into the most cells.
  const delimiter = ['\t', ';', ','].reduce((best, d) =>
    lines[0].split(d).length > lines[0].split(best).length ? d : best,
  )
  const dataLines = /name/i.test(lines[0].split(delimiter)[0] ?? '') ? lines.slice(1) : lines

  rows.value = dataLines.map((line) => {
    const cells = line.split(delimiter).map((c) => c.trim().replace(/^"|"$/g, ''))
    const [name = '', startCell = '', endCell = '', reason = '', weekdayCell = ''] = cells
    const performerId = matchPerformer(name)
    const startDate = parseCellDate(startCell)
    const endDate = parseCellDate(endCell)
    const weekday = parseWeekday(weekdayCell)

    let problem: string | null = null
    if (!name) problem = 'Missing name'
    else if (performerId === null) problem = 'No cast member with this name'
    else if (!startDate) problem = 'Unreadable start date'

    return {
      raw: line,
      name,
      performerId,
      problem,
      conflict:
        problem === null && startDate
          ? {
              performerId: performerId!,
              type: weekday ? 'Weekly' : 'OneOff',
              startDate,
              endDate,
              weekday,
              reason: reason || null,
            }
          : null,
    }
  })
}

async function onFile(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  fileName.value = file.name
  parseText(await file.text())
}

async function doImport() {
  if (matched.value.length === 0) return
  importing.value = true
  try {
    const { data } = await api.post<Conflict[]>('/conflicts/bulk', {
      productionId: props.productionId,
      conflicts: matched.value.map((r) => ({ id: 0, productionId: props.productionId, ...r.conflict })),
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
  rows.value = []
  fileName.value = ''
}
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) { reset(); emit('close') } }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[85vh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">Import conflicts from CSV</DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          Columns: <code class="text-xs">Name, Start date, End date, Reason, Weekday</code>.
          A weekday makes the row a recurring weekly conflict. Names must match cast
          members ("First Last" or "Last, First").
          <a :href="templateHref" download="conflicts-template.csv" class="text-primary hover:underline">
            Download template</a>.
        </DialogDescription>

        <input
          type="file"
          accept=".csv,.txt,text/csv"
          class="mt-4 block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border file:border-border file:bg-background file:px-3 file:py-1.5 file:text-sm file:font-medium hover:file:bg-accent"
          @change="onFile"
        />

        <div v-if="rows.length > 0" class="mt-4 space-y-3">
          <p class="text-sm">
            <span class="font-medium text-emerald-600">{{ matched.length }} ready</span>
            <span v-if="unmatched.length > 0" class="ml-2 font-medium text-destructive">
              {{ unmatched.length }} skipped
            </span>
            <span class="ml-2 text-xs text-muted-foreground">{{ fileName }}</span>
          </p>

          <ul class="max-h-60 space-y-1 overflow-y-auto rounded-md border border-border p-2">
            <li v-for="(r, i) in rows" :key="i" class="flex items-start gap-2 text-xs">
              <CheckCircle2 v-if="!r.problem" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-500" />
              <AlertTriangle v-else class="mt-0.5 h-3.5 w-3.5 shrink-0 text-destructive" />
              <span class="min-w-0 flex-1 truncate">
                <span class="font-medium">{{ r.name || '(no name)' }}</span>
                <span v-if="r.problem" class="ml-1 text-destructive">— {{ r.problem }}</span>
                <span v-else class="ml-1 text-muted-foreground">{{ r.raw }}</span>
              </span>
            </li>
          </ul>

          <div class="flex justify-end gap-2">
            <button
              class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
              @click="reset(); emit('close')"
            >
              Cancel
            </button>
            <button
              class="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="matched.length === 0 || importing"
              @click="doImport"
            >
              {{ importing ? 'Importing…' : `Import ${matched.length}` }}
            </button>
          </div>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
