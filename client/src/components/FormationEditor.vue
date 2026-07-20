<script setup lang="ts">
// Drag-and-drop SVG stage editor with quick layouts and AI suggestions, ported
// from dance-manager. Each number keeps a list of named formations (its history).
// Coordinates are 0–100 stage percentages, auto-saved on every change.
import { computed, ref, watch } from 'vue'
import { Loader2, Plus, Sparkles, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import type { Formation, Performer } from '@/types'

const props = withDefaults(
  defineProps<{ numberId: number | null; dancers: Performer[]; emptyHint?: string }>(),
  { emptyHint: 'No performers to arrange yet.' },
)

// Stage geometry (viewBox units).
const VB_W = 320, VB_H = 210
const GX0 = 16, GY0 = 28
const GW = VB_W - GX0 * 2 // 288
const GH = 150
const GY1 = GY0 + GH // 178
const NODE_R = 9

const GENDER_FILL: Record<string, string> = { Male: '#2563eb', Female: '#db2777', NonBinary: '#a855f7' }
const genderFill = (g?: string | null) => GENDER_FILL[g ?? ''] ?? '#94a3b8'
const genderText = (g?: string | null) => (GENDER_FILL[g ?? ''] ? '#ffffff' : '#0f172a')
const initials = (p: Performer) =>
  `${p.firstName?.[0] ?? ''}${p.lastName?.[0] ?? ''}`.toUpperCase() || '?'

type Coord = { x: number; y: number }
type CoordMap = Record<string, Coord>

const formations = ref<Formation[]>([])
const selectedFormationId = ref<number | null>(null)
const coordMap = ref<CoordMap>({})
const draggingId = ref<number | null>(null)

const svgRef = ref<SVGSVGElement | null>(null)

const selectedFormation = computed(() => formations.value.find((f) => f.id === selectedFormationId.value) ?? null)

function parseCoords(json: string): CoordMap {
  try {
    return JSON.parse(json || '{}') as CoordMap
  } catch {
    return {}
  }
}

async function loadFormations() {
  formations.value = []
  coordMap.value = {}
  selectedFormationId.value = null
  if (props.numberId == null) return
  formations.value = await api
    .get<Formation[]>(`/formations?numberId=${props.numberId}`)
    .then((r) => r.data)
    .catch(() => [])
  if (formations.value.length > 0) selectFormation(formations.value[0].id)
}
watch(() => props.numberId, loadFormations, { immediate: true })

function selectFormation(id: number) {
  selectedFormationId.value = id
  coordMap.value = parseCoords(formations.value.find((f) => f.id === id)?.coordinates ?? '{}')
}

const nodes = computed(() =>
  props.dancers.map((p, i) => {
    const c = coordMap.value[String(p.id)]
    return { dancer: p, x: c?.x ?? 50, y: c?.y ?? 10 + ((i * 12) % 80), placed: !!c }
  }),
)

// --- Drag (pointer events) ---------------------------------------------------

function onPointerDown(performerId: number, e: PointerEvent) {
  if (!selectedFormation.value) return
  draggingId.value = performerId
  ;(e.target as Element).setPointerCapture?.(e.pointerId)
  if (!coordMap.value[String(performerId)]) coordMap.value[String(performerId)] = { x: 50, y: 50 }
}

function onPointerMove(e: PointerEvent) {
  if (draggingId.value == null || !svgRef.value) return
  const rect = svgRef.value.getBoundingClientRect()
  const vbX = ((e.clientX - rect.left) / rect.width) * VB_W
  const vbY = ((e.clientY - rect.top) / rect.height) * VB_H
  const x = Math.max(0, Math.min(100, ((vbX - GX0) / GW) * 100))
  const y = Math.max(0, Math.min(100, ((vbY - GY0) / GH) * 100))
  coordMap.value[String(draggingId.value)] = { x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10 }
}

function onPointerUp() {
  if (draggingId.value == null) return
  draggingId.value = null
  saveFormation()
}

// --- Formation CRUD ----------------------------------------------------------

async function addFormation() {
  if (props.numberId == null) return
  const { data } = await api.post<Formation>('/formations', {
    id: 0,
    musicalNumberId: props.numberId,
    formationName: `Formation ${formations.value.length + 1}`,
    orderIndex: formations.value.length,
    coordinates: '{}',
  })
  formations.value.push(data)
  selectFormation(data.id)
}

async function saveFormation() {
  const f = selectedFormation.value
  if (!f) return
  f.coordinates = JSON.stringify(coordMap.value)
  await api.put(`/formations/${f.id}`, f).catch(() => {})
}

async function saveFormationName(f: Formation) {
  await api.put(`/formations/${f.id}`, f).catch(() => {})
  toast.success('Saved')
}

async function deleteFormation(f: Formation) {
  if (!(await confirm({ title: `Delete “${f.formationName}”?`, destructive: true, confirmText: 'Delete' }))) return
  await api.delete(`/formations/${f.id}`)
  formations.value = formations.value.filter((x) => x.id !== f.id)
  if (selectedFormationId.value === f.id) {
    if (formations.value[0]) selectFormation(formations.value[0].id)
    else { selectedFormationId.value = null; coordMap.value = {} }
  }
}

// --- Quick layouts (deterministic, offline) ---------------------------------

function centeredRow(count: number): number[] {
  if (count <= 1) return [50]
  const left = 15
  const step = (85 - left) / (count - 1)
  return Array.from({ length: count }, (_, i) => left + i * step)
}

function applyLayout(coords: CoordMap) {
  coordMap.value = coords
  saveFormation()
}

function layoutRows(rows: number) {
  const ids = props.dancers.map((d) => d.id)
  const perRow = Math.ceil(ids.length / rows)
  const map: CoordMap = {}
  ids.forEach((id, i) => {
    const r = Math.floor(i / perRow)
    const inRow = ids.slice(r * perRow, (r + 1) * perRow).length
    const xs = centeredRow(inRow)
    const y = rows === 1 ? 55 : 25 + r * (55 / (rows - 1))
    map[String(id)] = { x: xs[i - r * perRow], y }
  })
  applyLayout(map)
}

function layoutStaggered() {
  const ids = props.dancers.map((d) => d.id)
  const map: CoordMap = {}
  const perRow = Math.ceil(ids.length / 2)
  ids.forEach((id, i) => {
    const r = Math.floor(i / perRow)
    const inRow = ids.slice(r * perRow, (r + 1) * perRow).length
    const xs = centeredRow(inRow)
    const offset = r === 1 && xs.length > 1 ? Math.min((xs[1] - xs[0]) / 2, 92) : 0
    map[String(id)] = { x: Math.min(92, xs[i - r * perRow] + offset), y: 30 + r * 30 }
  })
  applyLayout(map)
}

function layoutV() {
  const ids = props.dancers.map((d) => d.id)
  const map: CoordMap = {}
  const half = Math.max(1, Math.ceil((ids.length - 1) / 2))
  ids.forEach((id, i) => {
    if (i === 0) { map[String(id)] = { x: 50, y: 22 }; return }
    const rank = Math.ceil(i / 2)
    const goLeft = i % 2 === 1
    const t = rank / half
    map[String(id)] = { x: 50 + (goLeft ? -33 : 33) * t, y: 22 + 55 * t }
  })
  applyLayout(map)
}

// --- AI suggest --------------------------------------------------------------

const aiConfigured = ref(true)
const aiLoading = ref(false)
const aiDescription = ref('')

async function probeAiConfig() {
  try {
    const { data } = await api.post<{ configured: boolean }>('/formations/suggest', { dancers: [], description: null })
    aiConfigured.value = data.configured !== false
  } catch {
    /* keep shown; transient */
  }
}
probeAiConfig()

async function suggestFormation() {
  if (!selectedFormation.value || props.dancers.length === 0) return
  aiLoading.value = true
  try {
    const { data } = await api.post<{ configured: boolean; ok: boolean; coordinates: Record<number, Coord> }>(
      '/formations/suggest',
      {
        dancers: props.dancers.map((p) => ({ performerId: p.id, gender: p.gender, firstName: p.firstName })),
        description: aiDescription.value || null,
      },
    )
    if (data.configured === false) {
      aiConfigured.value = false
      return
    }
    if (!data.ok || !data.coordinates) {
      toast.error("The AI couldn't place the formation — try a quick layout instead.")
      return
    }
    const map: CoordMap = {}
    for (const [id, c] of Object.entries(data.coordinates)) map[id] = c
    applyLayout(map)
  } finally {
    aiLoading.value = false
  }
}

const rosterSummary = computed(() => {
  const boys = props.dancers.filter((d) => d.gender === 'Male').length
  const girls = props.dancers.filter((d) => d.gender === 'Female').length
  return `${props.dancers.length} performers${boys || girls ? ` · ${boys} boys, ${girls} girls` : ''}`
})
</script>

<template>
  <div class="space-y-3">
    <p v-if="dancers.length === 0" class="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
      {{ emptyHint }}
    </p>

    <template v-else>
      <!-- Formation list -->
      <div class="flex flex-wrap items-center gap-1.5">
        <button
          v-for="f in formations"
          :key="f.id"
          class="rounded-full border px-2.5 py-0.5 text-xs"
          :class="f.id === selectedFormationId ? 'border-primary bg-accent text-accent-foreground' : 'border-border hover:bg-accent/50'"
          @click="selectFormation(f.id)"
        >
          {{ f.formationName }}
        </button>
        <button class="flex items-center gap-1 rounded-full border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="addFormation">
          <Plus class="h-3 w-3" /> Add
        </button>
      </div>

      <template v-if="selectedFormation">
        <div class="flex items-center gap-2">
          <input
            v-model="selectedFormation.formationName"
            class="flex-1 rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveFormationName(selectedFormation)"
          />
          <button class="rounded p-1 text-muted-foreground hover:text-destructive" title="Delete formation" @click="deleteFormation(selectedFormation)">
            <Trash2 class="h-3.5 w-3.5" />
          </button>
        </div>

        <!-- Stage -->
        <svg
          ref="svgRef"
          :viewBox="`0 0 ${VB_W} ${VB_H}`"
          preserveAspectRatio="xMidYMid meet"
          class="aspect-[32/21] w-full touch-none select-none rounded-md border border-border bg-muted"
          @pointermove="onPointerMove"
          @pointerup="onPointerUp"
          @pointerleave="onPointerUp"
        >
          <text :x="VB_W / 2" y="16" text-anchor="middle" font-size="8" fill="#94a3b8">UPSTAGE</text>
          <rect :x="GX0" :y="GY0" :width="GW" :height="GH" fill="none" stroke="#cbd5e1" stroke-width="0.75" />
          <line v-for="k in 21" :key="`v${k}`" :x1="GX0 + ((k - 1) / 20) * GW" :y1="GY0" :x2="GX0 + ((k - 1) / 20) * GW" :y2="GY1" stroke="#e2e8f0" :stroke-width="k - 1 === 10 ? 0.7 : 0.3" />
          <line v-for="r in 5" :key="`h${r}`" :x1="GX0" :y1="GY0 + (r * GH) / 6" :x2="GX0 + GW" :y2="GY0 + (r * GH) / 6" stroke="#e2e8f0" stroke-width="0.3" />
          <text v-for="k in 21" :key="`n${k}`" :x="GX0 + ((k - 1) / 20) * GW" :y="GY1 + 13" text-anchor="middle" font-size="6" fill="#94a3b8">{{ Math.abs(k - 11) }}</text>
          <text :x="VB_W / 2" :y="GY1 + 30" text-anchor="middle" font-size="8" fill="#94a3b8">DOWNSTAGE</text>

          <g v-for="n in nodes" :key="n.dancer.id">
            <circle
              :cx="GX0 + (n.x / 100) * GW"
              :cy="GY0 + (n.y / 100) * GH"
              :r="NODE_R"
              :fill="genderFill(n.dancer.gender)"
              :stroke="draggingId === n.dancer.id ? '#0f172a' : 'rgba(15,23,42,0.25)'"
              :stroke-width="draggingId === n.dancer.id ? 1.75 : 0.75"
              class="cursor-grab active:cursor-grabbing"
              @pointerdown="onPointerDown(n.dancer.id, $event)"
            />
            <text
              :x="GX0 + (n.x / 100) * GW"
              :y="GY0 + (n.y / 100) * GH + 3"
              text-anchor="middle"
              font-size="7"
              :fill="genderText(n.dancer.gender)"
              class="pointer-events-none"
            >{{ initials(n.dancer) }}</text>
          </g>
        </svg>

        <!-- Quick layouts -->
        <div class="flex flex-wrap gap-1.5 text-xs">
          <button class="rounded-md border border-border px-2 py-1 hover:bg-accent" @click="layoutRows(1)">One line</button>
          <button class="rounded-md border border-border px-2 py-1 hover:bg-accent" @click="layoutRows(2)">Two rows</button>
          <button class="rounded-md border border-border px-2 py-1 hover:bg-accent" @click="layoutRows(3)">Three rows</button>
          <button class="rounded-md border border-border px-2 py-1 hover:bg-accent" @click="layoutStaggered">Staggered</button>
          <button class="rounded-md border border-border px-2 py-1 hover:bg-accent" @click="layoutV">V-shape</button>
        </div>

        <!-- AI suggest -->
        <div v-if="aiConfigured" class="space-y-1.5 rounded-md border border-border p-2.5">
          <div class="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
            <Sparkles class="h-3.5 w-3.5 text-primary" /> AI formation · {{ rosterSummary }}
          </div>
          <div class="flex gap-2">
            <input v-model="aiDescription" placeholder="Describe the look (e.g. two lines, leads front center)" class="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring" />
            <button class="flex items-center gap-1 rounded-md bg-primary px-2.5 py-1 text-xs font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50" :disabled="aiLoading" @click="suggestFormation">
              <Loader2 v-if="aiLoading" class="h-3.5 w-3.5 animate-spin" />
              <Sparkles v-else class="h-3.5 w-3.5" />
              Suggest
            </button>
          </div>
        </div>
      </template>

      <p v-else class="text-center text-xs text-muted-foreground">Add a formation to start arranging.</p>
    </template>
  </div>
</template>
