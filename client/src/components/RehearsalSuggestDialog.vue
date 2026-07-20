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
import { AlertTriangle, Loader2, Sparkles, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { conflictedAttendees, REHEARSAL_TYPES } from '@/lib/rehearsals'
import type {
  Conflict,
  MusicalNumber,
  NumberCast,
  Rehearsal,
  SuggestResponse,
  SuggestedSlot,
} from '@/types'

const props = defineProps<{
  open: boolean
  productionId: number
  numbers: MusicalNumber[]
  numberCasts: NumberCast[]
  conflicts: Conflict[]
  defaultFrom: string
  defaultTo: string
  performerName: (id: number) => string
}>()
const emit = defineEmits<{ close: []; saved: [slots: Rehearsal[]]; unconfigured: [] }>()

const prompt = ref('')
const fromDate = ref(props.defaultFrom)
const toDate = ref(props.defaultTo)
const loading = ref(false)
const failed = ref(false)
const draft = ref<SuggestedSlot[]>([])
const saving = ref(false)

async function suggest() {
  loading.value = true
  failed.value = false
  fromDate.value ||= props.defaultFrom
  toDate.value ||= props.defaultTo
  try {
    const { data } = await api.post<SuggestResponse>('/rehearsals/suggest', {
      productionId: props.productionId,
      prompt: prompt.value.trim() || null,
      fromDate: fromDate.value,
      toDate: toDate.value,
    })
    if (!data.configured) {
      // The ONLY signal that hides the feature — never a transient failure.
      emit('unconfigured')
      emit('close')
      return
    }
    if (!data.ok) {
      failed.value = true
      return
    }
    draft.value = data.slots
  } catch {
    failed.value = true
  } finally {
    loading.value = false
  }
}

/** Conflicted kid names for a draft row (live warning while editing). */
function warningsFor(slot: SuggestedSlot): string[] {
  const attendees =
    slot.musicalNumberId != null
      ? props.numberCasts.filter((c) => c.musicalNumberId === slot.musicalNumberId).map((c) => c.performerId)
      : []
  return conflictedAttendees(attendees, slot.date, props.conflicts).map(props.performerName)
}

const canSave = computed(() => draft.value.length > 0 && !saving.value)

async function save() {
  if (!canSave.value) return
  saving.value = true
  try {
    const { data } = await api.post<Rehearsal[]>('/rehearsals/bulk', {
      productionId: props.productionId,
      rehearsals: draft.value.map((s) => ({
        id: 0,
        productionId: props.productionId,
        date: s.date,
        startTime: s.startTime,
        endTime: s.endTime,
        type: s.type,
        musicalNumberId: s.musicalNumberId ?? null,
        notes: s.notes ?? null,
      })),
    })
    emit('saved', data)
    draft.value = []
    prompt.value = ''
    emit('close')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) emit('close') }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[85vh] w-[calc(100%-2rem)] max-w-2xl -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="flex items-center gap-2 text-base font-semibold">
          <Sparkles class="h-4 w-4 text-primary" /> Suggest a rehearsal schedule
        </DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          Describe what you want to work on and when — the AI drafts a schedule around
          the kids' conflicts. Everything stays editable before you add it.
        </DialogDescription>

        <div class="mt-4 flex flex-wrap items-center gap-3 text-sm">
          <label class="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
            From
            <input v-model="fromDate" type="date" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring" />
          </label>
          <label class="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
            To
            <input v-model="toDate" type="date" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring" />
          </label>
        </div>

        <textarea
          v-model="prompt"
          rows="2"
          placeholder='e.g. "This Saturday 8am–12pm, work Tomorrow and the finale, music first then dance"'
          class="mt-2 block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
        <div class="mt-2 flex justify-end">
          <button
            class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
            :disabled="loading"
            @click="suggest"
          >
            <Loader2 v-if="loading" class="h-4 w-4 animate-spin" />
            <Sparkles v-else class="h-4 w-4" />
            {{ draft.length > 0 ? 'Try again' : 'Suggest' }}
          </button>
        </div>

        <p v-if="failed" class="mt-3 flex items-center gap-1.5 text-sm text-destructive">
          <AlertTriangle class="h-4 w-4" /> The suggestion didn't come back — try again in a moment.
        </p>

        <!-- Editable draft -->
        <div v-if="draft.length > 0" class="mt-4 space-y-2">
          <div
            v-for="(slot, i) in draft"
            :key="i"
            class="space-y-1.5 rounded-md border border-border p-2.5"
          >
            <div class="flex flex-wrap items-center gap-2">
              <input v-model="slot.date" type="date" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" />
              <input v-model="slot.startTime" type="time" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" />
              <span class="text-xs text-muted-foreground">–</span>
              <input v-model="slot.endTime" type="time" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none" />
              <select v-model="slot.type" class="rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none">
                <option v-for="t in REHEARSAL_TYPES" :key="t" :value="t">{{ t }}</option>
              </select>
              <select v-model="slot.musicalNumberId" class="min-w-32 flex-1 rounded-md border border-border bg-background px-1.5 py-1 text-xs focus:outline-none">
                <option :value="null">General</option>
                <option v-for="n in numbers" :key="n.id" :value="n.id">{{ n.title }}</option>
              </select>
              <button
                class="rounded p-1 text-muted-foreground hover:text-destructive"
                aria-label="Remove slot"
                @click="draft.splice(i, 1)"
              >
                <Trash2 class="h-3.5 w-3.5" />
              </button>
            </div>
            <input
              v-model="slot.notes"
              placeholder="Notes"
              class="block w-full rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none"
            />
            <p v-if="warningsFor(slot).length > 0" class="flex items-center gap-1 text-xs text-orange-600 dark:text-orange-400">
              <AlertTriangle class="h-3 w-3" /> Conflict: {{ warningsFor(slot).join(', ') }}
            </p>
          </div>

          <div class="flex justify-end gap-2 pt-1">
            <button class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent" @click="emit('close')">
              Cancel
            </button>
            <button
              class="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="!canSave"
              @click="save"
            >
              {{ saving ? 'Adding…' : `Add ${draft.length} slot${draft.length === 1 ? '' : 's'}` }}
            </button>
          </div>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
