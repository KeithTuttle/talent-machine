<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ChevronDown, ChevronRight, Plus, Archive, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import ShowStaffEditor from '@/components/ShowStaffEditor.vue'
import type { Season, Production } from '@/types'

const scope = useScopeStore()

const seasons = ref<Season[]>([])
const productions = ref<Production[]>([])

const newSeasonYear = ref(new Date().getFullYear())
const newSeasonName = ref('')
const newProductionTitle = ref<Record<number, string>>({})

// Collapsed by default once there are several; first season starts open.
const openSeasons = ref<Set<number>>(new Set())
const openProductions = ref<Set<number>>(new Set())

async function load() {
  // Reads fail to empty states; the API interceptor handles write errors.
  seasons.value = await api.get<Season[]>('/seasons?includeArchived=true').then((r) => r.data).catch(() => [])
  productions.value = await api.get<Production[]>('/productions?includeArchived=true').then((r) => r.data).catch(() => [])
  if (seasons.value.length > 0) openSeasons.value = new Set([seasons.value[0].id])
}
onMounted(load)

function productionsOf(seasonId: number) {
  return productions.value.filter((p) => p.seasonId === seasonId)
}

function toggleSeason(id: number) {
  const s = openSeasons.value
  s.has(id) ? s.delete(id) : s.add(id)
  openSeasons.value = new Set(s)
}
function toggleProduction(id: number) {
  const s = openProductions.value
  s.has(id) ? s.delete(id) : s.add(id)
  openProductions.value = new Set(s)
}

async function addSeason() {
  const name = newSeasonName.value.trim() || String(newSeasonYear.value)
  const { data } = await api.post<Season>('/seasons', {
    id: 0,
    year: newSeasonYear.value,
    name,
    isArchived: false,
    createdAt: new Date().toISOString(),
  })
  seasons.value.unshift(data)
  openSeasons.value = new Set([...openSeasons.value, data.id])
  newSeasonName.value = ''
  toast.success(`Season “${data.name}” created`)
  scope.fetchAll().catch(() => {})
}

async function addProduction(season: Season) {
  const title = (newProductionTitle.value[season.id] ?? '').trim()
  if (!title) return
  const { data } = await api.post<Production>('/productions', {
    id: 0,
    seasonId: season.id,
    title,
    isArchived: false,
    createdAt: new Date().toISOString(),
  })
  productions.value.push(data)
  newProductionTitle.value[season.id] = ''
  toast.success(`“${data.title}” added to ${season.name}`)
  scope.fetchAll().catch(() => {})
}

async function setOpeningDate(production: Production, e: Event) {
  production.openingDate = (e.target as HTMLInputElement).value || null
  await api.put(`/productions/${production.id}`, production).catch(() => {})
  scope.fetchAll().catch(() => {})
}

async function toggleArchive(season: Season) {
  season.isArchived = !season.isArchived
  await api.put(`/seasons/${season.id}`, season).catch(() => {
    season.isArchived = !season.isArchived
  })
  scope.fetchAll().catch(() => {})
}

async function removeProduction(production: Production) {
  const ok = await confirm({
    title: `Delete “${production.title}”?`,
    message: 'Its numbers, cast, groups, roles, and staff will be deleted too.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/productions/${production.id}`)
  productions.value = productions.value.filter((p) => p.id !== production.id)
  scope.fetchAll().catch(() => {})
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6 p-6">
    <div>
      <h1 class="font-display text-2xl font-bold">Seasons &amp; Shows</h1>
      <p class="text-sm text-muted-foreground">
        A season is a session within a year (e.g. “Summer 2026”); each holds one or more shows.
      </p>
    </div>

    <!-- New season -->
    <form
      class="flex flex-wrap items-end gap-2 rounded-lg border border-border p-4"
      @submit.prevent="addSeason"
    >
      <label class="space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Year</span>
        <input
          v-model.number="newSeasonYear"
          type="number"
          class="block w-24 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <label class="flex-1 space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Season name</span>
        <input
          v-model="newSeasonName"
          :placeholder="`e.g. Summer ${newSeasonYear}`"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <button
        type="submit"
        class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
      >
        <Plus class="h-4 w-4" /> New season
      </button>
    </form>

    <!-- Season list -->
    <p v-if="seasons.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      No seasons yet — create your first above.
    </p>

    <div
      v-for="season in seasons"
      :key="season.id"
      class="rounded-lg border border-border"
      :class="season.isArchived ? 'opacity-60' : ''"
    >
      <div class="flex items-center gap-2 border-b border-border px-3 py-3">
        <button
          class="flex flex-1 items-center gap-2 text-left"
          @click="toggleSeason(season.id)"
        >
          <ChevronDown v-if="openSeasons.has(season.id)" class="h-4 w-4 shrink-0 text-muted-foreground" />
          <ChevronRight v-else class="h-4 w-4 shrink-0 text-muted-foreground" />
          <span class="font-display text-lg font-semibold">{{ season.name }}</span>
          <span class="text-xs text-muted-foreground">{{ season.year }}</span>
          <span class="text-xs text-muted-foreground">· {{ productionsOf(season.id).length }} show{{ productionsOf(season.id).length === 1 ? '' : 's' }}</span>
          <span
            v-if="season.isArchived"
            class="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground"
          >Archived</span>
        </button>
        <button
          class="flex items-center gap-1.5 rounded-md px-2 py-1.5 text-xs text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          @click="toggleArchive(season)"
        >
          <Archive class="h-3.5 w-3.5" />
          {{ season.isArchived ? 'Unarchive' : 'Archive' }}
        </button>
      </div>

      <div v-if="openSeasons.has(season.id)">
        <ul class="divide-y divide-border">
          <li v-for="production in productionsOf(season.id)" :key="production.id">
            <div class="flex items-center gap-2 px-3 py-2.5 text-sm">
              <button
                class="flex flex-1 items-center gap-2 text-left"
                @click="toggleProduction(production.id)"
              >
                <ChevronDown v-if="openProductions.has(production.id)" class="h-4 w-4 shrink-0 text-muted-foreground" />
                <ChevronRight v-else class="h-4 w-4 shrink-0 text-muted-foreground" />
                <span class="font-medium">{{ production.title }}</span>
                <span v-if="production.openingDate" class="text-xs text-muted-foreground">
                  opens {{ new Date(`${production.openingDate}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) }}
                </span>
              </button>
              <button
                class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
                :aria-label="`Delete ${production.title}`"
                title="Delete show"
                @click="removeProduction(production)"
              >
                <Trash2 class="h-4 w-4" />
              </button>
            </div>

            <!-- Expanded show detail -->
            <div v-if="openProductions.has(production.id)" class="space-y-4 border-t border-border bg-muted/20 px-4 py-3">
              <label class="flex items-center gap-2 text-sm">
                <span class="text-xs font-medium text-muted-foreground">Open date</span>
                <input
                  type="date"
                  class="rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  :value="production.openingDate ?? ''"
                  @change="setOpeningDate(production, $event)"
                />
              </label>
              <!-- Creative team (staff) editor mounts here in Phase 3 -->
              <ShowStaffEditor :production-id="production.id" />
            </div>
          </li>
        </ul>

        <form class="flex gap-2 p-3" @submit.prevent="addProduction(season)">
          <input
            v-model="newProductionTitle[season.id]"
            placeholder="New show title (e.g. Annie JR.)"
            class="flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          />
          <button
            type="submit"
            class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
          >
            Add show
          </button>
        </form>
      </div>
    </div>
  </div>
</template>
