<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from 'reka-ui'
import { CheckCircle2, AlertTriangle, UserPlus, Copy, Sparkles, Loader2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { useScopeStore } from '@/stores/scope'
import {
  sheetToRows,
  guessCastMapping,
  buildRow,
  rowStatus,
  matchPerformer,
  parseGender,
  ageToDob,
  type CastColumnMap,
  type CastReviewRow,
} from '@/lib/castImport'
import { looksLikeHeader } from '@/lib/conflictImport'
import type { AiCastResult, CastImportRow, CastImportSummary, CastMembership, Gender, Performer } from '@/types'

const props = defineProps<{ open: boolean; roster: Performer[] }>()
const emit = defineEmits<{ close: []; imported: [] }>()

const scope = useScopeStore()

const fileName = ref('')
const rawRows = ref<string[][]>([])
const hasHeader = ref(true)
const mapping = ref<CastColumnMap>(emptyMap())
const reviewRows = ref<CastReviewRow[]>([])
const castPerformerIds = ref<Set<number>>(new Set())
const importing = ref(false)
const aiLoading = ref(false)

function emptyMap(): CastColumnMap {
  return {
    firstName: null, lastName: null, fullName: null, gender: null, dob: null, age: null,
    guardianName: null, guardianEmail: null, guardianPhone: null, castGroup: null, notes: null,
  }
}

const productionId = computed(() => scope.selectedProductionId)
const refIso = computed(() => scope.selectedProduction?.openingDate ?? null)

// Load who's already in this show's cast, so we can flag "already in cast".
watch(
  () => [props.open, productionId.value] as const,
  async ([isOpen, pid]) => {
    if (!isOpen || pid == null) return
    try {
      const { data } = await api.get<CastMembership[]>(`/castmemberships?productionId=${pid}`)
      castPerformerIds.value = new Set(data.map((m) => m.performerId))
    } catch {
      castPerformerIds.value = new Set()
    }
  },
  { immediate: true },
)

const rosterOptions = computed(() =>
  [...props.roster]
    .map((p) => ({ id: p.id, name: `${p.firstName} ${p.lastName}`.trim() }))
    .sort((a, b) => a.name.localeCompare(b.name)),
)

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
  mapping.value = hasHeader.value ? guessCastMapping(rawRows.value[0]) : emptyMap()
  rebuild()
}

function rebuild() {
  reviewRows.value = dataRows.value
    .map((cells) => buildRow(cells, mapping.value, props.roster, refIso.value))
    .filter((r) => r.firstName.trim() || r.lastName.trim())
}

function onMappingChange(field: keyof CastColumnMap, e: Event) {
  const v = (e.target as HTMLSelectElement).value
  mapping.value[field] = v === '' ? null : Number(v)
  rebuild()
}

async function runAi() {
  if (productionId.value == null) return
  aiLoading.value = true
  try {
    const { data } = await api.post<AiCastResult>('/castimport/ai', {
      productionId: productionId.value,
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
    reviewRows.value = data.rows.map((r): CastReviewRow => ({
      firstName: r.firstName,
      lastName: r.lastName,
      gender: parseGender(r.gender),
      dateOfBirth: r.dateOfBirth || (r.age ? ageToDob(r.age, refIso.value) : null),
      age: r.age,
      notes: r.notes || null,
      castGroup: r.castGroup || null,
      guardianName: r.guardianName || null,
      guardianEmail: r.guardianEmail || null,
      guardianPhone: r.guardianPhone || null,
      existingPerformerId: matchPerformer(`${r.firstName} ${r.lastName}`, props.roster),
      include: true,
    }))
    toast.success(`AI read ${data.rows.length} performer${data.rows.length === 1 ? '' : 's'} — review below.`)
  } catch {
    toast.error('AI cleanup failed — is the server running?')
  } finally {
    aiLoading.value = false
  }
}

const statusOf = (r: CastReviewRow) => rowStatus(r, castPerformerIds.value)
const readyCount = computed(() => reviewRows.value.filter((r) => ['new', 'existing'].includes(statusOf(r))).length)
const newCount = computed(() => reviewRows.value.filter((r) => statusOf(r) === 'new').length)
const alreadyCount = computed(() => reviewRows.value.filter((r) => statusOf(r) === 'already-in-cast').length)

const toImport = computed(() =>
  reviewRows.value
    .filter((r) => r.include && (r.firstName.trim() || r.lastName.trim()))
    .map((r): CastImportRow => ({
      existingPerformerId: r.existingPerformerId,
      firstName: r.firstName.trim(),
      lastName: r.lastName.trim(),
      gender: r.gender,
      dateOfBirth: r.dateOfBirth,
      notes: r.notes,
      castGroup: r.castGroup,
      guardianName: r.guardianName,
      guardianEmail: r.guardianEmail,
      guardianPhone: r.guardianPhone,
    })),
)

async function doImport() {
  if (productionId.value == null || toImport.value.length === 0) return
  importing.value = true
  try {
    const { data } = await api.post<CastImportSummary>('/castimport', {
      productionId: productionId.value,
      rows: toImport.value,
    })
    const bits = [
      data.performersCreated > 0 ? `${data.performersCreated} new` : '',
      data.addedToCast > 0 ? `${data.addedToCast} added to cast` : '',
      data.alreadyInCast > 0 ? `${data.alreadyInCast} already in cast` : '',
    ].filter(Boolean)
    toast.success(`Imported: ${bits.join(' · ') || 'nothing to do'}`)
    emit('imported')
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
  mapping.value = emptyMap()
}

const FIELDS: { key: keyof CastColumnMap; label: string }[] = [
  { key: 'firstName', label: 'First name' },
  { key: 'lastName', label: 'Last name' },
  { key: 'fullName', label: 'Full name' },
  { key: 'gender', label: 'Gender' },
  { key: 'dob', label: 'Birth date' },
  { key: 'age', label: 'Age' },
  { key: 'castGroup', label: 'Cast group' },
  { key: 'guardianName', label: 'Guardian' },
  { key: 'guardianEmail', label: 'Guardian email' },
  { key: 'guardianPhone', label: 'Guardian phone' },
  { key: 'notes', label: 'Notes' },
]
const GENDERS: Gender[] = ['Male', 'Female', 'NonBinary']
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) { reset(); emit('close') } }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[88vh] w-[calc(100%-2rem)] max-w-3xl -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">Import cast (CSV or Excel)</DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          <template v-if="productionId != null">
            Upload a roster — any column order. We'll match names to existing performers, link
            guardians, and add everyone to
            <span class="font-medium text-foreground">{{ scope.selectedProduction?.title }}</span>.
          </template>
          <template v-else>
            Pick a season and production first — the cast is imported into the selected show.
          </template>
        </DialogDescription>

        <template v-if="productionId != null">
          <input
            type="file"
            accept=".csv,.txt,.xlsx,.xls,text/csv"
            class="mt-4 block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border file:border-border file:bg-secondary file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-foreground hover:file:bg-accent"
            @change="onFile"
          />

          <!-- Mapping -->
          <div v-if="rawRows.length > 0" class="mt-4 space-y-3">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Match columns</span>
              <div class="flex items-center gap-3">
                <button
                  class="flex items-center gap-1.5 rounded-md border border-primary/40 bg-primary/5 px-2.5 py-1 text-xs font-medium text-primary hover:bg-primary/10 disabled:opacity-50"
                  title="Let AI read a messy roster and fill the review table"
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
            <div class="grid gap-2 sm:grid-cols-4">
              <label v-for="f in FIELDS" :key="f.key" class="space-y-1">
                <span class="text-xs font-medium">{{ f.label }}</span>
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

          <!-- Review -->
          <div v-if="reviewRows.length > 0" class="mt-4 space-y-3">
            <p class="flex flex-wrap gap-x-4 gap-y-1 text-sm">
              <span class="font-medium text-emerald-600 dark:text-emerald-400">{{ readyCount }} to import</span>
              <span v-if="newCount > 0" class="text-muted-foreground">{{ newCount }} new performer{{ newCount === 1 ? '' : 's' }}</span>
              <span v-if="alreadyCount > 0" class="font-medium text-muted-foreground">{{ alreadyCount }} already in cast</span>
              <span class="text-xs text-muted-foreground">{{ fileName }}</span>
            </p>

            <ul class="max-h-72 space-y-1.5 overflow-y-auto rounded-md border border-border p-2">
              <li
                v-for="(r, i) in reviewRows"
                :key="i"
                class="rounded-md border border-border/60 p-2 text-xs"
                :class="r.include ? '' : 'opacity-50'"
              >
                <div class="flex flex-wrap items-center gap-2">
                  <input type="checkbox" v-model="r.include" class="shrink-0" :aria-label="`Include ${r.firstName}`" />
                  <UserPlus v-if="statusOf(r) === 'new'" class="h-4 w-4 shrink-0 text-primary" title="New performer" />
                  <CheckCircle2 v-else-if="statusOf(r) === 'existing'" class="h-4 w-4 shrink-0 text-emerald-500" title="Existing performer" />
                  <Copy v-else-if="statusOf(r) === 'already-in-cast'" class="h-4 w-4 shrink-0 text-muted-foreground" title="Already in this cast" />
                  <AlertTriangle v-else class="h-4 w-4 shrink-0 text-amber-500" />
                  <input v-model="r.firstName" placeholder="First" class="w-20 rounded-md border border-border bg-background px-1.5 py-1 focus:outline-none" />
                  <input v-model="r.lastName" placeholder="Last" class="w-24 rounded-md border border-border bg-background px-1.5 py-1 focus:outline-none" />
                  <span class="text-muted-foreground">→</span>
                  <select v-model="r.existingPerformerId" class="min-w-0 flex-1 rounded-md border border-border bg-background px-1.5 py-1 focus:outline-none">
                    <option :value="null">➕ New performer</option>
                    <option v-for="o in rosterOptions" :key="o.id" :value="o.id">{{ o.name }}</option>
                  </select>
                  <select v-model="r.gender" class="rounded-md border border-border bg-background px-1.5 py-1 focus:outline-none">
                    <option :value="null">—</option>
                    <option v-for="g in GENDERS" :key="g" :value="g">{{ g === 'NonBinary' ? 'NB' : g[0] }}</option>
                  </select>
                </div>
                <div class="mt-1.5 flex flex-wrap items-center gap-2 pl-6 text-muted-foreground">
                  <input type="date" v-model="r.dateOfBirth" class="rounded border border-border bg-background px-1 py-0.5 focus:outline-none" />
                  <span v-if="r.age && !r.dateOfBirth">age {{ r.age }}</span>
                  <input v-model="r.castGroup" placeholder="Group" class="w-20 rounded border border-border bg-background px-1.5 py-0.5 focus:outline-none" />
                  <input v-model="r.guardianEmail" placeholder="Guardian email" class="w-44 rounded border border-border bg-background px-1.5 py-0.5 focus:outline-none" />
                  <input v-model="r.notes" placeholder="Notes" class="min-w-24 flex-1 rounded border border-border bg-background px-1.5 py-0.5 focus:outline-none" />
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
        </template>

        <div v-else class="mt-5 flex justify-end">
          <button class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent" @click="emit('close')">Close</button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
