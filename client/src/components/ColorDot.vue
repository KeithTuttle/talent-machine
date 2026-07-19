<script setup lang="ts">
// Small colored dot marking a group's color; renders nothing when no color set.
defineProps<{ color?: string | null; size?: 'sm' | 'md' }>()
</script>

<template>
  <span
    v-if="color"
    class="inline-block shrink-0 rounded-full"
    :class="size === 'sm' ? 'h-2 w-2' : 'h-2.5 w-2.5'"
    :style="{ backgroundColor: color }"
    aria-hidden="true"
  />
</template>
