<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { CalendarOff, Contact, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { ageOn } from '@/lib/age'
import { conflictLabel } from '@/lib/conflicts'
import ColorDot from '@/components/ColorDot.vue'
import type { CastGroup, CastMembership, Conflict, Guardian, LevelGroup, PerformerGuardian } from '@/types'

const props = defineProps<{
  member: CastMembership | null
  castGroup: CastGroup | null
  levelGroup: LevelGroup | null
  showDate?: string | null
}>()
const emit = defineEmits<{ close: [] }>()

const performerNotes = ref('')
const showNotes = ref('')
const conflicts = ref<Conflict[]>([])
const guardians = ref<Guardian[]>([])

watch(
  () => props.member,
  async (m) => {
    performerNotes.value = m?.performer?.notes ?? ''
    showNotes.value = m?.notes ?? ''
    conflicts.value = []
    guardians.value = []
    if (m) {
      const [conflictRows, links, allGuardians] = await Promise.all([
        api
          .get<Conflict[]>(`/conflicts?productionId=${m.productionId}&performerId=${m.performerId}`)
          .then((r) => r.data)
          .catch(() => [] as Conflict[]),
        api
          .get<PerformerGuardian[]>(`/performerguardians?performerId=${m.performerId}`)
          .then((r) => r.data)
          .catch(() => [] as PerformerGuardian[]),
        api.get<Guardian[]>('/guardians').then((r) => r.data).catch(() => [] as Guardian[]),
      ])
      conflicts.value = conflictRows
      const ids = new Set(links.map((l) => l.guardianId))
      guardians.value = allGuardians.filter((g) => ids.has(g.id))
    }
  },
  { immediate: true },
)

const age = computed(() => ageOn(props.member?.performer?.dateOfBirth, props.showDate))

const genderLabel = computed(() => {
  const g = props.member?.performer?.gender
  return g === 'Male' ? 'M' : g === 'Female' ? 'F' : g === 'NonBinary' ? 'NB' : null
})

async function savePerformerNotes() {
  const p = props.member?.performer
  if (!p) return
  p.notes = performerNotes.value.trim() || null
  await api.put(`/performers/${p.id}`, p)
  toast.success('Saved')
}

async function saveShowNotes() {
  const m = props.member
  if (!m) return
  m.notes = showNotes.value.trim() || null
  await api.put(`/castmemberships/${m.id}`, { ...m, performer: undefined })
  toast.success('Saved')
}
</script>

<template>
  <div v-if="member" class="fixed inset-0 z-50">
    <div class="absolute inset-0 bg-black/30" @click="emit('close')" />
    <aside
      class="absolute inset-y-0 right-0 flex w-full max-w-sm flex-col overflow-y-auto border-l border-border bg-background p-5 shadow-xl"
    >
      <div class="flex items-start justify-between gap-3">
        <div>
          <h2 class="font-display text-xl font-bold">
            {{ member.performer?.firstName }} {{ member.performer?.lastName }}
          </h2>
          <p class="mt-0.5 text-sm text-muted-foreground">
            <span v-if="age !== null">{{ age }} yrs</span>
            <span v-if="age !== null && genderLabel"> · </span>
            <span v-if="genderLabel">{{ genderLabel }}</span>
          </p>
        </div>
        <button
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground"
          aria-label="Close"
          @click="emit('close')"
        >
          <X class="h-4 w-4" />
        </button>
      </div>

      <div class="mt-2 flex flex-wrap gap-1.5">
        <span
          v-if="castGroup"
          class="flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs"
        >
          <ColorDot :color="castGroup.color" size="sm" /> {{ castGroup.name }}
        </span>
        <span
          v-if="levelGroup"
          class="flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs"
        >
          <ColorDot :color="levelGroup.color" size="sm" /> {{ levelGroup.name }}
        </span>
      </div>

      <div v-if="guardians.length > 0" class="mt-4">
        <h3 class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <Contact class="h-3.5 w-3.5" /> Guardians
        </h3>
        <ul class="mt-1.5 space-y-1">
          <li v-for="g in guardians" :key="g.id" class="text-sm">
            <span class="font-medium">{{ g.name }}</span>
            <span class="ml-1.5 text-xs text-muted-foreground">
              <a v-if="g.email" :href="`mailto:${g.email}`" class="text-primary hover:underline">{{ g.email }}</a>
              <span v-if="g.email && g.phone"> · </span>
              <span v-if="g.phone">{{ g.phone }}</span>
            </span>
          </li>
        </ul>
      </div>

      <label class="mt-5 block space-y-1">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Performer notes <span class="font-normal normal-case">(all shows)</span>
        </span>
        <textarea
          v-model="performerNotes"
          rows="3"
          placeholder="Constant things — allergies, sizes, family info…"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          @change="savePerformerNotes"
        />
      </label>

      <label class="mt-4 block space-y-1">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Show notes <span class="font-normal normal-case">(this production)</span>
        </span>
        <textarea
          v-model="showNotes"
          rows="3"
          placeholder="Just for this show — costume notes, understudy plans…"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          @change="saveShowNotes"
        />
      </label>

      <div class="mt-5">
        <h3 class="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <CalendarOff class="h-3.5 w-3.5" /> Conflicts this show
        </h3>
        <p v-if="conflicts.length === 0" class="mt-1.5 text-sm text-muted-foreground">None recorded.</p>
        <ul v-else class="mt-1.5 space-y-1">
          <li v-for="c in conflicts" :key="c.id" class="flex items-center gap-2 text-sm">
            <span
              class="rounded px-1.5 py-0.5 text-xs font-medium"
              :class="c.type === 'Weekly' ? 'bg-accent text-accent-foreground' : 'bg-muted text-muted-foreground'"
            >
              {{ conflictLabel(c) }}
            </span>
            <span class="truncate text-xs text-muted-foreground">{{ c.reason }}</span>
          </li>
        </ul>
        <RouterLink
          to="/conflicts"
          class="mt-2 inline-block text-xs font-medium text-primary hover:underline"
          @click="emit('close')"
        >
          Manage conflicts →
        </RouterLink>
      </div>
    </aside>
  </div>
</template>
