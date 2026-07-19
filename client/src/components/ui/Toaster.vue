<script setup lang="ts">
import { useToasts, dismissToast } from '@/lib/toast'
import { CheckCircle2, AlertTriangle, Info, X } from 'lucide-vue-next'

const toasts = useToasts()
const iconFor = { success: CheckCircle2, error: AlertTriangle, info: Info }
const colorFor = {
  success: 'text-emerald-500',
  error: 'text-destructive',
  info: 'text-muted-foreground',
}
</script>

<template>
  <div
    class="pointer-events-none fixed bottom-4 right-4 z-[100] flex w-full max-w-sm flex-col gap-2"
  >
    <transition-group
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="translate-y-2 opacity-0"
      leave-active-class="transition duration-150 ease-in"
      leave-to-class="translate-x-4 opacity-0"
    >
      <div
        v-for="t in toasts.items"
        :key="t.id"
        class="pointer-events-auto flex items-start gap-3 rounded-md border border-border bg-popover px-3 py-2.5 text-sm text-popover-foreground shadow-lg"
        role="status"
      >
        <component :is="iconFor[t.kind]" class="mt-0.5 h-4 w-4 shrink-0" :class="colorFor[t.kind]" />
        <span class="flex-1 leading-snug">{{ t.message }}</span>
        <button
          class="shrink-0 text-muted-foreground transition-colors hover:text-foreground"
          aria-label="Dismiss"
          @click="dismissToast(t.id)"
        >
          <X class="h-3.5 w-3.5" />
        </button>
      </div>
    </transition-group>
  </div>
</template>
