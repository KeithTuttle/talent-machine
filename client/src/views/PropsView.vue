<script setup lang="ts">
// Prop catalog + per-scene cues. Each prop is editable inline (name, qty, status,
// storage) and expands to manage where it's used: the scenes it appears in, with
// preset location, who brings it on, and where it strikes. "Props PDF" is the
// backstage sheet (master pull list + run-of-show).
import { computed, onMounted, ref, watch } from 'vue'
import {
  ChevronDown,
  ChevronRight,
  FileDown,
  Package,
  Plus,
  Trash2,
  X,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import { PROP_STATUSES, type Act, type Prop, type PropAssignment, type PropStatus, type Scene } from '@/types'

const scope = useScopeStore()

const props = ref<Prop[]>([])
const assignments = ref<PropAssignment[]>([])
const scenes = ref<Scene[]>([])
const acts = ref<Act[]>([])
const expanded = ref<Set<number>>(new Set())

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
    props.value = []
    assignments.value = []
    scenes.value = []
    acts.value = []
    return
  }
  ;[props.value, assignments.value, scenes.value, acts.value] = await Promise.all([
    safeGet<Prop>(`/props?productionId=${pid}`),
    safeGet<PropAssignment>(`/propassignments?productionId=${pid}`),
    safeGet<Scene>(`/scenes?productionId=${pid}`),
    safeGet<Act>(`/acts?productionId=${pid}`),
  ])
}
onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Lookups -----------------------------------------------------------------

const actName = computed(() => new Map(acts.value.map((a) => [a.id, a.name])))
const scenesSorted = computed(() =>
  [...scenes.value].sort(
    (a, b) =>
      (a.actId ?? 999) - (b.actId ?? 999) || a.orderIndex - b.orderIndex || a.id - b.id,
  ),
)
const sceneLabel = (s: Scene) => {
  const act = s.actId != null ? actName.value.get(s.actId) : null
  const setting = s.setting ? ` (${s.setting})` : ''
  return `${act ? act + ' · ' : ''}${s.name}${setting}`
}
const sceneLabelById = (id: number) => {
  const s = scenes.value.find((x) => x.id === id)
  return s ? sceneLabel(s) : `Scene #${id}`
}

const assignmentsOf = (propId: number) => assignments.value.filter((a) => a.propId === propId)

const statusCounts = computed(() => ({
  Needed: props.value.filter((p) => p.status === 'Needed').length,
  Sourced: props.value.filter((p) => p.status === 'Sourced').length,
  Ready: props.value.filter((p) => p.status === 'Ready').length,
}))

const statusClass = (s: PropStatus) =>
  s === 'Ready'
    ? 'text-green-700 dark:text-green-400'
    : s === 'Sourced'
      ? 'text-blue-700 dark:text-blue-400'
      : 'text-amber-700 dark:text-amber-500'

// --- Props (catalog) ---------------------------------------------------------

async function addProp() {
  const pid = scope.selectedProductionId
  if (pid === null) return
  const { data } = await api.post<Prop>('/props', {
    id: 0,
    productionId: pid,
    name: 'New prop',
    quantity: 1,
    status: 'Needed',
    orderIndex: props.value.length + 1,
  })
  props.value.push(data)
  expanded.value.add(data.id)
}

async function saveProp(p: Prop) {
  await api.put(`/props/${p.id}`, p).catch(() => {})
}

async function deleteProp(p: Prop) {
  const ok = await confirm({
    title: `Delete “${p.name}”?`,
    message: 'Removes it from the catalog and every scene it was in.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/props/${p.id}`)
  props.value = props.value.filter((x) => x.id !== p.id)
  assignments.value = assignments.value.filter((a) => a.propId !== p.id)
}

function toggle(id: number) {
  if (expanded.value.has(id)) expanded.value.delete(id)
  else expanded.value.add(id)
  // trigger reactivity on the Set
  expanded.value = new Set(expanded.value)
}

// --- Assignments (per scene) -------------------------------------------------

async function addAssignment(propId: number, e: Event) {
  const sceneId = Number((e.target as HTMLSelectElement).value)
  ;(e.target as HTMLSelectElement).value = ''
  if (!sceneId) return
  const { data } = await api.post<PropAssignment>('/propassignments', {
    id: 0,
    propId,
    sceneId,
  })
  assignments.value.push(data)
}

async function saveAssignment(a: PropAssignment) {
  await api.put(`/propassignments/${a.id}`, a).catch(() => {})
}

async function deleteAssignment(a: PropAssignment) {
  assignments.value = assignments.value.filter((x) => x.id !== a.id)
  await api.delete(`/propassignments/${a.id}`).catch(() => {})
}

// --- PDF ---------------------------------------------------------------------

async function downloadPdf() {
  try {
    const { data } = await api.get(`/props/pdf?productionId=${scope.selectedProductionId}`, {
      responseType: 'blob',
    })
    const url = URL.createObjectURL(data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'props.pdf'
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    toast.error("Couldn't generate the props PDF — is the server running?")
  }
}
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <Package class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production to track its props.
      </p>
    </div>

    <div v-else class="mx-auto max-w-3xl space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">Props</h1>
          <p class="text-sm text-muted-foreground">
            {{ scope.selectedProduction?.title }} — the catalog and where each prop is used.
          </p>
        </div>
        <div class="flex gap-2">
          <button
            class="flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="downloadPdf"
          >
            <FileDown class="h-4 w-4" /> PDF
          </button>
          <button
            class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90"
            @click="addProp"
          >
            <Plus class="h-4 w-4" /> Add prop
          </button>
        </div>
      </div>

      <!-- Status summary -->
      <div v-if="props.length" class="flex flex-wrap gap-2 text-sm">
        <span class="rounded-full bg-amber-100 px-3 py-1 font-medium text-amber-700 dark:bg-amber-950 dark:text-amber-400">
          {{ statusCounts.Needed }} needed
        </span>
        <span class="rounded-full bg-blue-100 px-3 py-1 font-medium text-blue-700 dark:bg-blue-950 dark:text-blue-400">
          {{ statusCounts.Sourced }} sourced
        </span>
        <span class="rounded-full bg-green-100 px-3 py-1 font-medium text-green-700 dark:bg-green-950 dark:text-green-400">
          {{ statusCounts.Ready }} ready
        </span>
      </div>

      <p
        v-if="props.length === 0"
        class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground"
      >
        No props yet. Add one, then tell us which scenes it's used in.
      </p>

      <!-- Catalog -->
      <div v-for="p in props" :key="p.id" class="rounded-lg border border-border">
        <div class="flex flex-wrap items-center gap-2 p-3">
          <button class="rounded p-1 text-muted-foreground hover:text-foreground" :aria-label="expanded.has(p.id) ? 'Collapse' : 'Expand'" @click="toggle(p.id)">
            <ChevronDown v-if="expanded.has(p.id)" class="h-4 w-4" />
            <ChevronRight v-else class="h-4 w-4" />
          </button>
          <input
            v-model="p.name"
            class="min-w-0 flex-1 rounded-md border border-transparent bg-transparent px-1 font-medium hover:border-border focus:border-border focus:outline-none"
            @change="saveProp(p)"
          />
          <label class="flex items-center gap-1 text-xs text-muted-foreground">
            Qty
            <input
              v-model.number="p.quantity"
              type="number"
              min="1"
              class="w-14 rounded-md border border-border bg-background px-1.5 py-1 text-sm focus:outline-none"
              @change="saveProp(p)"
            />
          </label>
          <select
            class="rounded-md border border-border bg-background px-1.5 py-1 text-sm font-medium focus:outline-none"
            :class="statusClass(p.status)"
            :value="p.status"
            @change="p.status = ($event.target as HTMLSelectElement).value as PropStatus; saveProp(p)"
          >
            <option v-for="s in PROP_STATUSES" :key="s" :value="s">{{ s }}</option>
          </select>
          <span class="flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
            {{ assignmentsOf(p.id).length }} {{ assignmentsOf(p.id).length === 1 ? 'scene' : 'scenes' }}
          </span>
          <button class="rounded p-1 text-muted-foreground hover:text-destructive" :aria-label="`Delete ${p.name}`" @click="deleteProp(p)">
            <Trash2 class="h-4 w-4" />
          </button>
        </div>

        <!-- Expanded: details + scene cues -->
        <div v-if="expanded.has(p.id)" class="space-y-3 border-t border-border p-3">
          <div class="grid gap-2 sm:grid-cols-2">
            <input v-model="p.description" placeholder="Description" class="rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none" @change="saveProp(p)" />
            <input v-model="p.storageLocation" placeholder="Stored where?" class="rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none" @change="saveProp(p)" />
          </div>
          <input v-model="p.notes" placeholder="Notes (e.g. hero prop — handle with care)" class="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none" @change="saveProp(p)" />

          <div class="space-y-2">
            <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Used in scenes</span>
            <p v-if="assignmentsOf(p.id).length === 0" class="text-xs italic text-muted-foreground">
              Not placed in a scene yet.
            </p>
            <div v-for="a in assignmentsOf(p.id)" :key="a.id" class="space-y-1.5 rounded-md border border-border bg-muted/30 p-2.5">
              <div class="flex items-center gap-2">
                <span class="flex-1 truncate text-sm font-medium">{{ sceneLabelById(a.sceneId) }}</span>
                <button class="rounded p-1 text-muted-foreground hover:text-destructive" aria-label="Remove from scene" @click="deleteAssignment(a)">
                  <X class="h-3.5 w-3.5" />
                </button>
              </div>
              <div class="grid gap-1.5 sm:grid-cols-2">
                <input :value="a.presetLocation ?? ''" placeholder="Preset location" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="a.presetLocation = ($event.target as HTMLInputElement).value || null; saveAssignment(a)" />
                <input :value="a.handler ?? ''" placeholder="Brought on by" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="a.handler = ($event.target as HTMLInputElement).value || null; saveAssignment(a)" />
                <input :value="a.strikeTo ?? ''" placeholder="Strike to" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="a.strikeTo = ($event.target as HTMLInputElement).value || null; saveAssignment(a)" />
                <input :value="a.notes ?? ''" placeholder="Notes" class="rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none" @change="a.notes = ($event.target as HTMLInputElement).value || null; saveAssignment(a)" />
              </div>
            </div>
            <select
              v-if="scenes.length"
              class="rounded-md border border-dashed border-border bg-transparent px-2 py-1 text-xs text-muted-foreground focus:outline-none"
              :value="''"
              @change="addAssignment(p.id, $event)"
            >
              <option value="">+ Add to a scene…</option>
              <option v-for="s in scenesSorted" :key="s.id" :value="s.id">{{ sceneLabel(s) }}</option>
            </select>
            <p v-else class="text-xs italic text-muted-foreground">
              Create scenes in Script first to place this prop.
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
