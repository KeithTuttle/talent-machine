<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Plus, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import type { Person } from '@/types'

const people = ref<Person[]>([])
const search = ref('')
const firstName = ref('')
const lastName = ref('')

onMounted(async () => {
  people.value = await api.get<Person[]>('/people').then((r) => r.data).catch(() => [])
})

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return people.value
  return people.value.filter((p) =>
    `${p.firstName} ${p.lastName}`.toLowerCase().includes(q),
  )
})

async function addPerson() {
  if (!firstName.value.trim()) return
  const { data } = await api.post<Person>('/people', {
    id: 0,
    firstName: firstName.value.trim(),
    lastName: lastName.value.trim(),
    isActive: true,
    createdAt: new Date().toISOString(),
  })
  people.value.push(data)
  toast.success(`${data.firstName} added`)
  firstName.value = ''
  lastName.value = ''
}

async function removePerson(person: Person) {
  const ok = await confirm({
    title: `Delete ${person.firstName} ${person.lastName}?`,
    message: 'Their production history and number casting will be deleted too.',
    destructive: true,
    confirmText: 'Delete',
  })
  if (!ok) return
  await api.delete(`/people/${person.id}`)
  people.value = people.value.filter((p) => p.id !== person.id)
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6 p-6">
    <div>
      <h1 class="font-display text-2xl font-bold">People</h1>
      <p class="text-sm text-muted-foreground">
        Performers across all seasons — one record per person, history spans years.
      </p>
    </div>

    <form class="flex flex-wrap items-end gap-2 rounded-lg border border-border p-4" @submit.prevent="addPerson">
      <label class="flex-1 space-y-1">
        <span class="text-xs font-medium text-muted-foreground">First name</span>
        <input
          v-model="firstName"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <label class="flex-1 space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Last name</span>
        <input
          v-model="lastName"
          class="block w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <button
        type="submit"
        class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
      >
        <Plus class="h-4 w-4" /> Add person
      </button>
    </form>

    <input
      v-model="search"
      placeholder="Search people…"
      class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
    />

    <p v-if="filtered.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      {{ people.length === 0 ? 'No people yet — add your first performer above.' : 'No matches.' }}
    </p>
    <ul v-else class="divide-y divide-border rounded-lg border border-border">
      <li
        v-for="person in filtered"
        :key="person.id"
        class="flex items-center justify-between px-4 py-2.5 text-sm"
      >
        <span>{{ person.firstName }} {{ person.lastName }}</span>
        <button
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
          :aria-label="`Delete ${person.firstName}`"
          @click="removePerson(person)"
        >
          <Trash2 class="h-4 w-4" />
        </button>
      </li>
    </ul>
  </div>
</template>
