<script setup lang="ts">
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from 'reka-ui'
import { useConfirmState, resolveConfirm } from '@/lib/confirm'

const s = useConfirmState()
</script>

<template>
  <DialogRoot :open="s.open" @update:open="(v: boolean) => { if (!v) resolveConfirm(false) }">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[90] bg-black/40" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-[100] w-[calc(100%-2rem)] max-w-sm -translate-x-1/2 -translate-y-1/2 rounded-lg border border-border bg-background p-5 shadow-xl focus:outline-none"
      >
        <DialogTitle class="text-base font-semibold">{{ s.title }}</DialogTitle>
        <DialogDescription v-if="s.message" class="mt-1.5 text-sm text-muted-foreground">
          {{ s.message }}
        </DialogDescription>
        <!-- Type-to-confirm guard for high-blast-radius deletes -->
        <label v-if="s.requireText" class="mt-3 block space-y-1.5">
          <span class="text-xs text-muted-foreground">
            Type <span class="font-semibold text-foreground">{{ s.requireText }}</span> to confirm
          </span>
          <input
            v-model="s.typed"
            :placeholder="s.requireText"
            autocomplete="off"
            class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </label>
        <div class="mt-5 flex justify-end gap-2">
          <button
            class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent hover:text-accent-foreground"
            @click="resolveConfirm(false)"
          >
            {{ s.cancelText }}
          </button>
          <button
            class="rounded-md px-3 py-1.5 text-sm font-medium hover:opacity-90 disabled:opacity-50"
            :class="
              s.destructive
                ? 'bg-destructive text-destructive-foreground'
                : 'bg-primary text-primary-foreground'
            "
            :disabled="!!s.requireText && s.typed.trim() !== s.requireText"
            @click="resolveConfirm(true)"
          >
            {{ s.confirmText }}
          </button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
