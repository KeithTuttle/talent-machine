<script setup lang="ts">
// Autocomplete over the tenant staff directory. Type a name to match existing
// people (returning staff), or create a new one inline. Emits the chosen/created
// StaffMember. Used for per-show creative team and per-number choreographer.
import { computed, onMounted, ref } from 'vue'
import { useStaffStore } from '@/stores/staff'
import type { StaffMember } from '@/types'

const props = withDefaults(defineProps<{ placeholder?: string; excludeIds?: number[] }>(), {
  placeholder: 'Add person…',
  excludeIds: () => [],
})
const emit = defineEmits<{ select: [member: StaffMember] }>()

const staff = useStaffStore()
onMounted(() => staff.fetch())

const query = ref('')
const open = ref(false)

const matches = computed(() => {
  const q = query.value.trim().toLowerCase()
  return staff.members
    .filter((m) => !props.excludeIds.includes(m.id))
    .filter((m) => q === '' || `${m.name} ${m.email ?? ''}`.toLowerCase().includes(q))
    .slice(0, 8)
})

const exactExists = computed(() =>
  staff.members.some((m) => m.name.trim().toLowerCase() === query.value.trim().toLowerCase()),
)

function pick(member: StaffMember) {
  emit('select', member)
  query.value = ''
  open.value = false
}

async function createAndPick() {
  const name = query.value.trim()
  if (!name) return
  const member = await staff.create({ name })
  pick(member)
}
</script>

<template>
  <div class="relative">
    <input
      v-model="query"
      :placeholder="placeholder"
      class="w-full rounded-md border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
      @focus="open = true"
      @keydown.escape="open = false"
      @keydown.enter.prevent="matches[0] ? pick(matches[0]) : createAndPick()"
    />
    <div
      v-if="open && query.trim()"
      class="absolute z-20 mt-1 max-h-56 w-full overflow-y-auto rounded-md border border-border bg-popover p-1 shadow-md"
    >
      <button
        v-for="m in matches"
        :key="m.id"
        type="button"
        class="flex w-full items-center justify-between gap-2 rounded-sm px-2 py-1.5 text-left text-sm hover:bg-accent"
        @mousedown.prevent="pick(m)"
      >
        <span class="truncate">{{ m.name }}</span>
        <span v-if="m.email" class="truncate text-xs text-muted-foreground">{{ m.email }}</span>
      </button>
      <button
        v-if="!exactExists"
        type="button"
        class="flex w-full items-center gap-1.5 rounded-sm border-t border-border px-2 py-1.5 text-left text-sm text-primary hover:bg-accent"
        @mousedown.prevent="createAndPick"
      >
        Add “{{ query.trim() }}” as a new person
      </button>
    </div>
    <!-- Click-away closer -->
    <div v-if="open" class="fixed inset-0 z-10" @mousedown="open = false" />
  </div>
</template>
