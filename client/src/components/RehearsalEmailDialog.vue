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
import { Mail } from 'lucide-vue-next'
import { formatTime } from '@/lib/rehearsals'
import type { CastMembership, Guardian, MusicalNumber, PerformerGuardian, Rehearsal } from '@/types'

const props = defineProps<{
  open: boolean
  productionTitle: string
  weekLabel: string
  /** This week's slots, in order. */
  slots: Rehearsal[]
  numbers: MusicalNumber[]
  cast: CastMembership[]
  guardians: Guardian[]
  links: PerformerGuardian[]
  /** performerIds scheduled this week (resolved attendees across slots). */
  scheduledPerformerIds: number[]
  onDownloadPdf: () => void
}>()
const emit = defineEmits<{ close: [] }>()

const audience = ref<'all' | 'scheduled'>('all')

const relevantPerformerIds = computed(() =>
  audience.value === 'all'
    ? props.cast.map((m) => m.performerId)
    : props.scheduledPerformerIds,
)

const recipients = computed(() => {
  const guardianIds = new Set(
    props.links
      .filter((l) => relevantPerformerIds.value.includes(l.performerId))
      .map((l) => l.guardianId),
  )
  const emails = props.guardians
    .filter((g) => guardianIds.has(g.id) && g.email)
    .map((g) => g.email!.trim())
  return [...new Set(emails)]
})

/** Kids in the audience with no guardian email on file. */
const missing = computed(() =>
  props.cast
    .filter((m) => relevantPerformerIds.value.includes(m.performerId))
    .filter((m) => {
      const gids = props.links.filter((l) => l.performerId === m.performerId).map((l) => l.guardianId)
      return !props.guardians.some((g) => gids.includes(g.id) && g.email)
    })
    .map((m) => `${m.performer?.firstName ?? ''} ${m.performer?.lastName ?? ''}`.trim()),
)

const numberTitle = (id?: number | null) =>
  props.numbers.find((n) => n.id === id)?.title ?? 'General'

const body = computed(() => {
  const lines: string[] = [
    `Rehearsal schedule — ${props.productionTitle}`,
    props.weekLabel,
    '',
  ]
  let currentDate = ''
  for (const s of props.slots) {
    if (s.date !== currentDate) {
      currentDate = s.date
      const d = new Date(`${s.date}T00:00:00`)
      lines.push(d.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' }))
    }
    lines.push(
      `  ${formatTime(s.startTime)}–${formatTime(s.endTime)}  ${numberTitle(s.musicalNumberId)} (${s.type})` +
        (s.notes ? ` — ${s.notes}` : ''),
    )
  }
  lines.push('', 'The full schedule PDF is attached.', '', 'See you there!')
  return lines.join('\n')
})

const mailtoHref = computed(() => {
  const subject = `Rehearsal schedule: ${props.productionTitle} — ${props.weekLabel}`
  return `mailto:?bcc=${encodeURIComponent(recipients.value.join(','))}&subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body.value)}`
})

function openEmail() {
  // Download the PDF alongside so it can be attached manually (mailto can't attach).
  props.onDownloadPdf()
  window.location.href = mailtoHref.value
}
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) emit('close') }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[85vh] w-[calc(100%-2rem)] max-w-md -translate-y-1/2 -translate-x-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">Email the schedule</DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          Opens your own email app with recipients and a formatted schedule pre-filled
          (as BCC, so families don't see each other's addresses). The PDF downloads
          alongside — attach it before sending. Nothing sends automatically.
        </DialogDescription>

        <div class="mt-4 flex rounded-md border border-border text-sm">
          <button
            v-for="a in ([['all', 'All guardians in the show'], ['scheduled', 'Scheduled kids only']] as const)"
            :key="a[0]"
            class="flex-1 px-2 py-1.5 font-medium"
            :class="audience === a[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="audience = a[0]"
          >
            {{ a[1] }}
          </button>
        </div>

        <p class="mt-3 text-sm">
          <span class="font-medium">{{ recipients.length }}</span> guardian email{{ recipients.length === 1 ? '' : 's' }}
        </p>
        <p v-if="recipients.length > 0" class="mt-1 max-h-24 overflow-y-auto break-all rounded-md border border-border p-2 text-xs text-muted-foreground">
          {{ recipients.join(', ') }}
        </p>
        <p v-if="missing.length > 0" class="mt-2 text-xs text-destructive">
          No guardian email on file: {{ missing.join(', ') }}
        </p>

        <div class="mt-4 flex justify-end gap-2">
          <button
            class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
            :disabled="recipients.length === 0"
            @click="openEmail"
          >
            <Mail class="h-4 w-4" /> Open email + download PDF
          </button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
