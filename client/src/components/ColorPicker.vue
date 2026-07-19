<script setup lang="ts">
// Preset swatches + a native color input for custom shades. v-model is the hex
// string (or null for "no color").
import { GROUP_COLORS } from '@/lib/colors'

const model = defineModel<string | null>()
</script>

<template>
  <div class="flex flex-wrap items-center gap-1">
    <button
      v-for="c in GROUP_COLORS"
      :key="c.hex"
      type="button"
      class="h-5 w-5 rounded-full border transition-transform hover:scale-110"
      :class="model === c.hex ? 'border-foreground ring-1 ring-ring' : 'border-border'"
      :style="{ backgroundColor: c.hex }"
      :title="c.name"
      :aria-label="`${c.name}${model === c.hex ? ' (selected)' : ''}`"
      @click="model = model === c.hex ? null : c.hex"
    />
    <input
      type="color"
      class="h-5 w-6 cursor-pointer rounded border border-border bg-background p-0"
      :value="model ?? '#888888'"
      title="Custom color"
      aria-label="Custom color"
      @input="model = ($event.target as HTMLInputElement).value"
    />
    <button
      v-if="model"
      type="button"
      class="text-xs text-muted-foreground hover:text-foreground"
      @click="model = null"
    >
      clear
    </button>
  </div>
</template>
