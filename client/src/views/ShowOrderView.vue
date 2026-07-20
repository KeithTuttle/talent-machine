<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ArrowDown, ArrowUp, FileDown, GripVertical, ListOrdered, Plus, Trash2, Users } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import type { Act, MusicalNumber, NumberCast } from '@/types'

const scope = useScopeStore()

const acts = ref<Act[]>([])
const numbers = ref<MusicalNumber[]>([])
const numberCasts = ref<NumberCast[]>([])

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
    acts.value = []
    numbers.value = []
    numberCasts.value = []
    return
  }
  ;[acts.value, numbers.value, numberCasts.value] = await Promise.all([
    safeGet<Act>(`/acts?productionId=${pid}`),
    safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
    safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
  ])
}
onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Derived -----------------------------------------------------------------

const orderedNumbers = (actId: number | null) =>
  numbers.value
    .filter((n) => (n.actId ?? null) === actId)
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)

/** Acts in order with their numbers + the running number each act starts at. */
const programme = computed(() => {
  const sections: { act: Act | null; entries: MusicalNumber[]; startNumber: number }[] = []
  let running = 0
  for (const act of [...acts.value].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)) {
    const entries = orderedNumbers(act.id)
    sections.push({ act, entries, startNumber: running })
    running += entries.length
  }
  const unassigned = orderedNumbers(null)
  if (unassigned.length > 0) sections.push({ act: null, entries: unassigned, startNumber: running })
  return sections
})

const castCount = (numberId: number) =>
  numberCasts.value.filter((c) => c.musicalNumberId === numberId).length

// --- Acts --------------------------------------------------------------------

async function addAct() {
  const pid = scope.selectedProductionId
  if (pid === null) return
  const { data } = await api.post<Act>('/acts', {
    id: 0,
    productionId: pid,
    name: `Act ${acts.value.length + 1}`,
    orderIndex: acts.value.length + 1,
  })
  acts.value.push(data)
}

async function saveAct(act: Act) {
  await api.put(`/acts/${act.id}`, act).catch(() => {})
}

async function moveAct(act: Act, delta: -1 | 1) {
  const sorted = [...acts.value].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
  const i = sorted.indexOf(act)
  const j = i + delta
  if (j < 0 || j >= sorted.length) return
  ;[sorted[i].orderIndex, sorted[j].orderIndex] = [sorted[j].orderIndex, sorted[i].orderIndex]
  // Normalize so future inserts stay stable.
  sorted.sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
  sorted.forEach((a, idx) => (a.orderIndex = idx + 1))
  await Promise.all(sorted.map((a) => api.put(`/acts/${a.id}`, a).catch(() => {})))
}

async function deleteAct(act: Act) {
  const ok = await confirm({
    title: `Delete “${act.name}”?`,
    message: 'Its numbers move to Unassigned — nothing is lost.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/acts/${act.id}`)
  acts.value = acts.value.filter((a) => a.id !== act.id)
  for (const n of numbers.value) if (n.actId === act.id) n.actId = null
}

// --- Numbers -----------------------------------------------------------------

async function moveNumber(number: MusicalNumber, delta: -1 | 1) {
  const siblings = orderedNumbers(number.actId ?? null)
  const i = siblings.indexOf(number)
  const j = i + delta
  if (j < 0 || j >= siblings.length) return
  const reordered = [...siblings]
  ;[reordered[i], reordered[j]] = [reordered[j], reordered[i]]
  // Optimistic local OrderIndex update, then persist this act's order.
  reordered.forEach((n, idx) => (n.orderIndex = idx + 1))
  await api
    .put('/numbers/reorder', {
      productionId: scope.selectedProductionId,
      orderedIds: reordered.map((n) => n.id),
    })
    .catch(() => {})
}

async function setAct(number: MusicalNumber, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  number.actId = raw === '' ? null : Number(raw)
  // Append to the end of the destination act.
  number.orderIndex = orderedNumbers(number.actId ?? null).length
  await api.put(`/numbers/${number.id}`, number).catch(() => {})
}

// --- Drag & drop -------------------------------------------------------------

const draggingId = ref<number | null>(null)
/** Insertion marker: act (null = Unassigned) + index within it. */
const dropTarget = ref<{ actId: number | null; index: number } | null>(null)

function onDragStart(number: MusicalNumber, e: DragEvent) {
  draggingId.value = number.id
  e.dataTransfer?.setData('text/plain', String(number.id))
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onDragOverRow(actId: number | null, index: number, e: DragEvent) {
  if (draggingId.value === null) return
  e.preventDefault()
  // Drop above or below the hovered row depending on cursor half.
  const el = e.currentTarget as HTMLElement
  const below = e.clientY > el.getBoundingClientRect().top + el.offsetHeight / 2
  dropTarget.value = { actId, index: index + (below ? 1 : 0) }
}

function onDragOverSection(actId: number | null, e: DragEvent) {
  if (draggingId.value === null) return
  e.preventDefault()
  // Hovering the section (not a row) → drop at the end.
  dropTarget.value ??= { actId, index: orderedNumbers(actId).length }
  if (dropTarget.value.actId !== actId)
    dropTarget.value = { actId, index: orderedNumbers(actId).length }
}

async function onDrop() {
  const target = dropTarget.value
  const dragged = numbers.value.find((n) => n.id === draggingId.value)
  draggingId.value = null
  dropTarget.value = null
  if (!target || !dragged) return

  const fromActId = dragged.actId ?? null
  const list = orderedNumbers(target.actId).filter((n) => n.id !== dragged.id)
  let index = target.index
  // Removing the dragged row from its own act shifts later indexes down one.
  if (fromActId === target.actId) {
    const oldIndex = orderedNumbers(fromActId).findIndex((n) => n.id === dragged.id)
    if (oldIndex !== -1 && oldIndex < index) index--
  }
  index = Math.max(0, Math.min(index, list.length))
  list.splice(index, 0, dragged)
  list.forEach((n, i) => (n.orderIndex = i + 1))

  if (fromActId !== target.actId) {
    dragged.actId = target.actId
    await api.put(`/numbers/${dragged.id}`, dragged).catch(() => {})
  }
  await api
    .put('/numbers/reorder', {
      productionId: scope.selectedProductionId,
      orderedIds: list.map((n) => n.id),
    })
    .catch(() => {})
}

function onDragEnd() {
  draggingId.value = null
  dropTarget.value = null
}

const isDropAt = (actId: number | null, index: number) =>
  dropTarget.value?.actId === actId && dropTarget.value.index === index

// --- PDF ---------------------------------------------------------------------

async function downloadPdf() {
  try {
    const { data } = await api.get(
      `/numbers/showorder/pdf?productionId=${scope.selectedProductionId}`,
      { responseType: 'blob' },
    )
    const url = URL.createObjectURL(data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'show-order.pdf'
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    toast.error("Couldn't generate the PDF — is the server running?")
  }
}
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <ListOrdered class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production, then arrange its running order here.
      </p>
    </div>

    <div v-else class="mx-auto max-w-3xl space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">Show Order</h1>
          <p class="text-sm text-muted-foreground">
            {{ scope.selectedProduction?.title }} — the running order, act by act.
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
            @click="addAct"
          >
            <Plus class="h-4 w-4" /> Add act
          </button>
        </div>
      </div>

      <p v-if="acts.length === 0 && numbers.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        Add numbers in the Planner, then create acts here and put them in order.
      </p>

      <div v-for="(section, si) in programme" :key="section.act?.id ?? 'unassigned'" class="rounded-lg border border-border">
        <!-- Act header -->
        <div class="flex items-center gap-2 border-b border-border px-4 py-2.5">
          <template v-if="section.act">
            <input
              v-model="section.act.name"
              class="font-display w-40 rounded-md border border-transparent bg-transparent px-1 text-base font-semibold hover:border-border focus:border-border focus:outline-none"
              @change="saveAct(section.act)"
            />
            <span class="flex-1" />
            <button
              class="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              :disabled="si === 0"
              aria-label="Move act up"
              @click="moveAct(section.act, -1)"
            >
              <ArrowUp class="h-4 w-4" />
            </button>
            <button
              class="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              :disabled="si >= acts.length - 1"
              aria-label="Move act down"
              @click="moveAct(section.act, 1)"
            >
              <ArrowDown class="h-4 w-4" />
            </button>
            <button
              class="rounded p-1 text-muted-foreground hover:text-destructive"
              :aria-label="`Delete ${section.act.name}`"
              @click="deleteAct(section.act)"
            >
              <Trash2 class="h-4 w-4" />
            </button>
          </template>
          <span v-else class="font-display text-base font-semibold text-muted-foreground">Unassigned</span>
        </div>

        <p
          v-if="section.entries.length === 0"
          class="px-4 py-3 text-xs italic text-muted-foreground"
          :class="isDropAt(section.act?.id ?? null, 0) ? 'bg-accent/50' : ''"
          @dragover="onDragOverSection(section.act?.id ?? null, $event)"
          @drop.prevent="onDrop"
        >
          No numbers yet — drag some here, or use each number's act selector.
        </p>
        <ul
          v-else
          class="divide-y divide-border"
          @dragover="onDragOverSection(section.act?.id ?? null, $event)"
          @drop.prevent="onDrop"
        >
          <li
            v-for="(number, i) in section.entries"
            :key="number.id"
            class="flex cursor-grab items-center gap-2 px-4 py-2 text-sm active:cursor-grabbing"
            :class="[
              draggingId === number.id ? 'opacity-40' : '',
              isDropAt(section.act?.id ?? null, i) ? 'border-t-2 !border-t-primary' : '',
              isDropAt(section.act?.id ?? null, i + 1) && i === section.entries.length - 1 ? 'border-b-2 !border-b-primary' : '',
            ]"
            draggable="true"
            title="Drag to reorder or move between acts"
            @dragstart="onDragStart(number, $event)"
            @dragover.stop="onDragOverRow(section.act?.id ?? null, i, $event)"
            @drop.prevent="onDrop"
            @dragend="onDragEnd"
          >
            <GripVertical class="h-4 w-4 shrink-0 text-muted-foreground/60" aria-hidden="true" />
            <span class="w-7 shrink-0 text-xs tabular-nums text-muted-foreground">
              {{ section.act ? section.startNumber + i + 1 + '.' : '—' }}
            </span>
            <span class="flex-1 truncate font-medium">{{ number.title }}</span>
            <span class="flex items-center gap-1 text-xs text-muted-foreground">
              <Users class="h-3.5 w-3.5" /> {{ castCount(number.id) }}
            </span>
            <select
              class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
              :value="number.actId ?? ''"
              aria-label="Act"
              @change="setAct(number, $event)"
            >
              <option value="">Unassigned</option>
              <option v-for="a in acts" :key="a.id" :value="a.id">{{ a.name }}</option>
            </select>
            <span class="flex flex-col">
              <ArrowUp
                class="h-3.5 w-3.5 cursor-pointer text-muted-foreground hover:text-foreground"
                role="button"
                aria-label="Move up"
                @click="moveNumber(number, -1)"
              />
              <ArrowDown
                class="h-3.5 w-3.5 cursor-pointer text-muted-foreground hover:text-foreground"
                role="button"
                aria-label="Move down"
                @click="moveNumber(number, 1)"
              />
            </span>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
