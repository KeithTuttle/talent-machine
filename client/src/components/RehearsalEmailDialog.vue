<script setup lang="ts">
// Preview-and-send: shows the exact email (subject, body, recipients) the server
// will send, with the schedule PDF attached, then sends server-side on an
// explicit click. Nothing auto-sends.
import { ref, watch } from 'vue'
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from 'reka-ui'
import { AlertTriangle, Loader2, Mail } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'

const props = defineProps<{
  open: boolean
  productionId: number
  from: string
  to: string
}>()
const emit = defineEmits<{ close: [] }>()

interface Preview {
  configured: boolean
  subject: string
  body: string
  recipients: string[]
  missingEmail: string[]
  /** Performers this audience covers, and the full cast size (for context). */
  audienceCount: number
  castCount: number
}

const audience = ref<'all' | 'scheduled'>('all')
const preview = ref<Preview | null>(null)
const loading = ref(false)
const sending = ref(false)
// Editable copies — the user can tweak before sending; these are what actually go out.
const editSubject = ref('')
const editBody = ref('')

async function loadPreview() {
  loading.value = true
  preview.value = null
  try {
    const { data } = await api.post<Preview>('/rehearsals/email/preview', {
      productionId: props.productionId,
      from: props.from,
      to: props.to,
      audience: audience.value,
    })
    preview.value = data
    editSubject.value = data.subject
    editBody.value = data.body
  } catch {
    preview.value = null
  } finally {
    loading.value = false
  }
}

// Reload the preview whenever the dialog opens or the audience changes.
watch(
  () => [props.open, audience.value] as const,
  ([isOpen]) => {
    if (isOpen) loadPreview()
  },
  { immediate: true },
)

async function send() {
  if (!preview.value || preview.value.recipients.length === 0) return
  sending.value = true
  try {
    const { data } = await api.post<{ sent: boolean; count: number }>('/rehearsals/email/send', {
      productionId: props.productionId,
      from: props.from,
      to: props.to,
      audience: audience.value,
      subject: editSubject.value,
      body: editBody.value,
    })
    if (data.sent) {
      toast.success(`Schedule emailed to ${data.count} guardian${data.count === 1 ? '' : 's'}`)
      emit('close')
    } else {
      toast.error("Couldn't send — check that email is configured on the server.")
    }
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <DialogRoot :open="open" @update:open="(v: boolean) => { if (!v) emit('close') }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] max-h-[85vh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">Email the schedule</DialogTitle>
        <DialogDescription class="mt-1.5 text-sm text-muted-foreground">
          This is exactly what will be sent — edit the subject or message below if you
          like. The schedule PDF is attached, recipients are BCC'd so families don't see
          each other's addresses, and nothing sends until you click Send.
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

        <div v-if="loading" class="mt-6 flex justify-center text-muted-foreground">
          <Loader2 class="h-5 w-5 animate-spin" />
        </div>

        <div v-else-if="preview" class="mt-4 space-y-3">
          <p v-if="!preview.configured" class="flex items-center gap-1.5 rounded-md bg-muted p-2 text-xs text-muted-foreground">
            <AlertTriangle class="h-3.5 w-3.5" />
            Server email isn't configured yet, so Send is disabled. (Set the Gmail app password.)
          </p>

          <div class="rounded-md border border-border">
            <label class="flex items-center gap-2 border-b border-border px-3 py-2 text-sm">
              <span class="text-xs font-medium text-muted-foreground">Subject</span>
              <input
                v-model="editSubject"
                class="min-w-0 flex-1 rounded-sm bg-transparent focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </label>
            <textarea
              v-model="editBody"
              rows="12"
              class="block max-h-72 w-full resize-y overflow-y-auto whitespace-pre-wrap bg-transparent px-3 py-2 font-sans text-xs leading-relaxed text-foreground focus:outline-none"
              spellcheck="true"
            />
            <div class="border-t border-border px-3 py-2 text-xs text-muted-foreground">
              📎 rehearsals-{{ from }}.pdf
            </div>
          </div>

          <p class="text-sm text-muted-foreground">
            Calls <span class="font-medium text-foreground">{{ preview.audienceCount }}</span>
            of {{ preview.castCount }} performers →
            <span class="font-medium text-foreground">{{ preview.recipients.length }}</span>
            guardian email{{ preview.recipients.length === 1 ? '' : 's' }} (BCC).
          </p>
          <p
            v-if="audience === 'scheduled' && preview.audienceCount === preview.castCount && preview.castCount > 0"
            class="rounded-md bg-muted p-2 text-xs text-muted-foreground"
          >
            Every performer is called this week (there's an all-company rehearsal), so this
            reaches the same families as “All guardians.”
          </p>
          <p v-if="preview.recipients.length > 0" class="max-h-20 overflow-y-auto break-all rounded-md border border-border p-2 text-xs text-muted-foreground">
            {{ preview.recipients.join(', ') }}
          </p>
          <p v-if="preview.missingEmail.length > 0" class="text-xs text-destructive">
            No guardian email on file: {{ preview.missingEmail.join(', ') }}
          </p>
        </div>

        <div class="mt-5 flex justify-end gap-2">
          <button
            class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
            :disabled="!preview || !preview.configured || preview.recipients.length === 0 || sending"
            :title="!preview?.configured ? 'Server email is not configured' : ''"
            @click="send"
          >
            <Loader2 v-if="sending" class="h-4 w-4 animate-spin" />
            <Mail v-else class="h-4 w-4" />
            {{ sending ? 'Sending…' : `Send to ${preview?.recipients.length ?? 0}` }}
          </button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
