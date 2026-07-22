<script setup lang="ts">
// Script breakdown: Act → Scene → nested Numbers. Per scene you set which
// characters are present, see "who's needed" (their performers + the performers
// cast in the scene's numbers), and schedule a blocking rehearsal for exactly
// that set of kids.
import { computed, onMounted, ref, watch } from 'vue'
import {
  ArrowDown,
  ArrowUp,
  CalendarPlus,
  Clapperboard,
  Music,
  Plus,
  Trash2,
  Users,
  X,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import type {
  Act,
  MusicalNumber,
  NumberCast,
  Performer,
  Role,
  Scene,
  SceneCharacter,
} from '@/types'

const scope = useScopeStore()

const acts = ref<Act[]>([])
const scenes = ref<Scene[]>([])
const numbers = ref<MusicalNumber[]>([])
const roles = ref<Role[]>([])
const performers = ref<Performer[]>([])
const numberCasts = ref<NumberCast[]>([])
const sceneChars = ref<SceneCharacter[]>([])

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
    scenes.value = []
    numbers.value = []
    roles.value = []
    performers.value = []
    numberCasts.value = []
    sceneChars.value = []
    return
  }
  ;[
    acts.value,
    scenes.value,
    numbers.value,
    roles.value,
    performers.value,
    numberCasts.value,
    sceneChars.value,
  ] = await Promise.all([
    safeGet<Act>(`/acts?productionId=${pid}`),
    safeGet<Scene>(`/scenes?productionId=${pid}`),
    safeGet<MusicalNumber>(`/numbers?productionId=${pid}`),
    safeGet<Role>(`/roles?productionId=${pid}`),
    safeGet<Performer>(`/performers`),
    safeGet<NumberCast>(`/numbercast?productionId=${pid}`),
    safeGet<SceneCharacter>(`/scenecharacters?productionId=${pid}`),
  ])
}
onMounted(loadAll)
watch(() => scope.selectedProductionId, loadAll)

// --- Lookups -----------------------------------------------------------------

const roleById = computed(() => new Map(roles.value.map((r) => [r.id, r])))
const performerById = computed(() => new Map(performers.value.map((p) => [p.id, p])))
const performerName = (id: number) => {
  const p = performerById.value.get(id)
  return p ? `${p.firstName} ${p.lastName}`.trim() : `#${id}`
}

const actsSorted = computed(() =>
  [...acts.value].sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id),
)
const scenesInAct = (actId: number | null) =>
  scenes.value
    .filter((s) => (s.actId ?? null) === actId)
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
const numbersInScene = (sceneId: number) =>
  numbers.value
    .filter((n) => n.sceneId === sceneId)
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)
/** Numbers placed in an act but not nested under any of its scenes. */
const unscenedInAct = (actId: number | null) =>
  numbers.value
    .filter((n) => (n.actId ?? null) === actId && n.sceneId == null)
    .sort((a, b) => a.orderIndex - b.orderIndex || a.id - b.id)

/** Sections = each act in order, then an "Unassigned" bucket (actId null). */
const sections = computed(() => {
  const out = actsSorted.value.map((act) => ({ act: act as Act | null }))
  const hasUnassigned =
    scenesInAct(null).length > 0 || unscenedInAct(null).length > 0
  if (hasUnassigned) out.push({ act: null })
  return out
})

const presentIn = (sceneId: number) =>
  sceneChars.value.filter((sc) => sc.sceneId === sceneId)
const charactersPresent = (sceneId: number) =>
  presentIn(sceneId)
    .map((sc) => roleById.value.get(sc.roleId))
    .filter((r): r is Role => !!r)
    .sort((a, b) => a.orderIndex - b.orderIndex || a.name.localeCompare(b.name))
const rolesAbsent = (sceneId: number) => {
  const present = new Set(presentIn(sceneId).map((sc) => sc.roleId))
  return roles.value.filter((r) => !present.has(r.id))
}

const castCount = (numberId: number) =>
  numberCasts.value.filter((c) => c.musicalNumberId === numberId).length

/** Performer ids needed in a scene: its characters' performers ∪ its numbers' cast. */
const whoNeededIds = (sceneId: number) => {
  const ids = new Set<number>()
  for (const r of charactersPresent(sceneId)) if (r.performerId != null) ids.add(r.performerId)
  for (const n of numbersInScene(sceneId))
    for (const c of numberCasts.value.filter((x) => x.musicalNumberId === n.id)) ids.add(c.performerId)
  return [...ids]
}
const whoNeeded = (sceneId: number) =>
  whoNeededIds(sceneId)
    .map((id) => ({ id, name: performerName(id) }))
    .sort((a, b) => a.name.localeCompare(b.name))

// --- Scenes ------------------------------------------------------------------

async function addScene(actId: number | null) {
  const pid = scope.selectedProductionId
  if (pid === null) return
  const { data } = await api.post<Scene>('/scenes', {
    id: 0,
    productionId: pid,
    actId,
    name: `Scene ${scenesInAct(actId).length + 1}`,
    setting: null,
    notes: null,
    orderIndex: scenesInAct(actId).length + 1,
  })
  scenes.value.push(data)
}

async function saveScene(scene: Scene) {
  await api.put(`/scenes/${scene.id}`, scene).catch(() => {})
}

async function moveScene(scene: Scene, delta: -1 | 1) {
  const sorted = scenesInAct(scene.actId ?? null)
  const i = sorted.indexOf(scene)
  const j = i + delta
  if (j < 0 || j >= sorted.length) return
  ;[sorted[i].orderIndex, sorted[j].orderIndex] = [sorted[j].orderIndex, sorted[i].orderIndex]
  sorted.forEach((s, idx) => (s.orderIndex = idx + 1))
  await Promise.all(sorted.map((s) => api.put(`/scenes/${s.id}`, s).catch(() => {})))
}

async function deleteScene(scene: Scene) {
  const ok = await confirm({
    title: `Delete “${scene.name}”?`,
    message: 'Its numbers un-nest (they keep their act) — nothing is lost.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/scenes/${scene.id}`)
  scenes.value = scenes.value.filter((s) => s.id !== scene.id)
  for (const n of numbers.value) if (n.sceneId === scene.id) n.sceneId = null
  sceneChars.value = sceneChars.value.filter((sc) => sc.sceneId !== scene.id)
}

async function setSceneAct(scene: Scene, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  scene.actId = raw === '' ? null : Number(raw)
  scene.orderIndex = scenesInAct(scene.actId ?? null).length
  await saveScene(scene)
}

// --- Number nesting ----------------------------------------------------------

async function setNumberScene(number: MusicalNumber, e: Event) {
  const raw = (e.target as HTMLSelectElement).value
  if (raw === '') {
    number.sceneId = null
  } else {
    const scene = scenes.value.find((s) => s.id === Number(raw))
    if (!scene) return
    number.sceneId = scene.id
    // Nesting a number pulls it into the scene's act so the book stays coherent.
    number.actId = scene.actId ?? null
  }
  await api.put(`/numbers/${number.id}`, number).catch(() => {})
}

// --- Character presence ------------------------------------------------------

async function addPresence(sceneId: number, e: Event) {
  const roleId = Number((e.target as HTMLSelectElement).value)
  ;(e.target as HTMLSelectElement).value = ''
  if (!roleId) return
  sceneChars.value.push({ sceneId, roleId })
  await api.post('/scenecharacters', { sceneId, roleId }).catch(() => {})
}

async function removePresence(sceneId: number, roleId: number) {
  sceneChars.value = sceneChars.value.filter((sc) => !(sc.sceneId === sceneId && sc.roleId === roleId))
  await api.delete(`/scenecharacters?sceneId=${sceneId}&roleId=${roleId}`).catch(() => {})
}

// --- Schedule blocking -------------------------------------------------------

async function scheduleBlocking(scene: Scene) {
  const pid = scope.selectedProductionId
  if (pid === null) return
  const ids = whoNeededIds(scene.id)
  if (ids.length === 0) {
    toast.error('No one is needed yet — add characters or nest numbers first.')
    return
  }
  const today = new Date().toISOString().slice(0, 10)
  try {
    const { data: rehearsal } = await api.post<{ id: number }>('/rehearsals', {
      id: 0,
      productionId: pid,
      date: today,
      startTime: '16:00:00',
      endTime: '18:00:00',
      type: 'Blocking',
      musicalNumberId: null,
      notes: `Blocking — ${scene.name}${scene.setting ? ` (${scene.setting})` : ''}`,
    })
    await Promise.all(
      ids.map((performerId) =>
        api.post('/rehearsalattendees', {
          rehearsalId: rehearsal.id,
          performerId,
          isExcluded: false,
        }),
      ),
    )
    toast.success(`Blocking rehearsal created with ${ids.length} in the room — set the date in Rehearsals.`)
  } catch {
    toast.error("Couldn't create the rehearsal — is the server running?")
  }
}
</script>

<template>
  <div class="p-6">
    <div
      v-if="scope.selectedProductionId === null"
      class="mx-auto mt-16 max-w-md rounded-lg border border-dashed border-border p-8 text-center"
    >
      <Clapperboard class="mx-auto h-8 w-8 text-primary" />
      <h1 class="font-display mt-3 text-xl font-bold">No production selected</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Pick a season and production, then break its book into scenes here.
      </p>
    </div>

    <div v-else class="mx-auto max-w-3xl space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="font-display text-2xl font-bold">Script</h1>
          <p class="text-sm text-muted-foreground">
            {{ scope.selectedProduction?.title }} — scenes, characters present, and who's needed.
          </p>
        </div>
      </div>

      <p
        v-if="acts.length === 0 && scenes.length === 0"
        class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground"
      >
        Create acts in Show Order, then add scenes here and drop your numbers into them.
      </p>

      <div
        v-for="section in sections"
        :key="section.act?.id ?? 'unassigned'"
        class="space-y-3 rounded-lg border border-border p-4"
      >
        <!-- Act header -->
        <div class="flex items-center justify-between gap-2">
          <h2 class="font-display text-lg font-semibold">
            {{ section.act?.name ?? 'Unassigned' }}
          </h2>
          <button
            class="flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs font-medium hover:bg-accent"
            @click="addScene(section.act?.id ?? null)"
          >
            <Plus class="h-3.5 w-3.5" /> Add scene
          </button>
        </div>

        <p
          v-if="scenesInAct(section.act?.id ?? null).length === 0 && unscenedInAct(section.act?.id ?? null).length === 0"
          class="text-xs italic text-muted-foreground"
        >
          No scenes yet.
        </p>

        <!-- Scenes -->
        <div
          v-for="(scene, si) in scenesInAct(section.act?.id ?? null)"
          :key="scene.id"
          class="rounded-md border border-border bg-muted/30"
        >
          <!-- Scene header -->
          <div class="flex flex-wrap items-center gap-2 border-b border-border px-3 py-2">
            <input
              v-model="scene.name"
              class="w-28 rounded-md border border-transparent bg-transparent px-1 text-sm font-semibold hover:border-border focus:border-border focus:outline-none"
              @change="saveScene(scene)"
            />
            <input
              v-model="scene.setting"
              placeholder="Setting…"
              class="min-w-0 flex-1 rounded-md border border-transparent bg-transparent px-1 text-sm text-muted-foreground hover:border-border focus:border-border focus:outline-none"
              @change="saveScene(scene)"
            />
            <select
              class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
              :value="scene.actId ?? ''"
              aria-label="Act"
              @change="setSceneAct(scene, $event)"
            >
              <option value="">Unassigned</option>
              <option v-for="a in actsSorted" :key="a.id" :value="a.id">{{ a.name }}</option>
            </select>
            <button
              class="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              :disabled="si === 0"
              aria-label="Move scene up"
              @click="moveScene(scene, -1)"
            >
              <ArrowUp class="h-4 w-4" />
            </button>
            <button
              class="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              :disabled="si >= scenesInAct(section.act?.id ?? null).length - 1"
              aria-label="Move scene down"
              @click="moveScene(scene, 1)"
            >
              <ArrowDown class="h-4 w-4" />
            </button>
            <button
              class="rounded p-1 text-muted-foreground hover:text-destructive"
              :aria-label="`Delete ${scene.name}`"
              @click="deleteScene(scene)"
            >
              <Trash2 class="h-4 w-4" />
            </button>
          </div>

          <div class="space-y-3 p-3">
            <!-- Characters present -->
            <div class="space-y-1.5">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Characters present</span>
              <div class="flex flex-wrap items-center gap-1">
                <span
                  v-for="r in charactersPresent(scene.id)"
                  :key="r.id"
                  class="flex items-center gap-1 rounded-full border border-border bg-background px-2 py-0.5 text-xs"
                >
                  {{ r.name }}
                  <span v-if="r.performerId" class="text-muted-foreground">· {{ performerName(r.performerId) }}</span>
                  <button
                    class="text-muted-foreground hover:text-destructive"
                    :aria-label="`Remove ${r.name}`"
                    @click="removePresence(scene.id, r.id)"
                  >
                    <X class="h-3 w-3" />
                  </button>
                </span>
                <select
                  v-if="rolesAbsent(scene.id).length"
                  class="rounded-md border border-dashed border-border bg-transparent px-2 py-0.5 text-xs text-muted-foreground focus:outline-none"
                  :value="''"
                  @change="addPresence(scene.id, $event)"
                >
                  <option value="">+ Add character</option>
                  <option v-for="r in rolesAbsent(scene.id)" :key="r.id" :value="r.id">{{ r.name }}</option>
                </select>
                <span v-if="charactersPresent(scene.id).length === 0 && !roles.length" class="text-xs italic text-muted-foreground">
                  Add characters on the Planner's cast list first.
                </span>
              </div>
            </div>

            <!-- Nested numbers -->
            <div class="space-y-1">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Numbers</span>
              <p v-if="numbersInScene(scene.id).length === 0" class="text-xs italic text-muted-foreground">
                No songs nested here yet.
              </p>
              <div
                v-for="n in numbersInScene(scene.id)"
                :key="n.id"
                class="flex items-center gap-2 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm"
              >
                <Music class="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                <span class="flex-1 truncate">{{ n.title }}</span>
                <span class="flex items-center gap-1 text-xs text-muted-foreground">
                  <Users class="h-3.5 w-3.5" /> {{ castCount(n.id) }}
                </span>
                <select
                  class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
                  :value="n.sceneId ?? ''"
                  aria-label="Scene"
                  @change="setNumberScene(n, $event)"
                >
                  <option value="">— none —</option>
                  <option v-for="s in scenesInAct(section.act?.id ?? null)" :key="s.id" :value="s.id">{{ s.name }}</option>
                </select>
              </div>
            </div>

            <!-- Who's needed + schedule -->
            <div class="flex flex-wrap items-center gap-2 border-t border-border pt-2.5">
              <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Who's needed</span>
              <span
                v-for="w in whoNeeded(scene.id)"
                :key="w.id"
                class="rounded-full bg-accent px-2 py-0.5 text-xs"
              >
                {{ w.name }}
              </span>
              <span v-if="whoNeeded(scene.id).length === 0" class="text-xs italic text-muted-foreground">
                — nobody yet —
              </span>
              <button
                class="ml-auto flex items-center gap-1 rounded-md bg-primary px-2.5 py-1 text-xs font-medium text-primary-foreground hover:opacity-90 disabled:opacity-40"
                :disabled="whoNeeded(scene.id).length === 0"
                @click="scheduleBlocking(scene)"
              >
                <CalendarPlus class="h-3.5 w-3.5" /> Schedule blocking
              </button>
            </div>
          </div>
        </div>

        <!-- Un-nested numbers in this act -->
        <div v-if="unscenedInAct(section.act?.id ?? null).length" class="space-y-1">
          <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Not in a scene
          </span>
          <div
            v-for="n in unscenedInAct(section.act?.id ?? null)"
            :key="n.id"
            class="flex items-center gap-2 rounded-md border border-dashed border-border px-2.5 py-1.5 text-sm"
          >
            <Music class="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span class="flex-1 truncate">{{ n.title }}</span>
            <select
              v-if="scenesInAct(section.act?.id ?? null).length"
              class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none"
              :value="''"
              aria-label="Nest in scene"
              @change="setNumberScene(n, $event)"
            >
              <option value="">Nest in scene…</option>
              <option v-for="s in scenesInAct(section.act?.id ?? null)" :key="s.id" :value="s.id">{{ s.name }}</option>
            </select>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
